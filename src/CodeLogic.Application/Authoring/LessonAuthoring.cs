using System.Collections.ObjectModel;

using CodeLogic.Application.Analysis;

namespace CodeLogic.Application.Authoring;

public enum AuthoredLessonStatus
{
    Draft = 1,
    Published = 2,
}

public sealed record LessonAuthoringInput(
    string Title,
    string Objective,
    string Content,
    string SampleCode);

public sealed record AuthoredLesson(
    Guid Id,
    AuthoredLessonStatus Status,
    int Version,
    string OwnerUserId,
    string Title,
    string Objective,
    string Content,
    string SampleCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public enum LessonAuthoringResultStatus
{
    Success = 1,
    ValidationFailed = 2,
    NotFound = 3,
    Forbidden = 4,
    Conflict = 5,
}

public sealed class LessonAuthoringResult
{
    public LessonAuthoringResult(
        LessonAuthoringResultStatus status,
        AuthoredLesson? lesson = null,
        IEnumerable<string>? errors = null)
    {
        Status = status;
        Lesson = lesson;
        Errors = new ReadOnlyCollection<string>(errors?.ToArray() ?? []);

        if ((status == LessonAuthoringResultStatus.Success) != (lesson is not null))
        {
            throw new ArgumentException("Only a successful authoring result can contain a lesson.", nameof(lesson));
        }
    }

    public LessonAuthoringResultStatus Status { get; }

    public AuthoredLesson? Lesson { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsSuccess => Status == LessonAuthoringResultStatus.Success;
}

public static class LessonAuthoringValidator
{
    public const int MaximumTitleLength = 200;
    public const int MaximumObjectiveLength = 1_000;
    public const int MaximumContentLength = 10_000;
    public const int MaximumSampleCodeLength = CodeAnalysisDefaults.MaximumCharacters;

    public static IReadOnlyList<string> Validate(LessonAuthoringInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<string>();
        ValidateText(input.Title, "Title", MaximumTitleLength, errors);
        ValidateText(input.Objective, "Objective", MaximumObjectiveLength, errors);
        ValidateText(input.Content, "Lesson content", MaximumContentLength, errors);
        ValidateText(input.SampleCode, "Sample code", MaximumSampleCodeLength, errors);
        return errors.AsReadOnly();
    }

    private static void ValidateText(
        string? value,
        string label,
        int maximumLength,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }
        else if (value.Length > maximumLength)
        {
            errors.Add($"{label} cannot exceed {maximumLength:N0} characters.");
        }
    }
}

public interface ILessonAuthoringService
{
    Task<IReadOnlyList<AuthoredLesson>> GetOwnedAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<AuthoredLesson?> GetOwnedByIdAsync(
        Guid id,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthoredLesson>> GetPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<AuthoredLesson?> GetPublishedByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LessonAuthoringResult> CreateDraftAsync(
        LessonAuthoringInput input,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<LessonAuthoringResult> UpdateDraftAsync(
        Guid id,
        LessonAuthoringInput input,
        string ownerUserId,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<LessonAuthoringResult> PublishAsync(
        Guid id,
        string ownerUserId,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
