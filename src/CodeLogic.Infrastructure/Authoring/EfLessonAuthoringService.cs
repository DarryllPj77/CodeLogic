using CodeLogic.Application.Authoring;
using CodeLogic.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeLogic.Infrastructure.Authoring;

public sealed partial class EfLessonAuthoringService(
    CodeLogicDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<EfLessonAuthoringService> logger) : ILessonAuthoringService
{
    public async Task<IReadOnlyList<AuthoredLesson>> GetOwnedAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        var entities = await dbContext.AuthoredLessons
            .AsNoTracking()
            .Where(lesson => lesson.OwnerUserId == ownerUserId)
            .ToArrayAsync(cancellationToken);
        return entities
            .OrderByDescending(lesson => lesson.UpdatedAtUtc)
            .Select(ToModel)
            .ToArray();
    }

    public async Task<AuthoredLesson?> GetOwnedByIdAsync(
        Guid id,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        var entity = await dbContext.AuthoredLessons
            .AsNoTracking()
            .SingleOrDefaultAsync(
                lesson => lesson.Id == id && lesson.OwnerUserId == ownerUserId,
                cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<AuthoredLesson>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.AuthoredLessons
            .AsNoTracking()
            .Where(lesson => lesson.Status == (int)AuthoredLessonStatus.Published)
            .ToArrayAsync(cancellationToken);
        return entities
            .OrderByDescending(lesson => lesson.PublishedAtUtc)
            .Select(ToModel)
            .ToArray();
    }

    public async Task<AuthoredLesson?> GetPublishedByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AuthoredLessons
            .AsNoTracking()
            .SingleOrDefaultAsync(
                lesson => lesson.Id == id
                    && lesson.Status == (int)AuthoredLessonStatus.Published,
                cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<LessonAuthoringResult> CreateDraftAsync(
        LessonAuthoringInput input,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        var errors = LessonAuthoringValidator.Validate(input);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var now = timeProvider.GetUtcNow();
        var entity = new AuthoredLessonEntity
        {
            Id = Guid.NewGuid(),
            Status = (int)AuthoredLessonStatus.Draft,
            Version = 1,
            OwnerUserId = ownerUserId,
            Title = input.Title.Trim(),
            Objective = input.Objective.Trim(),
            Content = input.Content.Trim(),
            SampleCode = input.SampleCode.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.AuthoredLessons.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogDraftCreated(logger, entity.Id, entity.Version);
        return Success(entity);
    }

    public async Task<LessonAuthoringResult> UpdateDraftAsync(
        Guid id,
        LessonAuthoringInput input,
        string ownerUserId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        var errors = LessonAuthoringValidator.Validate(input);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var lookup = await FindForMutationAsync(id, ownerUserId, cancellationToken);
        if (lookup.Result is not null)
        {
            return lookup.Result;
        }

        var entity = lookup.Entity!;
        if (entity.Status != (int)AuthoredLessonStatus.Draft)
        {
            return Conflict("Published lessons are read-only in this first editorial workflow.");
        }

        if (entity.Version != expectedVersion)
        {
            return Conflict("This draft changed after it was loaded. Reload it before saving again.");
        }

        entity.Title = input.Title.Trim();
        entity.Objective = input.Objective.Trim();
        entity.Content = input.Content.Trim();
        entity.SampleCode = input.SampleCode.Trim();
        entity.Version++;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        var result = await SaveMutationAsync(entity, cancellationToken);
        if (result.IsSuccess)
        {
            LogDraftUpdated(logger, entity.Id, entity.Version);
        }

        return result;
    }

    public async Task<LessonAuthoringResult> PublishAsync(
        Guid id,
        string ownerUserId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        var lookup = await FindForMutationAsync(id, ownerUserId, cancellationToken);
        if (lookup.Result is not null)
        {
            return lookup.Result;
        }

        var entity = lookup.Entity!;
        if (entity.Status != (int)AuthoredLessonStatus.Draft)
        {
            return Conflict("This lesson is already published.");
        }

        if (entity.Version != expectedVersion)
        {
            return Conflict("This draft changed after it was loaded. Reload it before publishing.");
        }

        var errors = LessonAuthoringValidator.Validate(new LessonAuthoringInput(
            entity.Title,
            entity.Objective,
            entity.Content,
            entity.SampleCode));
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var now = timeProvider.GetUtcNow();
        entity.Status = (int)AuthoredLessonStatus.Published;
        entity.Version++;
        entity.UpdatedAtUtc = now;
        entity.PublishedAtUtc = now;
        var result = await SaveMutationAsync(entity, cancellationToken);
        if (result.IsSuccess)
        {
            LogLessonPublished(logger, entity.Id, entity.Version);
        }

        return result;
    }

    private async Task<(AuthoredLessonEntity? Entity, LessonAuthoringResult? Result)> FindForMutationAsync(
        Guid id,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AuthoredLessons.SingleOrDefaultAsync(
            lesson => lesson.Id == id,
            cancellationToken);
        if (entity is null)
        {
            return (null, new LessonAuthoringResult(
                LessonAuthoringResultStatus.NotFound,
                errors: ["The authored lesson was not found."]));
        }

        if (!string.Equals(entity.OwnerUserId, ownerUserId, StringComparison.Ordinal))
        {
            LogOwnershipRejected(logger, id);
            return (null, new LessonAuthoringResult(
                LessonAuthoringResultStatus.Forbidden,
                errors: ["Only the owning administrator can change this lesson."]));
        }

        return (entity, null);
    }

    private async Task<LessonAuthoringResult> SaveMutationAsync(
        AuthoredLessonEntity entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Success(entity);
        }
        catch (DbUpdateConcurrencyException)
        {
            LogConcurrencyRejected(logger, entity.Id);
            return Conflict("This lesson changed concurrently. Reload it and review the newest version.");
        }
    }

    private static LessonAuthoringResult Success(AuthoredLessonEntity entity) => new(
        LessonAuthoringResultStatus.Success,
        ToModel(entity));

    private static LessonAuthoringResult ValidationFailed(IEnumerable<string> errors) => new(
        LessonAuthoringResultStatus.ValidationFailed,
        errors: errors);

    private static LessonAuthoringResult Conflict(string error) => new(
        LessonAuthoringResultStatus.Conflict,
        errors: [error]);

    private static AuthoredLesson ToModel(AuthoredLessonEntity entity) => new(
        entity.Id,
        (AuthoredLessonStatus)entity.Status,
        entity.Version,
        entity.OwnerUserId,
        entity.Title,
        entity.Objective,
        entity.Content,
        entity.SampleCode,
        entity.CreatedAtUtc,
        entity.UpdatedAtUtc,
        entity.PublishedAtUtc);

    private static void ValidateOwner(string ownerUserId) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

    [LoggerMessage(2001, LogLevel.Information, "Created private authored lesson draft {LessonId} at version {Version}.")]
    private static partial void LogDraftCreated(ILogger logger, Guid lessonId, int version);

    [LoggerMessage(2002, LogLevel.Information, "Updated authored lesson draft {LessonId} to version {Version}.")]
    private static partial void LogDraftUpdated(ILogger logger, Guid lessonId, int version);

    [LoggerMessage(2003, LogLevel.Information, "Published authored lesson {LessonId} at version {Version}.")]
    private static partial void LogLessonPublished(ILogger logger, Guid lessonId, int version);

    [LoggerMessage(2004, LogLevel.Warning, "Rejected an authored lesson mutation because the requester does not own lesson {LessonId}.")]
    private static partial void LogOwnershipRejected(ILogger logger, Guid lessonId);

    [LoggerMessage(2005, LogLevel.Warning, "Rejected a concurrent authored lesson mutation for lesson {LessonId}.")]
    private static partial void LogConcurrencyRejected(ILogger logger, Guid lessonId);
}
