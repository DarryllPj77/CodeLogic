namespace CodeLogic.Web.Playground;

public sealed class PlaygroundFlowchartDebouncer : IDisposable
{
    private readonly TimeSpan delay;
    private CancellationTokenSource? cancellation;

    public PlaygroundFlowchartDebouncer(TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        this.delay = delay;
    }

    public async Task ScheduleAsync(
        string source,
        Func<string, CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var current = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref cancellation, current);
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(delay, current.Token);
            await action(source, current.Token);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            // A newer editor change superseded this update.
        }
    }

    public void Cancel()
    {
        var current = Interlocked.Exchange(ref cancellation, null);
        current?.Cancel();
        current?.Dispose();
    }

    public void Dispose() => Cancel();
}
