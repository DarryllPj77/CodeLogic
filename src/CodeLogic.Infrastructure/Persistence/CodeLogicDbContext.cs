using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using CodeLogic.Application.Analysis;
using CodeLogic.Infrastructure.Identity;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class CodeLogicDbContext(DbContextOptions<CodeLogicDbContext> options)
    : IdentityDbContext<CodeLogicUser>(options)
{
    public DbSet<CurriculumTopicEntity> CurriculumTopics => Set<CurriculumTopicEntity>();

    public DbSet<CurriculumLessonEntity> CurriculumLessons => Set<CurriculumLessonEntity>();

    public DbSet<AuthoredLessonEntity> AuthoredLessons => Set<AuthoredLessonEntity>();

    public DbSet<UserExerciseAttemptEntity> UserExerciseAttempts => Set<UserExerciseAttemptEntity>();

    public DbSet<UserLessonProgressEntity> UserLessonProgress => Set<UserLessonProgressEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        ConfigureIdentityRoles(builder);
        ConfigureCurriculumTopic(builder);
        ConfigureCurriculumLesson(builder);
        ConfigureAuthoredLesson(builder);
        ConfigureUserExerciseAttempt(builder);
        ConfigureUserLessonProgress(builder);
    }

    private static void ConfigureIdentityRoles(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<IdentityRole>().HasData(new IdentityRole
        {
            Id = CodeLogicRoles.AdministratorRoleId,
            Name = CodeLogicRoles.Administrator,
            NormalizedName = CodeLogicRoles.Administrator.ToUpperInvariant(),
            ConcurrencyStamp = "codelogic-administrator-role-v1",
        });

    private static void ConfigureCurriculumTopic(ModelBuilder modelBuilder)
    {
        var topic = modelBuilder.Entity<CurriculumTopicEntity>();
        topic.ToTable("CurriculumTopics");
        topic.HasKey(entity => entity.Id);
        topic.Property(entity => entity.Order).IsRequired();
        topic.Property(entity => entity.Slug).HasMaxLength(100).IsRequired();
        topic.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        topic.Property(entity => entity.Description).HasMaxLength(1_000).IsRequired();
        topic.HasIndex(entity => entity.Order).IsUnique();
        topic.HasIndex(entity => entity.Slug).IsUnique();
        topic.HasData(CurriculumPersistenceSeed.CreateTopics());
    }

    private static void ConfigureCurriculumLesson(ModelBuilder modelBuilder)
    {
        var lesson = modelBuilder.Entity<CurriculumLessonEntity>();
        lesson.ToTable("CurriculumLessons");
        lesson.HasKey(entity => entity.Id);
        lesson.Property(entity => entity.Order).IsRequired();
        lesson.Property(entity => entity.Status).IsRequired();
        lesson.Property(entity => entity.Difficulty).IsRequired();
        lesson.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        lesson.Property(entity => entity.Objective).HasMaxLength(1_000).IsRequired();
        lesson.Property(entity => entity.SectionsJson).IsRequired();
        lesson.Property(entity => entity.ExerciseTitle).HasMaxLength(200).IsRequired();
        lesson.Property(entity => entity.ExercisePrompt).HasMaxLength(2_000).IsRequired();
        lesson.Property(entity => entity.ExerciseKind).IsRequired();
        lesson.Property(entity => entity.SampleCode).HasMaxLength(CodeAnalysisDefaults.MaximumCharacters).IsRequired();
        lesson.Property(entity => entity.AllowedConceptsJson).IsRequired();
        lesson.Property(entity => entity.QuizResponseKind).IsRequired();
        lesson.Property(entity => entity.QuizAssessmentGoal).HasMaxLength(1_000).IsRequired();
        lesson.Property(entity => entity.QuizExpectedAnswer).HasMaxLength(500).IsRequired();
        lesson.Property(entity => entity.QuizNormalization).IsRequired();
        lesson.Property(entity => entity.QuizExplanation).HasMaxLength(2_000).IsRequired();
        lesson.Property(entity => entity.QuizAnswerOptionsJson).IsRequired();
        lesson.HasIndex(entity => entity.Order).IsUnique();
        lesson.HasIndex(entity => entity.ExerciseId).IsUnique();
        lesson.HasOne(entity => entity.Topic)
            .WithMany(entity => entity.Lessons)
            .HasForeignKey(entity => entity.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
        lesson.HasData(CurriculumPersistenceSeed.CreateLessons());
    }

    private static void ConfigureUserExerciseAttempt(ModelBuilder modelBuilder)
    {
        var attempt = modelBuilder.Entity<UserExerciseAttemptEntity>();
        attempt.ToTable("UserExerciseAttempts");
        attempt.HasKey(entity => entity.Id);
        attempt.Property(entity => entity.UserId).HasMaxLength(450).IsRequired();
        attempt.Property(entity => entity.AttemptNumber).IsRequired();
        attempt.Property(entity => entity.Outcome).IsRequired();
        attempt.Property(entity => entity.AttemptedAtUtc).IsRequired();
        attempt.HasIndex(entity => new { entity.UserId, entity.ExerciseId, entity.AttemptNumber })
            .IsUnique();
        attempt.HasOne<CodeLogicUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        attempt.HasOne<CurriculumLessonEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.LessonId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAuthoredLesson(ModelBuilder modelBuilder)
    {
        var lesson = modelBuilder.Entity<AuthoredLessonEntity>();
        lesson.ToTable("AuthoredLessons");
        lesson.HasKey(entity => entity.Id);
        lesson.Property(entity => entity.Status).IsRequired();
        lesson.Property(entity => entity.Version).IsRequired().IsConcurrencyToken();
        lesson.Property(entity => entity.OwnerUserId).HasMaxLength(450).IsRequired();
        lesson.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        lesson.Property(entity => entity.Objective).HasMaxLength(1_000).IsRequired();
        lesson.Property(entity => entity.Content).HasMaxLength(10_000).IsRequired();
        lesson.Property(entity => entity.SampleCode).HasMaxLength(CodeAnalysisDefaults.MaximumCharacters).IsRequired();
        lesson.Property(entity => entity.CreatedAtUtc).IsRequired();
        lesson.Property(entity => entity.UpdatedAtUtc).IsRequired();
        lesson.HasIndex(entity => new { entity.Status, entity.PublishedAtUtc });
        lesson.HasIndex(entity => new { entity.OwnerUserId, entity.UpdatedAtUtc });
        lesson.HasOne<CodeLogicUser>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureUserLessonProgress(ModelBuilder modelBuilder)
    {
        var progress = modelBuilder.Entity<UserLessonProgressEntity>();
        progress.ToTable("UserLessonProgress");
        progress.HasKey(entity => new { entity.UserId, entity.LessonId });
        progress.Property(entity => entity.UserId).HasMaxLength(450).IsRequired();
        progress.Property(entity => entity.CompletedAtUtc).IsRequired();
        progress.HasOne<CodeLogicUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        progress.HasOne<CurriculumLessonEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.LessonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
