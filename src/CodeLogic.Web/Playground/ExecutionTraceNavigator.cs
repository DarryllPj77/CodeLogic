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

    public bool CanStepBack => CurrentIndex > 0;

    public IReadOnlySet<string> ChangedVariables
    {
        get
        {
            if (!CanStepBack)
            {
                return Current.Variables.Keys.ToHashSet(StringComparer.Ordinal);
            }

            var previous = steps[CurrentIndex - 1].Variables;
            return Current.Variables
                .Where(variable => !previous.TryGetValue(variable.Key, out var previousValue)
                    || !string.Equals(previousValue, variable.Value, StringComparison.Ordinal))
                .Select(variable => variable.Key)
                .ToHashSet(StringComparer.Ordinal);
        }
    }

    public int CurrentNodeVisitCount => steps
        .Take(CurrentIndex + 1)
        .Count(step => string.Equals(step.ActiveNodeId, Current.ActiveNodeId, StringComparison.Ordinal));

    public void Step()
    {
        if (CanStep)
        {
            CurrentIndex++;
        }
    }

    public void StepBack()
    {
        if (CanStepBack)
        {
            CurrentIndex--;
        }
    }

    public void Reset() => CurrentIndex = 0;
}
