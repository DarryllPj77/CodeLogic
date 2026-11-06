using CodeLogic.Application.Flowcharts;

namespace CodeLogic.Web.Playground;

public static class SourceMappedSourceEditor
{
    public static bool TryReplace(
        string source,
        SourceSpan span,
        string? replacement,
        int maximumCharacters,
        out string candidate,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(span);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);

        candidate = source;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(replacement))
        {
            error = "Node source cannot be empty.";
            return false;
        }

        if (span.StartOffset > source.Length || span.Length > source.Length - span.StartOffset)
        {
            error = "The node mapping is stale. Generate the flowchart again before editing this node.";
            return false;
        }

        var normalized = replacement.Trim();
        if (source.Length - span.Length + normalized.Length > maximumCharacters)
        {
            error = $"The edited source would exceed the {maximumCharacters:N0}-character limit.";
            return false;
        }

        candidate = string.Concat(
            source.AsSpan(0, span.StartOffset),
            normalized,
            source.AsSpan(span.StartOffset + span.Length));
        return true;
    }
}
