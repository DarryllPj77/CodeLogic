using CodeLogic.Application.Execution;

namespace CodeLogic.Web.Playground;

public sealed class ExecutionTraceNavigator
{
    private readonly IReadOnlyList<ExecutionTraceStep> steps;

    public ExecutionTraceNavigator(ExecutionTraceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.HasTrace)
        {
            throw new ArgumentException("A trace navigator requires a completed or limited trace.", nameof(result));
        }

        steps = result.Steps;
    }

    public int CurrentIndex { get; private set; }

    public ExecutionTraceStep Current => steps[CurrentIndex];

    public int StepNumber => CurrentIndex + 1;

    public int StepCount => steps.Count;

    public bool CanStep => CurrentIndex < steps.Count - 1;

    public void Step()
    {
        if (CanStep)
        {
            CurrentIndex++;
        }
    }

    public void Reset() => CurrentIndex = 0;
}
