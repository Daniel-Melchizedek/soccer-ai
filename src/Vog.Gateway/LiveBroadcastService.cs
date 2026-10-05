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
    ILogger<LiveBroadcastService> log) : BackgroundService
{
    /// <summary>Between matches; listeners stay connected and hear the next kick-off.</summary>
    private static readonly TimeSpan MatchBreak = TimeSpan.FromMinutes(2);

    /// <summary>Five pitch updates a second: smooth enough to follow by ear once the browser interpolates.</summary>
    private static readonly TimeSpan PitchInterval = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentDictionary<string, Lazy<SpeechWorker>> _workers = new();
    private readonly Uri _endpoint = azure.Value.SpeechEndpoint!;
    private MatchPipeline? _pipeline;

    public MatchInfo? Info => _pipeline?.Info;

    /// <summary>The live match as of the latest tick, for the companion's tools.</summary>
    public MatchSnapshot? Snapshot => _pipeline?.Snapshot;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = BroadcastStateAsync(stoppingToken);
        var pitch = BroadcastPitchAsync(stoppingToken);
        var seed = options.Value.Seed;
        var schedule = new MatchSchedule(MatchSchedule.LengthOf(GoldenMatch.Setup(seed)), MatchBreak, options.Value.Speed);
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
                log.LogInformation("Joining {Home} v {Away} (seed {Seed}) {Minutes:0.0} minutes in, on the schedule every gateway shares", Info!.Home.Name, Info.Away.Name, seed, matchMs.Value / 60_000.0);
                await runner.RunAsync(OnBoothEventAsync, stoppingToken, () => schedule.At(DateTimeOffset.UtcNow).MatchMs ?? long.MaxValue);
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
        if (!directory.HasListeners(channelId))
        {
            return;
        }

        var u = e.Event.Utterance;
        switch (e.Event.Kind)
        {
            case BoothEventKind.Started:
                await hub.Clients.Group(channelId).SendAsync("utterance", UtteranceMessage.From(u, e.Frame));
                Worker(e.Channel).Enqueue(u);
                break;

            case BoothEventKind.Finished when u.Status == UtteranceStatus.Preempted:
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
                    var message = new StateMessage(MatchClock.Display(frame.Period, frame.MatchClockMs), frame.Period, ball.HomeScore, ball.AwayScore, ball.Status == BallStatus.InPlay);
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
