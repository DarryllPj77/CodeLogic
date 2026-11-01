using CodeLogic.Application.Analysis;
using CodeLogic.Application.Flowcharts;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeLogic.Infrastructure.Flowcharts;

public sealed class RoslynFlowchartTranslator(ICodeAnalysisService analysisService)
    : IFlowchartTranslator
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    public async Task<FlowchartTranslationResult> TranslateAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        var analysis = await analysisService.AnalyzeAsync(source, cancellationToken);
        if (!analysis.IsValid)
        {
            return new FlowchartTranslationResult(
                MapStatus(analysis.Status),
                diagnostics: analysis.Diagnostics);
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source!,
            ParseOptions,
            cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);
        var statements = root.DescendantNodes()
            .OfType<GlobalStatementSyntax>()
            .Select(global => global.Statement)
            .ToArray();

        var translationDiagnostic = FindUnsupportedTranslationSyntax(statements);
        if (translationDiagnostic is not null)
        {
            return new FlowchartTranslationResult(
                FlowchartTranslationStatus.Unsupported,
                diagnostics: [translationDiagnostic]);
        }

        var builder = new GraphBuilder();
        return new FlowchartTranslationResult(
            FlowchartTranslationStatus.Success,
            builder.Build(statements));
    }

    private static CodeDiagnostic? FindUnsupportedTranslationSyntax(
        IReadOnlyList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            if (statement is IfStatementSyntax branch)
            {
                var nestedBranch = branch.Statement.DescendantNodesAndSelf()
                    .OfType<IfStatementSyntax>()
                    .FirstOrDefault();
                nestedBranch ??= branch.Else?.Statement.DescendantNodesAndSelf()
                    .OfType<IfStatementSyntax>()
                    .FirstOrDefault();
                if (nestedBranch is not null)
                {
                    return Unsupported(
                        nestedBranch,
                        "FL1001",
                        "Nested if/else branches are not supported by flowchart schema 1.0.",
                        "Keep one decision level for now. Nested branching will be introduced in a later task.");
                }

                var unsupportedBranchStatement = BranchStatements(branch)
                    .FirstOrDefault(candidate => !IsSimpleStatement(candidate));
                if (unsupportedBranchStatement is not null)
                {
                    return UnsupportedStatement(unsupportedBranchStatement);
                }

                continue;
            }

            if (!IsSimpleStatement(statement))
            {
                return UnsupportedStatement(statement);
            }
        }

        return null;
    }

    private static IEnumerable<StatementSyntax> BranchStatements(IfStatementSyntax branch)
    {
        foreach (var statement in Unwrap(branch.Statement))
        {
            yield return statement;
        }

        if (branch.Else is not null)
        {
            foreach (var statement in Unwrap(branch.Else.Statement))
            {
                yield return statement;
            }
        }
    }

    private static StatementSyntax[] Unwrap(StatementSyntax statement) =>
        statement is BlockSyntax block ? [.. block.Statements] : [statement];

    private static bool IsSimpleStatement(StatementSyntax statement) => statement is
        LocalDeclarationStatementSyntax
        or ExpressionStatementSyntax
        or EmptyStatementSyntax;

    private static CodeDiagnostic UnsupportedStatement(StatementSyntax statement) => Unsupported(
        statement,
        "FL1002",
        $"{statement.Kind()} is not supported by flowchart schema 1.0.",
        "This task translates declarations, expressions, input/output calls, and one-level if/else branches only.");

    private static CodeDiagnostic Unsupported(
        SyntaxNode node,
        string code,
        string message,
        string explanation)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return new CodeDiagnostic(
            code,
            CodeDiagnosticCategory.Unsupported,
            CodeDiagnosticSeverity.Error,
            message,
            explanation,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            node.Span.Length);
    }

    private static FlowchartTranslationStatus MapStatus(CodeAnalysisStatus status) => status switch
    {
        CodeAnalysisStatus.InvalidInput => FlowchartTranslationStatus.InvalidInput,
        CodeAnalysisStatus.SyntaxError => FlowchartTranslationStatus.SyntaxError,
        CodeAnalysisStatus.Unsupported => FlowchartTranslationStatus.Unsupported,
        CodeAnalysisStatus.TimedOut => FlowchartTranslationStatus.TimedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unexpected analysis status."),
    };

    private sealed class GraphBuilder
    {
        private readonly List<FlowNode> nodes = [];
        private readonly List<FlowEdge> edges = [];

        public FlowGraph Build(IReadOnlyList<StatementSyntax> statements)
        {
            var start = AddNode(FlowNodeType.Start, "Start", source: null);
            IReadOnlyList<PendingEdge> exits = [new(start.Id, FlowEdgeType.Next)];

            foreach (var statement in statements)
            {
                exits = AddStatement(statement, exits);
            }

            var end = AddNode(FlowNodeType.End, "End", source: null);
            Connect(exits, end.Id);
            return new FlowGraph(nodes, edges);
        }

        private IReadOnlyList<PendingEdge> AddStatement(
            StatementSyntax statement,
            IReadOnlyList<PendingEdge> incoming)
        {
            if (statement is EmptyStatementSyntax)
            {
                return incoming;
            }

            if (statement is IfStatementSyntax branch)
            {
                return AddBranch(branch, incoming);
            }

            var node = AddNode(Classify(statement), Text(statement), statement);
            Connect(incoming, node.Id);
            return [new PendingEdge(node.Id, FlowEdgeType.Next)];
        }

        private IReadOnlyList<PendingEdge> AddBranch(
            IfStatementSyntax branch,
            IReadOnlyList<PendingEdge> incoming)
        {
            var decision = AddNode(
                FlowNodeType.Decision,
                branch.Condition.NormalizeWhitespace().ToFullString(),
                branch.Condition);
            Connect(incoming, decision.Id);

            IReadOnlyList<PendingEdge> trueExits =
                [new PendingEdge(decision.Id, FlowEdgeType.True)];
            foreach (var statement in Unwrap(branch.Statement))
            {
                trueExits = AddStatement(statement, trueExits);
            }

            IReadOnlyList<PendingEdge> falseExits =
                [new PendingEdge(decision.Id, FlowEdgeType.False)];
            if (branch.Else is not null)
            {
                foreach (var statement in Unwrap(branch.Else.Statement))
                {
                    falseExits = AddStatement(statement, falseExits);
                }
            }

            return [.. trueExits, .. falseExits];
        }

        private FlowNode AddNode(FlowNodeType type, string label, SyntaxNode? source)
        {
            var node = new FlowNode(
                $"node-{nodes.Count:D3}",
                type,
                label,
                source is null ? null : ToSourceSpan(source));
            nodes.Add(node);
            return node;
        }

        private void Connect(IEnumerable<PendingEdge> incoming, string toNodeId)
        {
            foreach (var pending in incoming)
            {
                edges.Add(new FlowEdge(
                    $"edge-{edges.Count:D3}",
                    pending.FromNodeId,
                    toNodeId,
                    pending.Type,
                    pending.Type switch
                    {
                        FlowEdgeType.True => "True",
                        FlowEdgeType.False => "False",
                        _ => null,
                    }));
            }
        }

        private static FlowNodeType Classify(StatementSyntax statement)
        {
            if (statement.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => IsConsoleCall(invocation, "ReadLine")))
            {
                return FlowNodeType.Input;
            }

            if (statement is ExpressionStatementSyntax expressionStatement
                && expressionStatement.Expression is InvocationExpressionSyntax invocation
                && IsConsoleCall(invocation, "WriteLine"))
            {
                return FlowNodeType.Output;
            }

            return FlowNodeType.Action;
        }

        private static bool IsConsoleCall(
            InvocationExpressionSyntax invocation,
            string methodName) =>
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "Console" },
                Name.Identifier.ValueText: var name,
            }
            && name == methodName;

        private static string Text(SyntaxNode node) =>
            node.NormalizeWhitespace().ToFullString();

        private static SourceSpan ToSourceSpan(SyntaxNode node)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            return new SourceSpan(
                node.SpanStart,
                node.Span.Length,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1,
                lineSpan.EndLinePosition.Line + 1,
                lineSpan.EndLinePosition.Character + 1);
        }

        private sealed record PendingEdge(string FromNodeId, FlowEdgeType Type);
    }
}
