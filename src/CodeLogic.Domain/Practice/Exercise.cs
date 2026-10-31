using CodeLogic.Domain;

namespace CodeLogic.Domain.Practice;

public sealed class Exercise
{
    public Exercise(
        ExerciseId id,
        LessonId lessonId,
        string title,
        string prompt,
        ExerciseKind kind,
        int order)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("The exercise identifier cannot be empty.", nameof(id));
        }

        if (lessonId.IsEmpty)
        {
            throw new ArgumentException("The lesson identifier cannot be empty.", nameof(lessonId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The exercise kind is not supported.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);

        Id = id;
        LessonId = lessonId;
        Title = Guard.RequiredText(title, nameof(title), 160);
        Prompt = Guard.RequiredText(prompt, nameof(prompt), 2_000);
        Kind = kind;
        Order = order;
    }

    public ExerciseId Id { get; }

    public LessonId LessonId { get; }

    public string Title { get; }

    public string Prompt { get; }

    public ExerciseKind Kind { get; }

    public int Order { get; }
}
