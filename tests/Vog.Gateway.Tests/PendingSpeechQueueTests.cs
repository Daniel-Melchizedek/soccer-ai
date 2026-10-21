using Vog.Contracts;

namespace Vog.Gateway.Tests;

public sealed class PendingSpeechQueueTests
{
    [Fact]
    public async Task OverflowEvictsOldestLowestPriorityAndPreservesArrivalOrder()
    {
        var queue = new PendingSpeechQueue(3);
        var goal = Call("goal", Priority.P0);
        var routine = Call("routine", Priority.P4);
        var laterRoutine = Call("later-routine", Priority.P4);
        var shot = Call("shot", Priority.P1);
        Assert.Null(queue.Enqueue(goal));
        Assert.Null(queue.Enqueue(routine));
        Assert.Null(queue.Enqueue(laterRoutine));
        Assert.Same(routine, queue.Enqueue(shot));
        queue.Complete();
        Assert.Same(goal, (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance);
        Assert.Same(laterRoutine, (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance);
        Assert.Same(shot, (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance);
        Assert.Null(await queue.DequeueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RoutineCallsCannotDisplaceCriticalCalls()
    {
        var queue = new PendingSpeechQueue(1);
        var goal = Call("goal", Priority.P0);
        var routine = Call("routine", Priority.P1);
        queue.Enqueue(goal);
        Assert.Same(routine, queue.Enqueue(routine));
        Assert.Same(goal, (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance);
    }

    [Fact]
    public async Task CriticalOnlyOverflowRetainsNewestCallsWithinCapacity()
    {
        var queue = new PendingSpeechQueue(2);
        var oldest = Call("oldest", Priority.P0);
        queue.Enqueue(oldest);
        queue.Enqueue(Call("second", Priority.P0));
        Assert.Same(oldest, queue.Enqueue(Call("third", Priority.P0)));
        queue.Complete();
        Assert.Equal("second", (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance.UtteranceId);
        Assert.Equal("third", (await queue.DequeueAsync(TestContext.Current.CancellationToken))!.Utterance.UtteranceId);
        Assert.Null(await queue.DequeueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitingReaderWakesOnEnqueueAndCompletion()
    {
        var queue = new PendingSpeechQueue(1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = queue.DequeueAsync(timeout.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        var goal = Call("goal", Priority.P0);
        queue.Enqueue(goal);
        Assert.Same(goal, (await waiting)!.Utterance);
        var finishing = queue.DequeueAsync(timeout.Token).AsTask();
        queue.Complete();
        Assert.Null(await finishing);
        Assert.Same(goal, queue.Enqueue(goal));
    }

    [Fact]
    public async Task EmptyQueueReadCanBeCancelled()
    {
        var queue = new PendingSpeechQueue(1);
        using var cancellation = new CancellationTokenSource();
        var waiting = queue.DequeueAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);
    }

    [Fact]
    public void CapacityMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingSpeechQueue(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingSpeechQueue(-1));
    }

    private static Utterance Call(string id, Priority priority) => new(id,
        new UtteranceIntent(id, "match", IntentKind.Goal, "goal", new Dictionary<string, Slot>(),
            priority, 1, 0, 1000), "en-GB-ad", id, 0, 1000, UtteranceStatus.Done);
}