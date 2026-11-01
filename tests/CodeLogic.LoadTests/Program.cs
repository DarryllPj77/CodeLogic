using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using CodeLogic.Application.Analysis;
using CodeLogic.Infrastructure.Analysis;

const int warmupOperations = 50;
var options = LoadTestOptions.Parse(args);
var service = new RoslynCodeAnalysisService();
AnalysisSample[] samples =
[
    new("int score = 5; score = score + 1;", CodeAnalysisStatus.Valid),
    new("int score = ;", CodeAnalysisStatus.SyntaxError),
    new("System.Diagnostics.Process.Start(\"calc\");", CodeAnalysisStatus.Unsupported),
];

for (var index = 0; index < warmupOperations; index++)
{
    _ = await service.AnalyzeAsync(samples[index % samples.Length].Source);
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var durations = new ConcurrentBag<double>();
var statuses = new ConcurrentDictionary<CodeAnalysisStatus, int>();
var failures = new ConcurrentBag<string>();
var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
var workingSetBefore = Process.GetCurrentProcess().WorkingSet64;
var totalStarted = Stopwatch.GetTimestamp();

await Parallel.ForEachAsync(
    Enumerable.Range(0, options.Operations),
    new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency },
    async (index, cancellationToken) =>
    {
        var sample = samples[index % samples.Length];
        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await service.AnalyzeAsync(sample.Source, cancellationToken);
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            statuses.AddOrUpdate(result.Status, 1, static (_, count) => count + 1);

            if (result.Status != sample.ExpectedStatus)
            {
                failures.Add($"Expected {sample.ExpectedStatus}; received {result.Status}.");
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception.GetType().Name);
        }
    });

var totalElapsed = Stopwatch.GetElapsedTime(totalStarted);
var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
var workingSetAfter = Process.GetCurrentProcess().WorkingSet64;
var orderedDurations = durations.Order().ToArray();
var report = new LoadTestReport(
    DateTimeOffset.UtcNow,
    Environment.Version.ToString(),
    Environment.OSVersion.ToString(),
    Environment.ProcessorCount,
    options.Operations,
    options.Concurrency,
    totalElapsed.TotalMilliseconds,
    options.Operations / totalElapsed.TotalSeconds,
    Percentile(orderedDurations, 0.50),
    Percentile(orderedDurations, 0.95),
    Percentile(orderedDurations, 0.99),
    orderedDurations.Length == 0 ? 0 : orderedDurations[^1],
    allocatedBytes / (double)options.Operations,
    workingSetAfter - workingSetBefore,
    statuses.OrderBy(pair => pair.Key).ToDictionary(),
    failures.Count,
    options.P95BudgetMilliseconds,
    failures.IsEmpty && Percentile(orderedDurations, 0.95) <= options.P95BudgetMilliseconds);

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return report.Passed ? 0 : 1;

static double Percentile(double[] orderedValues, double percentile)
{
    if (orderedValues.Length == 0)
    {
        return 0;
    }

    var index = Math.Clamp(
        (int)Math.Ceiling(percentile * orderedValues.Length) - 1,
        0,
        orderedValues.Length - 1);
    return orderedValues[index];
}

internal sealed record AnalysisSample(string Source, CodeAnalysisStatus ExpectedStatus);

internal sealed record LoadTestOptions(int Operations, int Concurrency, double P95BudgetMilliseconds)
{
    public static LoadTestOptions Parse(string[] arguments)
    {
        var operations = ReadInt(arguments, "--operations", 1_000);
        var concurrency = ReadInt(
            arguments,
            "--concurrency",
            Math.Clamp(Environment.ProcessorCount, 1, 4));
        var p95Budget = ReadDouble(arguments, "--p95-budget-ms", 50);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p95Budget);
        return new LoadTestOptions(operations, concurrency, p95Budget);
    }

    private static int ReadInt(string[] arguments, string name, int defaultValue) =>
        int.Parse(Read(arguments, name, defaultValue.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static double ReadDouble(string[] arguments, string name, double defaultValue) =>
        double.Parse(Read(arguments, name, defaultValue.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static string Read(string[] arguments, string name, string defaultValue)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length
            ? arguments[index + 1]
            : defaultValue;
    }
}

internal sealed record LoadTestReport(
    DateTimeOffset MeasuredAtUtc,
    string Runtime,
    string OperatingSystem,
    int ProcessorCount,
    int Operations,
    int Concurrency,
    double TotalMilliseconds,
    double OperationsPerSecond,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds,
    double AllocatedBytesPerOperation,
    long WorkingSetDeltaBytes,
    IReadOnlyDictionary<CodeAnalysisStatus, int> Statuses,
    int Failures,
    double P95BudgetMilliseconds,
    bool Passed);
