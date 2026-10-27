using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Vog.Booth;
using Vog.Contracts;
using Vog.Domain;
using Vog.Pipeline;
using Vog.Stats;

namespace Vog.Gateway;

/// <summary>
/// Runs the golden match live, round after round, and broadcasts each channel's commentary:
/// the utterance as the booth starts it, then its audio (streamed), then word-timed captions.
/// Preemptions are forwarded so clients stop the cut utterance where the booth cut it.
/// </summary>
public sealed class LiveBroadcastService(
    IHubContext<CommentaryHub> hub,
    ChannelDirectory directory,
    LatencyStats stats,
    IOptions<BroadcastOptions> options,
    IOptions<AzureAiOptions> azure,
    MatchArchiveService archive,
    ILogger<LiveBroadcastService> log) : BackgroundService
{
    /// <summary>Five pitch updates a second: smooth enough to follow by ear once the browser interpolates.</summary>
    private static readonly TimeSpan PitchInterval = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentDictionary<string, Lazy<SpeechWorker>> _workers = new();
    private readonly Uri _endpoint = azure.Value.SpeechEndpoint!;
    private readonly TaskCompletionSource _firstListener = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _matchReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MatchPipeline? _pipeline;

    public MatchInfo? Info => _pipeline?.Info;

    /// <summary>The live match as of the latest tick, for the companion's tools.</summary>
    public MatchSnapshot? Snapshot => _pipeline?.Snapshot;

    public async Task StartMatchAsync(CancellationToken cancellationToken)
    {
        _firstListener.TrySetResult();
        await _matchReady.Task.WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seed = options.Value.Seed;
        var matchLength = MatchSchedule.LengthOf(GoldenMatch.Setup(seed));
        await _firstListener.Task.WaitAsync(stoppingToken);
        var schedule = new MatchSchedule(matchLength, TimeSpan.FromSeconds(options.Value.BreakSeconds), options.Value.Speed)
        {
            StartsAt = DateTimeOffset.UtcNow,
        };
        var state = BroadcastStateAsync(stoppingToken);
        var pitch = BroadcastPitchAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (matchMs, untilKickOff) = schedule.At(DateTimeOffset.UtcNow);
                if (matchMs is null)
                {
                    log.LogInformation("Break: next kick-off in {Seconds:0} s", untilKickOff.TotalSeconds);
                    await Task.Delay(untilKickOff, stoppingToken);
                    continue;
                }

                var runner = new LiveMatchRunner(GoldenMatch.Setup(seed), directory.Available, options.Value.Speed);
                _pipeline = runner.Pipeline;
                _matchReady.TrySetResult();
                log.LogInformation("Starting shared {Home} v {Away} (seed {Seed}) {Minutes:0.0} minutes in", Info!.Home.Name, Info.Away.Name, seed, matchMs.Value / 60_000.0);
                await runner.RunAsync(OnBoothEventAsync, stoppingToken, () => schedule.At(DateTimeOffset.UtcNow).MatchMs ?? long.MaxValue);

                // Archive a lightweight match summary after each completed match.
                // This is fire-and-forget: a failure never delays the next match.
                if (_pipeline is { Ball: { } ball, Info: { } info, Frame: { } frame })
                {
                    // Collect goals from the snapshot's key events list.
                    var goals = (_pipeline.Snapshot?.KeyEvents ?? [])
                        .Where(e => e.Type == MatchEventType.Goal)
                        .Select(e =>
                        {
                            var player = info.FindPlayer(e.PlayerId ?? string.Empty);
                            var teamName = e.TeamId == info.Home.TeamId ? info.Home.Name : info.Away.Name;
                            var minute = (int)(e.MatchClockMs / 60_000);
                            return new GoalRecord(minute, teamName, player?.ShortName ?? e.PlayerId ?? "Unknown");
                        })
                        .ToList();
                    var summary = new MatchSummary(
                        seed,
                        DateTimeOffset.UtcNow,
                        ball.HomeScore,
                        ball.AwayScore,
                        info.Home.Name,
                        info.Away.Name,
                        goals,
                        frame.PtsMs);
                    _ = archive.ArchiveAsync(summary);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await Task.WhenAll(state, pitch);
    }

    private async ValueTask OnBoothEventAsync(ChannelBoothEvent e)
    {
        // Only channels with listeners are spoken: cost scales with channels in use.
        var channelId = e.Channel.Id;
        if (!directory.HasAnyListeners(channelId))
        {
            return;
        }

        var u = e.Event.Utterance;
        switch (e.Event.Kind)
        {
            case BoothEventKind.Started:
                var message = UtteranceMessage.From(u, e.Frame, e.Channel.Lang);
                if (directory.HasListeners(channelId))
                {
                    if (options.Value.Speak(u.Intent.Kind))
                    {
                        await hub.Clients.Group(channelId).SendAsync("utterance", message);
                        if (Worker(e.Channel).Enqueue(u) is { } dropped)
                        {
                            await hub.Clients.Group(channelId).SendAsync("skipped", dropped.UtteranceId);
                        }
                    }
                }

                if (directory.HasTextListeners(channelId))
                {
                    await hub.Clients.Group($"{channelId}:text").SendAsync("utterance", message);
                }
                break;

            case BoothEventKind.Finished when u.Status == UtteranceStatus.Preempted && directory.HasListeners(channelId) && options.Value.Speak(u.Intent.Kind) && options.Value.Speed <= 4:
                await hub.Clients.Group(channelId).SendAsync("cut", u.UtteranceId, u.DurationMs);
                break;
        }
    }

    /// <summary>Starts a channel's speech when its first listener joins, so it's warm before the next line.</summary>
    public void Prepare(ChannelSpec channel) => _ = Worker(channel);

    private SpeechWorker Worker(ChannelSpec channel) =>
        _workers.GetOrAdd(channel.Id, _ => new Lazy<SpeechWorker>(() => new SpeechWorker(channel, _endpoint, hub, stats, log))).Value;

    /// <summary>Clock and score once a second, for the visual display and screen-reader status.</summary>
    private async Task BroadcastStateAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_pipeline is { Frame: { } frame, Ball: { } ball })
                {
                    var message = new StateMessage(MatchClock.Display(frame.Period, frame.MatchClockMs), frame.Period, ball.HomeScore, ball.AwayScore, ball.Status == BallStatus.InPlay, options.Value.Speed);
                    await hub.Clients.All.SendAsync("state", message, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>The ball and players five times a second; nothing is sent between matches.</summary>
    private async Task BroadcastPitchAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PitchInterval);
        long lastFrame = -1;
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (Snapshot is { } snapshot && snapshot.Frame.FrameId != lastFrame)
                {
                    lastFrame = snapshot.Frame.FrameId;
                    await hub.Clients.All.SendAsync("pitch", PitchMessage.From(snapshot), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var worker in _workers.Values.Where(w => w.IsValueCreated))
        {
            await worker.Value.DisposeAsync();
        }
    }
}
