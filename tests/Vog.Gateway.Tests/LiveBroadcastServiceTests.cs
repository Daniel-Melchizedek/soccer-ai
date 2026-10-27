using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Vog.Gateway.Tests;

public sealed class LiveBroadcastServiceTests
{
    [Fact]
    public async Task FirstListenerStartsAtZeroAndLaterJoinDoesNotResetMatch()
    {
        var hub = new RecordingHub();
        var options = Options.Create(new BroadcastOptions());
        using var service = new LiveBroadcastService(hub, new ChannelDirectory(options), new LatencyStats(), options,
            Options.Create(new AzureAiOptions()), null!, NullLogger<LiveBroadcastService>.Instance);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await service.StartAsync(timeout.Token);
        try
        {
            Assert.Null(service.Info);
            Assert.Null(service.Snapshot);
            await service.StartMatchAsync(timeout.Token);
            var info = service.Info;
            Assert.NotNull(info);
            var first = await hub.States.Reader.ReadAsync(timeout.Token);
            Assert.StartsWith("00:", first.Clock);
            var next = await hub.States.Reader.ReadAsync(timeout.Token);
            Assert.NotEqual(first.Clock, next.Clock);
            await Task.WhenAll(service.StartMatchAsync(timeout.Token), service.StartMatchAsync(timeout.Token));
            Assert.Same(info, service.Info);
            var later = await hub.States.Reader.ReadAsync(timeout.Token);
            Assert.NotEqual(first.Clock, later.Clock);
        }
        finally
        {
            await service.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task ShutdownBeforeFirstListenerDoesNotStartAMatch()
    {
        var options = Options.Create(new BroadcastOptions());
        using var service = new LiveBroadcastService(new RecordingHub(), new ChannelDirectory(options), new LatencyStats(), options,
            Options.Create(new AzureAiOptions()), null!, NullLogger<LiveBroadcastService>.Instance);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await service.StartAsync(timeout.Token);
        await service.StopAsync(timeout.Token);
        Assert.Null(service.Info);
    }

    private sealed class RecordingHub : IHubContext<CommentaryHub>, IHubClients, IClientProxy
    {
        public Channel<StateMessage> States { get; } = Channel.CreateUnbounded<StateMessage>();
        IHubClients IHubContext<CommentaryHub>.Clients => this;
        IGroupManager IHubContext<CommentaryHub>.Groups => throw new NotSupportedException();
        public IClientProxy All => this;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;
        public IClientProxy Client(string connectionId) => this;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => this;
        public IClientProxy Group(string groupName) => this;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => this;
        public IClientProxy User(string userId) => this;
        public IClientProxy Users(IReadOnlyList<string> userIds) => this;

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (method == "state") States.Writer.TryWrite((StateMessage)args[0]!);
            return Task.CompletedTask;
        }
    }
}