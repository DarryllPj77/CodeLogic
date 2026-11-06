using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class PlaygroundFlowchartDebouncerTests
{
    [Fact]
    public async Task NewScheduleSupersedesPendingSource()
    {
        using var debouncer = new PlaygroundFlowchartDebouncer(TimeSpan.FromMilliseconds(30));
        var applied = new List<string>();

        var first = debouncer.ScheduleAsync("first", ApplyAsync);
        var second = debouncer.ScheduleAsync("second", ApplyAsync);
        await Task.WhenAll(first, second);

        Assert.Equal(["second"], applied);

        Task ApplyAsync(string source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            applied.Add(source);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CancelPreventsPendingUpdate()
    {
        using var debouncer = new PlaygroundFlowchartDebouncer(TimeSpan.FromMilliseconds(30));
        var wasApplied = false;

        var pending = debouncer.ScheduleAsync(
            "source",
            (_, _) =>
            {
                wasApplied = true;
                return Task.CompletedTask;
            });
        debouncer.Cancel();
        await pending;

        Assert.False(wasApplied);
    }
}
