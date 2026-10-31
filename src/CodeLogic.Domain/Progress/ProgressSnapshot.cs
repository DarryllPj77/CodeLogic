using System.Collections.ObjectModel;

using CodeLogic.Domain;

namespace CodeLogic.Domain.Progress;

public sealed class ProgressSnapshot
{
    private readonly ReadOnlyCollection<LessonId> completedLessonIds;

    public ProgressSnapshot(
        int totalLessonCount,
        IEnumerable<LessonId> completedLessonIds,
        int attemptCount,
        int correctAttemptCount,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalLessonCount);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(correctAttemptCount);
        ArgumentNullException.ThrowIfNull(completedLessonIds);
        Guard.UtcTimestamp(capturedAtUtc, nameof(capturedAtUtc));

        var completedIds = completedLessonIds.ToArray();
        if (completedIds.Any(id => id.IsEmpty))
        {
            throw new ArgumentException("Completed lesson identifiers cannot be empty.", nameof(completedLessonIds));
        }

        if (completedIds.Distinct().Count() != completedIds.Length)
        {
            throw new ArgumentException("Completed lesson identifiers must be unique.", nameof(completedLessonIds));
        }

        if (completedIds.Length > totalLessonCount)
        {
            throw new ArgumentException(
                "Completed lesson count cannot exceed total lesson count.",
                nameof(completedLessonIds));
        }

        if (correctAttemptCount > attemptCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctAttemptCount),
                correctAttemptCount,
                "Correct attempt count cannot exceed total attempt count.");
        }

        TotalLessonCount = totalLessonCount;
        this.completedLessonIds = Array.AsReadOnly(completedIds);
        AttemptCount = attemptCount;
        CorrectAttemptCount = correctAttemptCount;
        CapturedAtUtc = capturedAtUtc;
    }

    public int TotalLessonCount { get; }

    public IReadOnlyList<LessonId> CompletedLessonIds => completedLessonIds;

    public int CompletedLessonCount => completedLessonIds.Count;

    public int AttemptCount { get; }

    public int CorrectAttemptCount { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public decimal CompletionPercentage => TotalLessonCount == 0
        ? 0m
        : decimal.Round(
            CompletedLessonCount * 100m / TotalLessonCount,
            2,
            MidpointRounding.AwayFromZero);
}
