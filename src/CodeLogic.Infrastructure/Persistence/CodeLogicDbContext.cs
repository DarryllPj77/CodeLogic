using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using CodeLogic.Infrastructure.Identity;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class CodeLogicDbContext(DbContextOptions<CodeLogicDbContext> options)
    : IdentityDbContext<CodeLogicUser>(options)
{
    public DbSet<CurriculumTopicEntity> CurriculumTopics => Set<CurriculumTopicEntity>();

    public DbSet<CurriculumLessonEntity> CurriculumLessons => Set<CurriculumLessonEntity>();

    public DbSet<UserExerciseAttemptEntity> UserExerciseAttempts => Set<UserExerciseAttemptEntity>();

    public DbSet<UserLessonProgressEntity> UserLessonProgress => Set<UserLessonProgressEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        ConfigureCurriculumTopic(builder);
        ConfigureCurriculumLesson(builder);
        ConfigureUserExerciseAttempt(builder);
        ConfigureUserLessonProgress(builder);
    }

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
        lesson.Property(entity => entity.SampleCode).HasMaxLength(1_200).IsRequired();
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
