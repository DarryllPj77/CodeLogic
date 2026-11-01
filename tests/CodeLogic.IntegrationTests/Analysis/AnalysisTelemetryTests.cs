using System.Diagnostics;
using System.Diagnostics.Metrics;

using CodeLogic.Application.Analysis;
using CodeLogic.Infrastructure.Analysis;

namespace CodeLogic.IntegrationTests.Analysis;

public sealed class AnalysisTelemetryTests
{
    [Fact]
    public async Task AnalysisEmitsPrivacySafeActivityAndMetrics()
    {
        const string source = "int score = 5;";
        Activity? stoppedActivity = null;
        var operationStatuses = new List<string>();
        var durationStatuses = new List<string>();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource =>
                activitySource.Name == AnalysisTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("code_logic.analysis.source_length"), source.Length))
                {
                    stoppedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AnalysisTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>(
            (instrument, _, tags, _) =>
            {
                if (instrument.Name == AnalysisTelemetry.CompletedOperationsName)
                {
                    operationStatuses.Add(ReadStatus(tags));
                }
            });
        meterListener.SetMeasurementEventCallback<double>(
            (instrument, _, tags, _) =>
            {
                if (instrument.Name == AnalysisTelemetry.DurationName)
                {
                    durationStatuses.Add(ReadStatus(tags));
                }
            });
        meterListener.Start();

        var result = await new RoslynCodeAnalysisService().AnalyzeAsync(source);

        Assert.True(result.IsValid);
        Assert.Contains(nameof(CodeAnalysisStatus.Valid), operationStatuses);
        Assert.Contains(nameof(CodeAnalysisStatus.Valid), durationStatuses);
        Assert.NotNull(stoppedActivity);
        Assert.Equal("analyze", stoppedActivity.OperationName);
        Assert.Equal(source.Length, stoppedActivity.GetTagItem("code_logic.analysis.source_length"));
        Assert.Equal("Valid", stoppedActivity.GetTagItem("code_logic.analysis.status"));
        Assert.DoesNotContain(
            stoppedActivity.Tags,
            tag => string.Equals(tag.Value, source, StringComparison.Ordinal));
    }

    private static string ReadStatus(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "code_logic.analysis.status")
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
