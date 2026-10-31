using Microsoft.EntityFrameworkCore;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class CodeLogicDbContext(DbContextOptions<CodeLogicDbContext> options)
    : DbContext(options)
{
    public DbSet<CurriculumTopicEntity> CurriculumTopics => Set<CurriculumTopicEntity>();

    public DbSet<CurriculumLessonEntity> CurriculumLessons => Set<CurriculumLessonEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureCurriculumTopic(modelBuilder);
        ConfigureCurriculumLesson(modelBuilder);
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
}
