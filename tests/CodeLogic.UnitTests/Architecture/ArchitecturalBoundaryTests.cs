using System.Reflection;
using System.Xml.Linq;

using CodeLogic.Application.Analysis;
using CodeLogic.Application.Authoring;
using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Feedback;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Application.Progress;

namespace CodeLogic.UnitTests.Architecture;

public sealed class ArchitecturalBoundaryTests
{
    private static readonly string[] ForbiddenOuterAssemblyPrefixes =
    [
        "CodeLogic.Infrastructure",
        "CodeLogic.Web",
        "Microsoft.AspNetCore",
        "Microsoft.CodeAnalysis",
        "Microsoft.EntityFrameworkCore",
    ];

    [Fact]
    public void ProjectReferencesFollowDeclaredDependencyGraph()
    {
        var root = FindRepositoryRoot();
        var domain = Path.Combine(root, "src", "CodeLogic.Domain", "CodeLogic.Domain.csproj");
        var application = Path.Combine(root, "src", "CodeLogic.Application", "CodeLogic.Application.csproj");
        var infrastructure = Path.Combine(root, "src", "CodeLogic.Infrastructure", "CodeLogic.Infrastructure.csproj");
        var web = Path.Combine(root, "src", "CodeLogic.Web", "CodeLogic.Web.csproj");

        AssertProjectReferences(domain);
        AssertProjectReferences(application, domain);
        AssertProjectReferences(infrastructure, application, domain);
        AssertProjectReferences(web, application, infrastructure);
    }

    [Fact]
    public void ApplicationAssemblyHasNoOuterLayerOrFrameworkDependencies()
    {
        var referencedAssemblies = typeof(ICurriculumCatalog).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referencedAssemblies,
            assembly => ForbiddenOuterAssemblyPrefixes.Any(
                prefix => assembly.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }

    [Fact]
    public void ApplicationPortSignaturesRemainFrameworkNeutral()
    {
        Type[] applicationPorts =
        [
            typeof(ICodeAnalysisService),
            typeof(ILessonAuthoringService),
            typeof(ICurriculumCatalog),
            typeof(IPedagogicalInterpreter),
            typeof(ILogicalFeedbackService),
            typeof(IFlowchartTranslator),
            typeof(IMermaidFlowchartSerializer),
            typeof(IUserLearningProgressStore),
        ];

        var exposedTypes = applicationPorts
            .SelectMany(port => port.GetMethods())
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .SelectMany(ExpandTypeGraph)
            .Distinct()
            .ToArray();

        Assert.DoesNotContain(
            exposedTypes,
            type => ForbiddenOuterAssemblyPrefixes.Any(
                prefix => type.Assembly.GetName().Name?.StartsWith(
                    prefix,
                    StringComparison.Ordinal) == true));
    }

    private static IEnumerable<Type> ExpandTypeGraph(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nestedType in ExpandTypeGraph(elementType))
            {
                yield return nestedType;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nestedType in ExpandTypeGraph(argument))
            {
                yield return nestedType;
            }
        }
    }

    private static void AssertProjectReferences(string projectPath, params string[] expectedReferences)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");
        var project = XDocument.Load(projectPath);
        var actualReferences = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedExpected = expectedReferences
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(normalizedExpected, actualReferences, StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CodeLogic.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the CodeLogic repository root.");
    }
}
