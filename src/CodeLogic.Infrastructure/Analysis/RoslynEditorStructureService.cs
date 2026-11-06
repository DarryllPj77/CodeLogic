using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeLogic.Infrastructure.Analysis;

public sealed class RoslynEditorStructureService : IEditorStructureService
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    public async Task<IReadOnlyList<EditorFoldRegion>> GetFoldRegionsAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > CodeAnalysisDefaults.MaximumCharacters)
        {
            return [];
        }

        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);
        var regions = new Dictionary<(int Start, int End), EditorFoldRegion>();

        foreach (var node in root.DescendantNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var braces = Braces(node);
            if (braces is null)
            {
                continue;
            }

            var start = braces.Value.Open.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var end = braces.Value.Close.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (end <= start)
            {
                continue;
            }

            regions.TryAdd((start, end), new EditorFoldRegion(start, end, Kind(node)));
        }

        return EditorFoldRegions.ReadOnly(regions.Values
            .OrderBy(region => region.StartLine)
            .ThenByDescending(region => region.EndLine));
    }

    private static (SyntaxToken Open, SyntaxToken Close)? Braces(SyntaxNode node) => node switch
    {
        NamespaceDeclarationSyntax item => (item.OpenBraceToken, item.CloseBraceToken),
        TypeDeclarationSyntax item => (item.OpenBraceToken, item.CloseBraceToken),
        SwitchStatementSyntax item => (item.OpenBraceToken, item.CloseBraceToken),
        BlockSyntax item => (item.OpenBraceToken, item.CloseBraceToken),
        _ => ((SyntaxToken Open, SyntaxToken Close)?)null,
    };

    private static string Kind(SyntaxNode node) => node switch
    {
        BaseNamespaceDeclarationSyntax => "namespace",
        TypeDeclarationSyntax => "class",
        SwitchStatementSyntax => "switch",
        BlockSyntax { Parent: MethodDeclarationSyntax or LocalFunctionStatementSyntax } => "method",
        BlockSyntax { Parent: IfStatementSyntax } => "if",
        BlockSyntax { Parent: ElseClauseSyntax } => "else",
        BlockSyntax { Parent: ForStatementSyntax } => "for",
        BlockSyntax { Parent: ForEachStatementSyntax } => "foreach",
        BlockSyntax { Parent: WhileStatementSyntax } => "while",
        BlockSyntax { Parent: DoStatementSyntax } => "do",
        BlockSyntax { Parent: TryStatementSyntax } => "try",
        BlockSyntax { Parent: CatchClauseSyntax } => "catch",
        BlockSyntax { Parent: FinallyClauseSyntax } => "finally",
        _ => "block",
    };
}
