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
    IConfiguration configuration,
    ILogger<LiveBroadcastService> log) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Lazy<SpeechWorker>> _workers = new();
    private readonly Uri? _endpoint = configuration["VOG_AI_ENDPOINT"] is { Length: > 0 } e ? new Uri(e) : null;
    private MatchPipeline? _pipeline;

    public MatchInfo? Info => _pipeline?.Info;

    public bool SpeechEnabled => _endpoint is not null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_endpoint is null)
        {
            log.LogWarning("VOG_AI_ENDPOINT is not set: captions only, no speech. Run infra/provision.ps1 to create .env.local.");
        }

        var state = BroadcastStateAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var runner = new LiveMatchRunner(GoldenMatch.Setup(options.Value.Seed), directory.Available, options.Value.Speed);
            _pipeline = runner.Pipeline;
            log.LogInformation("Kick-off: {Home} v {Away} (seed {Seed}, speed {Speed}x)", Info!.Home.Name, Info.Away.Name, options.Value.Seed, options.Value.Speed);
            try
            {
                await runner.RunAsync(OnBoothEventAsync, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await state;
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var worker in _workers.Values.Where(w => w.IsValueCreated))
        {
            await worker.Value.DisposeAsync();
        }
    }
}
