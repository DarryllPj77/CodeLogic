using CodeLogic.Domain;
using CodeLogic.Domain.Curriculum;

namespace CodeLogic.UnitTests.Architecture;

public sealed class DomainDependencyTests
{
    [Fact]
    public void DomainAssemblyHasNoForbiddenFrameworkDependencies()
    {
        var forbiddenPrefixes = new[]
        {
            "CodeLogic.Application",
            "CodeLogic.Infrastructure",
            "CodeLogic.Web",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.CodeAnalysis",
        };

        var referencedAssemblies = typeof(Lesson).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referencedAssemblies,
            assembly => forbiddenPrefixes.Any(
                prefix => assembly.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }

    [Fact]
    public void StrongIdentifiersRejectEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => new TopicId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new LessonId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new LessonSectionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ExerciseId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ExerciseAttemptId(Guid.Empty));
    }
}
