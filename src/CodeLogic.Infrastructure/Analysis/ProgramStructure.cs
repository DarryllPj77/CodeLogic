using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeLogic.Infrastructure.Analysis;

/// <summary>
/// Reads the shape of a learner's program: classic (class with a static Main) or top-level
/// statements. The analyzer, flowchart translator, and trace interpreter all use this one reader, so
/// they agree on where the program starts and which static methods exist.
/// </summary>
internal sealed class ProgramStructure
{
    private ProgramStructure(
        ProgramForm form,
        IReadOnlyList<StatementSyntax> entryStatements,
        MethodDeclarationSyntax? main,
        IReadOnlyList<MethodDeclarationSyntax> mainCandidates,
        ClassDeclarationSyntax? programClass,
        IReadOnlyDictionary<string, MethodDeclarationSyntax> methods)
    {
        Form = form;
        EntryStatements = entryStatements;
        Main = main;
        MainCandidates = mainCandidates;
        ProgramClass = programClass;
        Methods = methods;
    }

    public ProgramForm Form { get; }

    /// <summary>The statements the program starts with: Main's body, or the top-level statements.</summary>
    public IReadOnlyList<StatementSyntax> EntryStatements { get; }

    /// <summary>The single entry point, when exactly one Main exists.</summary>
    public MethodDeclarationSyntax? Main { get; }

    public IReadOnlyList<MethodDeclarationSyntax> MainCandidates { get; }

    public ClassDeclarationSyntax? ProgramClass { get; }

    /// <summary>Static methods declared next to Main (excluding Main), by name.</summary>
    public IReadOnlyDictionary<string, MethodDeclarationSyntax> Methods { get; }

    public static ProgramStructure Read(SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var globals = root.ChildNodes()
            .OfType<GlobalStatementSyntax>()
            .Select(global => global.Statement)
            .ToArray();
        var mains = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "Main")
            .ToArray();
        var hasTypeShell = root.DescendantNodes().Any(node =>
            node is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax);

        var form = (globals.Length, mains.Length, hasTypeShell) switch
        {
            ( > 0, > 0, _) => ProgramForm.Mixed,
            ( > 0, 0, _) => ProgramForm.TopLevel,
            (0, _, true) => ProgramForm.Classic,
            _ => ProgramForm.Unknown,
        };

        var main = form == ProgramForm.Classic && mains.Length == 1 ? mains[0] : null;
        var programClass = main?.Parent as ClassDeclarationSyntax;
        var methods = programClass is null
            ? new Dictionary<string, MethodDeclarationSyntax>(StringComparer.Ordinal)
            : programClass.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method != main && method.Modifiers.Any(SyntaxKind.StaticKeyword))
                .GroupBy(method => method.Identifier.ValueText, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        IReadOnlyList<StatementSyntax> entry = form switch
        {
            ProgramForm.TopLevel => globals,
            ProgramForm.Classic when main?.Body is not null => [.. main.Body.Statements],
            _ => [],
        };

        return new ProgramStructure(form, entry, main, mains, programClass, methods);
    }

    /// <summary>Statements of a method body (block-bodied methods only).</summary>
    public static IReadOnlyList<StatementSyntax> BodyOf(MethodDeclarationSyntax method) =>
        method.Body is null ? [] : [.. method.Body.Statements];

    /// <summary>
    /// A switch section's statements without its closing break: that break is simply the way out of
    /// the switch, so the flowchart does not draw it and the trace does not record it.
    /// </summary>
    public static IReadOnlyList<StatementSyntax> SwitchSectionBody(SwitchSectionSyntax section) =>
        section.Statements.LastOrDefault() is BreakStatementSyntax
            ? [.. section.Statements.Take(section.Statements.Count - 1)]
            : [.. section.Statements];

    /// <summary>The name of a call to one of the program's own methods, or null for any other call.</summary>
    public string? CalledMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression is IdentifierNameSyntax identifier
        && Methods.ContainsKey(identifier.Identifier.ValueText)
            ? identifier.Identifier.ValueText
            : null;

    /// <summary>A readable signature such as "Area(int width, int height)".</summary>
    public static string Signature(MethodDeclarationSyntax method) =>
        $"{method.Identifier.ValueText}({string.Join(", ", method.ParameterList.Parameters.Select(parameter => $"{parameter.Type} {parameter.Identifier.ValueText}"))})";
}
