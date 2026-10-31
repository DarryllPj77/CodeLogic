namespace CodeLogic.Domain;

public readonly record struct TopicId
{
    public TopicId(Guid value) => Value = Guard.NonEmptyGuid(value, nameof(value));

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static TopicId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct LessonId
{
    public LessonId(Guid value) => Value = Guard.NonEmptyGuid(value, nameof(value));

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static LessonId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct LessonSectionId
{
    public LessonSectionId(Guid value) => Value = Guard.NonEmptyGuid(value, nameof(value));

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static LessonSectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ExerciseId
{
    public ExerciseId(Guid value) => Value = Guard.NonEmptyGuid(value, nameof(value));

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static ExerciseId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ExerciseAttemptId
{
    public ExerciseAttemptId(Guid value) => Value = Guard.NonEmptyGuid(value, nameof(value));

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static ExerciseAttemptId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
