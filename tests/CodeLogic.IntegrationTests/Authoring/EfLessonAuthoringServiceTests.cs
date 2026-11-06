using CodeLogic.Application.Authoring;
using CodeLogic.Infrastructure.Authoring;
using CodeLogic.Infrastructure.Identity;
using CodeLogic.Infrastructure.Persistence;
using CodeLogic.IntegrationTests.Support;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeLogic.IntegrationTests.Authoring;

public sealed class EfLessonAuthoringServiceTests
{
    [Fact]
    public async Task MigrationSeedsAdministratorRoleWithoutPromotingAnyAccount()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();

        var role = await fixture.Context.Roles.SingleAsync(
            item => item.Id == CodeLogicRoles.AdministratorRoleId);

        Assert.Equal(CodeLogicRoles.Administrator, role.Name);
        Assert.Empty(await fixture.Context.UserRoles.ToArrayAsync());
    }

    [Fact]
    public async Task DraftIsPrivateThenPublishedLessonBecomesPublicWithVersionedAuditFields()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var initialTime = new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero);
        var time = new MutableTimeProvider(initialTime);
        var service = CreateService(fixture.Context, time);

        var created = await service.CreateDraftAsync(ValidInput(), fixture.Owner.Id);

        Assert.True(created.IsSuccess);
        var draft = created.Lesson!;
        Assert.Equal(AuthoredLessonStatus.Draft, draft.Status);
        Assert.Equal(1, draft.Version);
        Assert.Equal(initialTime, draft.CreatedAtUtc);
        Assert.Equal(initialTime, draft.UpdatedAtUtc);
        Assert.Null(draft.PublishedAtUtc);
        Assert.Null(await service.GetPublishedByIdAsync(draft.Id));
        Assert.Empty(await service.GetPublishedAsync());

        time.Advance(TimeSpan.FromMinutes(5));
        var updatedInput = ValidInput() with { Title = "Updated branching lesson" };
        var updated = await service.UpdateDraftAsync(
            draft.Id,
            updatedInput,
            fixture.Owner.Id,
            expectedVersion: 1);

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Lesson!.Version);
        Assert.Equal(initialTime, updated.Lesson.CreatedAtUtc);
        Assert.Equal(initialTime.AddMinutes(5), updated.Lesson.UpdatedAtUtc);

        time.Advance(TimeSpan.FromMinutes(5));
        var published = await service.PublishAsync(
            draft.Id,
            fixture.Owner.Id,
            expectedVersion: 2);

        Assert.True(published.IsSuccess);
        Assert.Equal(AuthoredLessonStatus.Published, published.Lesson!.Status);
        Assert.Equal(3, published.Lesson.Version);
        Assert.Equal(initialTime.AddMinutes(10), published.Lesson.PublishedAtUtc);
        var publicLesson = await service.GetPublishedByIdAsync(draft.Id);
        Assert.NotNull(publicLesson);
        Assert.Equal("Updated branching lesson", publicLesson.Title);
        Assert.Single(await service.GetPublishedAsync());
    }

    [Fact]
    public async Task ValidationRejectsBlankAndOversizedContentWithoutSavingDraft()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var service = CreateService(fixture.Context);
        var input = new LessonAuthoringInput(
            Title: " ",
            Objective: "Learn safely.",
            Content: new string('x', LessonAuthoringValidator.MaximumContentLength + 1),
            SampleCode: "int value = 1;");

        var result = await service.CreateDraftAsync(input, fixture.Owner.Id);

        Assert.Equal(LessonAuthoringResultStatus.ValidationFailed, result.Status);
        Assert.Contains("Title is required.", result.Errors);
        Assert.Contains(result.Errors, error => error.Contains("10,000", StringComparison.Ordinal));
        Assert.Empty(await fixture.Context.AuthoredLessons.ToArrayAsync());
    }

    [Fact]
    public async Task SampleCodeUsesTheSharedTenThousandCharacterBoundary()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var service = CreateService(fixture.Context);

        var accepted = await service.CreateDraftAsync(
            ValidInput() with { SampleCode = "int value = 1;".PadRight(10_000) },
            fixture.Owner.Id);
        var rejected = await service.CreateDraftAsync(
            ValidInput() with { SampleCode = "int value = 1;".PadRight(10_001) },
            fixture.Owner.Id);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(LessonAuthoringResultStatus.ValidationFailed, rejected.Status);
        Assert.Contains(rejected.Errors, error => error.Contains("10,000", StringComparison.Ordinal));
        Assert.Single(await fixture.Context.AuthoredLessons.ToArrayAsync());
    }

    [Fact]
    public async Task NonOwnerCannotUpdateOrPublishDraft()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var service = CreateService(fixture.Context);
        var created = await service.CreateDraftAsync(ValidInput(), fixture.Owner.Id);

        var update = await service.UpdateDraftAsync(
            created.Lesson!.Id,
            ValidInput() with { Title = "Taken over" },
            fixture.OtherUser.Id,
            expectedVersion: 1);
        var publish = await service.PublishAsync(
            created.Lesson.Id,
            fixture.OtherUser.Id,
            expectedVersion: 1);

        Assert.Equal(LessonAuthoringResultStatus.Forbidden, update.Status);
        Assert.Equal(LessonAuthoringResultStatus.Forbidden, publish.Status);
        var unchanged = await service.GetOwnedByIdAsync(created.Lesson.Id, fixture.Owner.Id);
        Assert.Equal("Understand if and else", unchanged!.Title);
        Assert.Equal(AuthoredLessonStatus.Draft, unchanged.Status);
        Assert.Equal(1, unchanged.Version);
    }

    [Fact]
    public async Task StaleVersionCannotOverwriteNewerDraft()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var service = CreateService(fixture.Context);
        var created = await service.CreateDraftAsync(ValidInput(), fixture.Owner.Id);
        var firstUpdate = await service.UpdateDraftAsync(
            created.Lesson!.Id,
            ValidInput() with { Title = "Version two" },
            fixture.Owner.Id,
            expectedVersion: 1);

        var staleUpdate = await service.UpdateDraftAsync(
            created.Lesson.Id,
            ValidInput() with { Title = "Stale overwrite" },
            fixture.Owner.Id,
            expectedVersion: 1);

        Assert.True(firstUpdate.IsSuccess);
        Assert.Equal(LessonAuthoringResultStatus.Conflict, staleUpdate.Status);
        Assert.Contains("Reload", Assert.Single(staleUpdate.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationStopsDraftPersistence()
    {
        await using var fixture = await AuthoringFixture.CreateAsync();
        var service = CreateService(fixture.Context);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateDraftAsync(
                ValidInput(),
                fixture.Owner.Id,
                cancellationSource.Token));

        Assert.Empty(await fixture.Context.AuthoredLessons.AsNoTracking().ToArrayAsync());
    }

    private static LessonAuthoringInput ValidInput() => new(
        "Understand if and else",
        "Choose one branch from a Boolean condition.",
        "A condition selects exactly one of two paths. Test values at the boundary.",
        "int score = 8;\nConsole.WriteLine(score >= 5);");

    private static EfLessonAuthoringService CreateService(
        CodeLogicDbContext context,
        TimeProvider? timeProvider = null) =>
        new(
            context,
            timeProvider ?? TimeProvider.System,
            NullLogger<EfLessonAuthoringService>.Instance);

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current += duration;
    }

    private sealed class AuthoringFixture : IAsyncDisposable
    {
        private readonly SqliteTestDatabase database;

        private AuthoringFixture(
            SqliteTestDatabase database,
            CodeLogicUser owner,
            CodeLogicUser otherUser)
        {
            this.database = database;
            Context = database.Context;
            Owner = owner;
            OtherUser = otherUser;
        }

        public CodeLogicDbContext Context { get; }

        public CodeLogicUser Owner { get; }

        public CodeLogicUser OtherUser { get; }

        public static async Task<AuthoringFixture> CreateAsync()
        {
            var database = await SqliteTestDatabase.CreateMigratedAsync();
            var context = database.Context;
            var owner = User("owner");
            var other = User("other");
            context.Users.AddRange(owner, other);
            await context.SaveChangesAsync();
            return new AuthoringFixture(database, owner, other);
        }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }

        private static CodeLogicUser User(string prefix) => new()
        {
            Id = $"{prefix}-user-id",
            UserName = $"{prefix}@example.test",
            NormalizedUserName = $"{prefix.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"{prefix}@example.test",
            NormalizedEmail = $"{prefix.ToUpperInvariant()}@EXAMPLE.TEST",
            SecurityStamp = $"{prefix}-security-stamp",
        };
    }
}
