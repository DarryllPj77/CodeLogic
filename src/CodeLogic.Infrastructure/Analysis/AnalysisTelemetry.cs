using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeLogic.Infrastructure.Analysis;

public static class AnalysisTelemetry
{
    public const string ActivitySourceName = "CodeLogic.Analysis";
    public const string MeterName = "CodeLogic.Analysis";
    public const string CompletedOperationsName = "codelogic.analysis.operations";
    public const string DurationName = "codelogic.analysis.duration";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> CompletedOperations = Meter.CreateCounter<long>(
        CompletedOperationsName,
        unit: "{operation}",
        description: "Number of bounded code-analysis operations by outcome.");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        DurationName,
        unit: "ms",
        description: "Bounded code-analysis duration in milliseconds by outcome.");
}
