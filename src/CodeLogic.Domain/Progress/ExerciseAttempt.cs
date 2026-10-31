using CodeLogic.Domain;

namespace CodeLogic.Domain.Progress;

public sealed class ExerciseAttempt
{
    public ExerciseAttempt(
        ExerciseAttemptId id,
        ExerciseId exerciseId,
        int attemptNumber,
        AttemptOutcome outcome,
        DateTimeOffset attemptedAtUtc)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("The attempt identifier cannot be empty.", nameof(id));
        }

        if (exerciseId.IsEmpty)
        {
            throw new ArgumentException("The exercise identifier cannot be empty.", nameof(exerciseId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The attempt outcome is not supported.");
        }

        Guard.UtcTimestamp(attemptedAtUtc, nameof(attemptedAtUtc));

        Id = id;
        ExerciseId = exerciseId;
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        AttemptedAtUtc = attemptedAtUtc;
    }

    public ExerciseAttemptId Id { get; }

    public ExerciseId ExerciseId { get; }

    public int AttemptNumber { get; }

    public AttemptOutcome Outcome { get; }

    public DateTimeOffset AttemptedAtUtc { get; }
}
