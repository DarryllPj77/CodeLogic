using CodeLogic.Domain;
using CodeLogic.Domain.Progress;

namespace CodeLogic.UnitTests.Domain;

public sealed class ProgressSnapshotTests
{
    [Fact]
    public void SnapshotCalculatesCompletionAndCopiesIdentifiers()
    {
        var completedLessons = new List<LessonId>
        {
            LessonId.New(),
            LessonId.New(),
        };

        var snapshot = new ProgressSnapshot(
            totalLessonCount: 3,
            completedLessons,
            attemptCount: 4,
            correctAttemptCount: 3,
            DateTimeOffset.UnixEpoch);

        completedLessons.Add(LessonId.New());

        Assert.Equal(2, snapshot.CompletedLessonCount);
        Assert.Equal(66.67m, snapshot.CompletionPercentage);
        Assert.Equal(3, snapshot.CorrectAttemptCount);
    }

    [Fact]
    public void EmptySnapshotHasZeroCompletion()
    {
        var snapshot = new ProgressSnapshot(
            totalLessonCount: 0,
            completedLessonIds: [],
            attemptCount: 0,
            correctAttemptCount: 0,
            DateTimeOffset.UnixEpoch);

        Assert.Equal(0m, snapshot.CompletionPercentage);
    }

    [Fact]
    public void SnapshotRejectsDuplicateCompletedLessons()
    {
        var lessonId = LessonId.New();

        var exception = Assert.Throws<ArgumentException>(
            () => new ProgressSnapshot(
                totalLessonCount: 2,
                completedLessonIds: [lessonId, lessonId],
                attemptCount: 0,
                correctAttemptCount: 0,
                DateTimeOffset.UnixEpoch));

        Assert.Equal("completedLessonIds", exception.ParamName);
    }

    [Fact]
    public void SnapshotRejectsMoreCompletedLessonsThanTotal()
    {
        Assert.Throws<ArgumentException>(
            () => new ProgressSnapshot(
                totalLessonCount: 1,
                completedLessonIds: [LessonId.New(), LessonId.New()],
                attemptCount: 0,
                correctAttemptCount: 0,
                DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void SnapshotRejectsMoreCorrectAttemptsThanAttempts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProgressSnapshot(
                totalLessonCount: 1,
                completedLessonIds: [],
                attemptCount: 1,
                correctAttemptCount: 2,
                DateTimeOffset.UnixEpoch));
    }
}
