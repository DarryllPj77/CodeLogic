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
        IEnumerable<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            CodeDiagnostic? diagnostic = statement switch
            {
                LocalDeclarationStatementSyntax or ExpressionStatementSyntax or EmptyStatementSyntax => null,
                IfStatementSyntax branch => FindUnsupportedInBranch(branch),
                WhileStatementSyntax loop => FindUnsupportedTranslationSyntax(Unwrap(loop.Statement)),
                ForStatementSyntax loop => FindUnsupportedTranslationSyntax(Unwrap(loop.Statement)),
                DoStatementSyntax loop => FindUnsupportedTranslationSyntax(Unwrap(loop.Statement)),
                _ => UnsupportedStatement(statement),
            };

            if (diagnostic is not null)
            {
                return diagnostic;
            }
        }

        return null;
    }

    private static CodeDiagnostic? FindUnsupportedInBranch(IfStatementSyntax branch)
    {
        var diagnostic = FindUnsupportedTranslationSyntax(Unwrap(branch.Statement));
        return diagnostic ?? (branch.Else is null
            ? null
            : FindUnsupportedTranslationSyntax(Unwrap(branch.Else.Statement)));
    }

    private static StatementSyntax[] Unwrap(StatementSyntax statement) =>
        statement is BlockSyntax block ? [.. block.Statements] : [statement];

    private static CodeDiagnostic UnsupportedStatement(StatementSyntax statement) => Unsupported(
        statement,
        "FL1002",
        $"{statement.Kind()} is not supported by flowchart schema 1.1.",
        "This graph supports declarations, expressions, nested if/else, while, for, and do/while. Break, continue, return, and local functions remain unsupported.");

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
            var exits = AddStatements(statements, [new(start.Id, FlowEdgeType.Next)]);
            var end = AddNode(FlowNodeType.End, "End", source: null);
            Connect(exits, end.Id);
            return new FlowGraph(nodes, edges);
        }

        private IReadOnlyList<PendingEdge> AddStatements(
            IEnumerable<StatementSyntax> statements,
            IReadOnlyList<PendingEdge> incoming)
        {
            var exits = incoming;
            foreach (var statement in statements)
            {
                exits = AddStatement(statement, exits);
            }

            return exits;
        }

        private IReadOnlyList<PendingEdge> AddStatement(
            StatementSyntax statement,
            IReadOnlyList<PendingEdge> incoming) => statement switch
            {
                EmptyStatementSyntax => incoming,
                IfStatementSyntax branch => AddBranch(branch, incoming),
                WhileStatementSyntax loop => AddWhile(loop, incoming),
                ForStatementSyntax loop => AddFor(loop, incoming),
                DoStatementSyntax loop => AddDoWhile(loop, incoming),
                _ => AddSimpleStatement(statement, incoming),
            };

        private IReadOnlyList<PendingEdge> AddSimpleStatement(
            StatementSyntax statement,
            IReadOnlyList<PendingEdge> incoming)
        {
            var node = AddNode(Classify(statement), Text(statement), statement);
            Connect(incoming, node.Id);
            return [new PendingEdge(node.Id, FlowEdgeType.Next)];
        }

        private IReadOnlyList<PendingEdge> AddBranch(
            IfStatementSyntax branch,
            IReadOnlyList<PendingEdge> incoming)
        {
            var decision = AddNode(FlowNodeType.Decision, Text(branch.Condition), branch.Condition);
            Connect(incoming, decision.Id);

            var trueExits = AddStatements(
                Unwrap(branch.Statement),
                [new PendingEdge(decision.Id, FlowEdgeType.True)]);
            var falseExits = branch.Else is null
                ? [new PendingEdge(decision.Id, FlowEdgeType.False)]
                : AddStatements(
                    Unwrap(branch.Else.Statement),
                    [new PendingEdge(decision.Id, FlowEdgeType.False)]);

            return [.. trueExits, .. falseExits];
        }

        private IReadOnlyList<PendingEdge> AddWhile(
            WhileStatementSyntax loop,
            IReadOnlyList<PendingEdge> incoming)
        {
            var decision = AddNode(FlowNodeType.Decision, Text(loop.Condition), loop.Condition);
            Connect(incoming, decision.Id);
            var bodyExits = AddStatements(
                Unwrap(loop.Statement),
                [new PendingEdge(decision.Id, FlowEdgeType.True)]);
            ConnectLoopBack(bodyExits, decision.Id);
            return [new PendingEdge(decision.Id, FlowEdgeType.False)];
        }

        private IReadOnlyList<PendingEdge> AddFor(
            ForStatementSyntax loop,
            IReadOnlyList<PendingEdge> incoming)
        {
            var exits = incoming;
            if (loop.Declaration is not null)
            {
                exits = AddAction($"for init: {Text(loop.Declaration)}", loop.Declaration, exits);
            }

            foreach (var initializer in loop.Initializers)
            {
                exits = AddAction($"for init: {Text(initializer)}", initializer, exits);
            }

            var condition = AddNode(
                FlowNodeType.Decision,
                loop.Condition is null ? "true (implicit for condition)" : Text(loop.Condition),
                (SyntaxNode?)loop.Condition ?? loop);
            Connect(exits, condition.Id);

            var bodyExits = AddStatements(
                Unwrap(loop.Statement),
                [new PendingEdge(condition.Id, FlowEdgeType.True)]);
            foreach (var incrementor in loop.Incrementors)
            {
                bodyExits = AddAction(
                    $"for iterator: {Text(incrementor)}",
                    incrementor,
                    bodyExits);
            }

            ConnectLoopBack(bodyExits, condition.Id);
            return [new PendingEdge(condition.Id, FlowEdgeType.False)];
        }

        private IReadOnlyList<PendingEdge> AddDoWhile(
            DoStatementSyntax loop,
            IReadOnlyList<PendingEdge> incoming)
        {
            var firstBodyNodeIndex = nodes.Count;
            var bodyExits = AddStatements(Unwrap(loop.Statement), incoming);
            var decision = AddNode(FlowNodeType.Decision, Text(loop.Condition), loop.Condition);
            Connect(bodyExits, decision.Id);

            var repeatTarget = firstBodyNodeIndex < nodes.Count - 1
                ? nodes[firstBodyNodeIndex].Id
                : decision.Id;
            AddEdge(decision.Id, repeatTarget, FlowEdgeType.LoopBack, "True · repeat");
            return [new PendingEdge(decision.Id, FlowEdgeType.False)];
        }

        private IReadOnlyList<PendingEdge> AddAction(
            string label,
            SyntaxNode source,
            IReadOnlyList<PendingEdge> incoming)
        {
            var action = AddNode(FlowNodeType.Action, label, source);
            Connect(incoming, action.Id);
            return [new PendingEdge(action.Id, FlowEdgeType.Next)];
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
                AddEdge(pending.FromNodeId, toNodeId, pending.Type, EdgeLabel(pending.Type));
            }
        }

        private void ConnectLoopBack(IEnumerable<PendingEdge> incoming, string toNodeId)
        {
            foreach (var pending in incoming)
            {
                var label = pending.Type switch
                {
                    FlowEdgeType.True => "True · repeat",
                    FlowEdgeType.False => "False · repeat",
                    _ => "Repeat",
                };
                AddEdge(pending.FromNodeId, toNodeId, FlowEdgeType.LoopBack, label);
            }
        }

        private void AddEdge(
            string fromNodeId,
            string toNodeId,
            FlowEdgeType type,
            string? label = null) =>
            edges.Add(new FlowEdge(
                $"edge-{edges.Count:D3}",
                fromNodeId,
                toNodeId,
                type,
                label));

        private static string? EdgeLabel(FlowEdgeType type) => type switch
        {
            FlowEdgeType.True => "True",
            FlowEdgeType.False => "False",
            FlowEdgeType.LoopBack => "Repeat",
            _ => null,
        };

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
