using System.Globalization;
using System.Text;

using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeLogic.Infrastructure.Execution;

public sealed class RoslynPedagogicalInterpreter : IPedagogicalInterpreter
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    private readonly IFlowchartTranslator flowchartTranslator;

    public RoslynPedagogicalInterpreter(
        IFlowchartTranslator flowchartTranslator,
        ExecutionTraceLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(flowchartTranslator);
        this.flowchartTranslator = flowchartTranslator;
        Limits = limits ?? ExecutionTraceLimits.Default;
    }

    public ExecutionTraceLimits Limits { get; }

    public async Task<ExecutionTraceResult> TraceAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        var translation = await flowchartTranslator.TranslateAsync(source, cancellationToken);
        if (!translation.IsSuccess)
        {
            return new ExecutionTraceResult(
                MapStatus(translation.Status),
                Limits.MaximumOperations,
                diagnostics: translation.Diagnostics);
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source!,
            ParseOptions,
            cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);
        var unsupported = FindUnsupportedTraceSyntax(root);
        if (unsupported is not null)
        {
            return new ExecutionTraceResult(
                ExecutionTraceStatus.Unsupported,
                Limits.MaximumOperations,
                diagnostics: [unsupported]);
        }

        var statements = root.DescendantNodes()
            .OfType<GlobalStatementSyntax>()
            .Select(global => global.Statement)
            .ToArray();
        var interpreter = new Interpreter(
            translation.Graph!,
            Limits.MaximumOperations,
            cancellationToken);

        try
        {
            return interpreter.Run(statements);
        }
        catch (OperationLimitReachedException)
        {
            return new ExecutionTraceResult(
                ExecutionTraceStatus.StepLimitReached,
                Limits.MaximumOperations,
                interpreter.Steps,
                [new CodeDiagnostic(
                    "ET0002",
                    CodeDiagnosticCategory.Limit,
                    CodeDiagnosticSeverity.Warning,
                    $"The trace stopped after {Limits.MaximumOperations} operations.",
                    "The bounded interpreter stopped before a possible infinite loop could keep using server resources.",
                    null,
                    null,
                    0)]);
        }
        catch (TraceUnsupportedException exception)
        {
            return new ExecutionTraceResult(
                ExecutionTraceStatus.Unsupported,
                Limits.MaximumOperations,
                diagnostics: [Diagnostic(
                    exception.Node,
                    "ET1002",
                    exception.Message,
                    "Rewrite the expression using compatible scalar values in the documented trace subset.")]);
        }
    }

    private static CodeDiagnostic? FindUnsupportedTraceSyntax(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case VariableDeclaratorSyntax { Initializer: null } declarator:
                    return Diagnostic(
                        declarator,
                        "ET1001",
                        $"Variable '{declarator.Identifier.ValueText}' needs an initial value for tracing.",
                        "Give every traced variable an initial value so each state is deterministic.");
                case ArrayTypeSyntax or ArrayCreationExpressionSyntax
                    or ImplicitArrayCreationExpressionSyntax or ElementAccessExpressionSyntax:
                    return Diagnostic(
                        node,
                        "ET1001",
                        "Arrays are not supported by the current trace subset.",
                        "Use scalar int, double, bool, string, or char variables for this trace.");
                case InvocationExpressionSyntax invocation when IsConsoleReadLine(invocation):
                    return Diagnostic(
                        invocation,
                        "ET1001",
                        "Console.ReadLine is not supported by deterministic traces.",
                        "The trace has no live console input. Replace input with an authored literal value.");
                case AssignmentExpressionSyntax assignment
                    when assignment.Left is not IdentifierNameSyntax:
                    return Diagnostic(
                        assignment.Left,
                        "ET1001",
                        "Only direct variable assignment is supported by traces.",
                        "Assign to a named scalar variable rather than an array element or other target.");
            }
        }

        return null;
    }

    private static bool IsConsoleReadLine(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Console" },
            Name.Identifier.ValueText: "ReadLine",
        };

    private static CodeDiagnostic Diagnostic(
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

    private static ExecutionTraceStatus MapStatus(FlowchartTranslationStatus status) => status switch
    {
        FlowchartTranslationStatus.InvalidInput => ExecutionTraceStatus.InvalidInput,
        FlowchartTranslationStatus.SyntaxError => ExecutionTraceStatus.SyntaxError,
        FlowchartTranslationStatus.Unsupported => ExecutionTraceStatus.Unsupported,
        FlowchartTranslationStatus.TimedOut => ExecutionTraceStatus.TimedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unexpected translation status."),
    };

    private sealed class Interpreter
    {
        private readonly FlowGraph graph;
        private readonly int maximumOperations;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<string, object> variables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SyntaxKind> variableTypes = new(StringComparer.Ordinal);
        private readonly List<string> output = [];
        private readonly List<ExecutionTraceStep> steps = [];
        private int operations;

        public Interpreter(
            FlowGraph graph,
            int maximumOperations,
            CancellationToken cancellationToken)
        {
            this.graph = graph;
            this.maximumOperations = maximumOperations;
            this.cancellationToken = cancellationToken;
        }

        public IReadOnlyList<ExecutionTraceStep> Steps => steps;

        public ExecutionTraceResult Run(IReadOnlyList<StatementSyntax> statements)
        {
            RecordSynthetic(FlowNodeType.Start, "Trace ready. No learner code has been run.");
            ExecuteStatements(statements);
            RecordSynthetic(FlowNodeType.End, "The bounded simulation reached the end.");
            return new ExecutionTraceResult(
                ExecutionTraceStatus.Completed,
                maximumOperations,
                steps);
        }

        private void ExecuteStatements(IEnumerable<StatementSyntax> statements)
        {
            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExecuteStatement(statement);
            }
        }

        private void ExecuteStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case EmptyStatementSyntax:
                    return;
                case LocalDeclarationStatementSyntax declaration:
                    ExecuteDeclaration(declaration.Declaration);
                    RecordOperation(statement, DescribeDeclaration(declaration.Declaration));
                    return;
                case ExpressionStatementSyntax expressionStatement:
                    var value = Evaluate(expressionStatement.Expression);
                    var explanation = IsConsoleWriteLine(expressionStatement.Expression)
                        ? $"Added {Format(value)} to output."
                        : expressionStatement.Expression is AssignmentExpressionSyntax
                            or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax
                                ? "Updated the variable state."
                                : $"Evaluated the statement; its value is {Format(value)}.";
                    RecordOperation(statement, explanation);
                    return;
                case IfStatementSyntax branch:
                    var branchResult = RequireBoolean(Evaluate(branch.Condition), branch.Condition);
                    RecordOperation(
                        branch.Condition,
                        $"The condition is {Format(branchResult)}, so the {(branchResult ? "True" : "False")} path is active.");
                    if (branchResult)
                    {
                        ExecuteEmbedded(branch.Statement);
                    }
                    else if (branch.Else is not null)
                    {
                        ExecuteEmbedded(branch.Else.Statement);
                    }

                    return;
                case WhileStatementSyntax loop:
                    while (true)
                    {
                        var condition = RequireBoolean(Evaluate(loop.Condition), loop.Condition);
                        RecordOperation(
                            loop.Condition,
                            $"The while condition is {Format(condition)}.");
                        if (!condition)
                        {
                            return;
                        }

                        ExecuteEmbedded(loop.Statement);
                    }
                case ForStatementSyntax loop:
                    ExecuteFor(loop);
                    return;
                case DoStatementSyntax loop:
                    do
                    {
                        ExecuteEmbedded(loop.Statement);
                        var condition = RequireBoolean(Evaluate(loop.Condition), loop.Condition);
                        RecordOperation(
                            loop.Condition,
                            $"The do-while condition is {Format(condition)}.");
                        if (!condition)
                        {
                            return;
                        }
                    }
                    while (true);
                default:
                    throw new TraceUnsupportedException(statement, $"{statement.Kind()} is not supported by traces.");
            }
        }

        private void ExecuteFor(ForStatementSyntax loop)
        {
            if (loop.Declaration is not null)
            {
                ExecuteDeclaration(loop.Declaration);
                RecordOperation(loop.Declaration, DescribeDeclaration(loop.Declaration));
            }

            foreach (var initializer in loop.Initializers)
            {
                var value = Evaluate(initializer);
                RecordOperation(initializer, $"Evaluated the for initializer as {Format(value)}.");
            }

            while (true)
            {
                var condition = loop.Condition is null
                    || RequireBoolean(Evaluate(loop.Condition), loop.Condition);
                RecordOperation(
                    (SyntaxNode?)loop.Condition ?? loop,
                    loop.Condition is null
                        ? "The omitted for condition is treated as true."
                        : $"The for condition is {Format(condition)}.");
                if (!condition)
                {
                    return;
                }

                ExecuteEmbedded(loop.Statement);
                foreach (var incrementor in loop.Incrementors)
                {
                    Evaluate(incrementor);
                    RecordOperation(incrementor, "Applied the for iterator and updated the variable state.");
                }
            }
        }

        private void ExecuteEmbedded(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                ExecuteStatements(block.Statements);
                return;
            }

            ExecuteStatement(statement);
        }

        private void ExecuteDeclaration(VariableDeclarationSyntax declaration)
        {
            var type = declaration.Type is PredefinedTypeSyntax predefined
                ? predefined.Keyword.Kind()
                : throw new TraceUnsupportedException(
                    declaration.Type,
                    $"Type '{declaration.Type}' is not supported by traces.");
            foreach (var declarator in declaration.Variables)
            {
                var name = declarator.Identifier.ValueText;
                variables[name] = Coerce(Evaluate(declarator.Initializer!.Value), type, declarator);
                variableTypes[name] = type;
            }
        }

        private string DescribeDeclaration(VariableDeclarationSyntax declaration) =>
            string.Join(
                " ",
                declaration.Variables.Select(variable =>
                    $"Set {variable.Identifier.ValueText} to {Format(variables[variable.Identifier.ValueText])}."));

        private object Evaluate(ExpressionSyntax expression) => expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.Value
                ?? throw new TraceUnsupportedException(literal, "This literal has no supported value."),
            IdentifierNameSyntax identifier => GetVariable(identifier),
            ParenthesizedExpressionSyntax parenthesized => Evaluate(parenthesized.Expression),
            BinaryExpressionSyntax binary => EvaluateBinary(binary),
            AssignmentExpressionSyntax assignment => EvaluateAssignment(assignment),
            PrefixUnaryExpressionSyntax prefix => EvaluatePrefix(prefix),
            PostfixUnaryExpressionSyntax postfix => EvaluatePostfix(postfix),
            InterpolatedStringExpressionSyntax interpolated => EvaluateInterpolatedString(interpolated),
            InvocationExpressionSyntax invocation => EvaluateInvocation(invocation),
            _ => throw new TraceUnsupportedException(
                expression,
                $"{expression.Kind()} is not supported by traces."),
        };

        private object EvaluateBinary(BinaryExpressionSyntax binary)
        {
            var left = Evaluate(binary.Left);
            if (binary.IsKind(SyntaxKind.LogicalAndExpression) && !RequireBoolean(left, binary.Left))
            {
                return false;
            }

            if (binary.IsKind(SyntaxKind.LogicalOrExpression) && RequireBoolean(left, binary.Left))
            {
                return true;
            }

            var right = Evaluate(binary.Right);
            return binary.Kind() switch
            {
                SyntaxKind.AddExpression => Add(left, right, binary),
                SyntaxKind.SubtractExpression => Numeric(left, right, binary, (a, b) => a - b),
                SyntaxKind.MultiplyExpression => Numeric(left, right, binary, (a, b) => a * b),
                SyntaxKind.DivideExpression => Divide(left, right, binary),
                SyntaxKind.ModuloExpression => Modulo(left, right, binary),
                SyntaxKind.EqualsExpression => AreEqual(left, right),
                SyntaxKind.NotEqualsExpression => !AreEqual(left, right),
                SyntaxKind.LessThanExpression => Compare(left, right, binary) < 0,
                SyntaxKind.LessThanOrEqualExpression => Compare(left, right, binary) <= 0,
                SyntaxKind.GreaterThanExpression => Compare(left, right, binary) > 0,
                SyntaxKind.GreaterThanOrEqualExpression => Compare(left, right, binary) >= 0,
                SyntaxKind.LogicalAndExpression => RequireBoolean(right, binary.Right),
                SyntaxKind.LogicalOrExpression => RequireBoolean(right, binary.Right),
                _ => throw new TraceUnsupportedException(binary, $"Operator '{binary.OperatorToken.Text}' is not supported by traces."),
            };
        }

        private object EvaluateAssignment(AssignmentExpressionSyntax assignment)
        {
            var identifier = (IdentifierNameSyntax)assignment.Left;
            var name = identifier.Identifier.ValueText;
            if (!variables.ContainsKey(name))
            {
                throw new TraceUnsupportedException(
                    identifier,
                    $"Variable '{name}' must be declared before assignment.");
            }

            var right = Evaluate(assignment.Right);
            var value = assignment.Kind() switch
            {
                SyntaxKind.SimpleAssignmentExpression => right,
                SyntaxKind.AddAssignmentExpression => Add(GetVariable(identifier), right, assignment),
                SyntaxKind.SubtractAssignmentExpression => Numeric(GetVariable(identifier), right, assignment, (a, b) => a - b),
                SyntaxKind.MultiplyAssignmentExpression => Numeric(GetVariable(identifier), right, assignment, (a, b) => a * b),
                SyntaxKind.DivideAssignmentExpression => Divide(GetVariable(identifier), right, assignment),
                _ => throw new TraceUnsupportedException(assignment, $"Assignment '{assignment.OperatorToken.Text}' is not supported by traces."),
            };
            var coerced = Coerce(value, variableTypes[name], assignment);
            variables[name] = coerced;
            return coerced;
        }

        private object EvaluatePrefix(PrefixUnaryExpressionSyntax prefix)
        {
            if (prefix.Operand is IdentifierNameSyntax identifier
                && prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression)
            {
                return ChangeByOne(identifier, prefix.IsKind(SyntaxKind.PreIncrementExpression), prefix);
            }

            var operand = Evaluate(prefix.Operand);
            return prefix.Kind() switch
            {
                SyntaxKind.LogicalNotExpression => !RequireBoolean(operand, prefix.Operand),
                SyntaxKind.UnaryMinusExpression => Negate(operand, prefix),
                SyntaxKind.UnaryPlusExpression => RequireNumber(operand, prefix),
                _ => throw new TraceUnsupportedException(prefix, $"Operator '{prefix.OperatorToken.Text}' is not supported by traces."),
            };
        }

        private object EvaluatePostfix(PostfixUnaryExpressionSyntax postfix)
        {
            if (postfix.Operand is not IdentifierNameSyntax identifier)
            {
                throw new TraceUnsupportedException(postfix, "Increment and decrement require a named variable.");
            }

            var original = GetVariable(identifier);
            ChangeByOne(identifier, postfix.IsKind(SyntaxKind.PostIncrementExpression), postfix);
            return original;
        }

        private object ChangeByOne(IdentifierNameSyntax identifier, bool increment, SyntaxNode source)
        {
            var original = GetVariable(identifier);
            var updated = Numeric(original, 1, source, increment ? (a, b) => a + b : (a, b) => a - b);
            variables[identifier.Identifier.ValueText] = updated;
            return updated;
        }

        private string EvaluateInterpolatedString(InterpolatedStringExpressionSyntax interpolated)
        {
            var builder = new StringBuilder();
            foreach (var content in interpolated.Contents)
            {
                switch (content)
                {
                    case InterpolatedStringTextSyntax text:
                        builder.Append(text.TextToken.ValueText);
                        break;
                    case InterpolationSyntax interpolation:
                        builder.Append(Format(Evaluate(interpolation.Expression)));
                        break;
                }
            }

            return builder.ToString();
        }

        private object EvaluateInvocation(InvocationExpressionSyntax invocation)
        {
            if (!IsConsoleWriteLine(invocation))
            {
                throw new TraceUnsupportedException(invocation, "Only Console.WriteLine is supported by traces.");
            }

            if (invocation.ArgumentList.Arguments.Count > 1)
            {
                throw new TraceUnsupportedException(invocation, "Console.WriteLine traces accept zero or one argument.");
            }

            var value = invocation.ArgumentList.Arguments.Count == 0
                ? string.Empty
                : Evaluate(invocation.ArgumentList.Arguments[0].Expression);
            output.Add(Format(value));
            return value;
        }

        private object GetVariable(IdentifierNameSyntax identifier)
        {
            if (variables.TryGetValue(identifier.Identifier.ValueText, out var value))
            {
                return value;
            }

            throw new TraceUnsupportedException(
                identifier,
                $"Variable '{identifier.Identifier.ValueText}' has no traced value.");
        }

        private void RecordSynthetic(FlowNodeType type, string explanation)
        {
            var node = graph.Nodes.Single(item => item.Type == type);
            steps.Add(CreateStep(node, explanation));
        }

        private void RecordOperation(SyntaxNode source, string explanation)
        {
            if (operations >= maximumOperations)
            {
                throw new OperationLimitReachedException();
            }

            operations++;
            var node = FindNode(source);
            steps.Add(CreateStep(node, explanation));
        }

        private FlowNode FindNode(SyntaxNode source) => graph.Nodes.SingleOrDefault(node =>
            node.SourceSpan?.StartOffset == source.SpanStart
            && node.SourceSpan.Length == source.Span.Length)
            ?? throw new TraceUnsupportedException(
                source,
                "The trace operation could not be matched to a flowchart node.");

        private ExecutionTraceStep CreateStep(FlowNode node, string explanation) => new(
            operations,
            node.Id,
            node.Type,
            node.Label,
            variables.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => KeyValuePair.Create(pair.Key, Format(pair.Value))),
            output,
            explanation);

        private static bool IsConsoleWriteLine(ExpressionSyntax expression) =>
            expression is InvocationExpressionSyntax invocation && IsConsoleWriteLine(invocation);

        private static bool IsConsoleWriteLine(InvocationExpressionSyntax invocation) =>
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "Console" },
                Name.Identifier.ValueText: "WriteLine",
            };

        private static bool RequireBoolean(object value, SyntaxNode source) => value is bool result
            ? result
            : throw new TraceUnsupportedException(source, "This condition did not produce true or false.");

        private static object RequireNumber(object value, SyntaxNode source) => value is int or double
            ? value
            : throw new TraceUnsupportedException(source, "This operator requires a number.");

        private static object Add(object left, object right, SyntaxNode source)
        {
            if (left is string || right is string)
            {
                return Format(left) + Format(right);
            }

            return Numeric(left, right, source, (a, b) => a + b);
        }

        private static object Divide(object left, object right, SyntaxNode source)
        {
            var divisor = ToDouble(right, source);
            if (divisor == 0)
            {
                throw new TraceUnsupportedException(source, "Division by zero cannot be traced.");
            }

            if (left is int leftInt && right is int rightInt)
            {
                return leftInt / rightInt;
            }

            return ToDouble(left, source) / divisor;
        }

        private static object Modulo(object left, object right, SyntaxNode source)
        {
            var divisor = ToDouble(right, source);
            if (divisor == 0)
            {
                throw new TraceUnsupportedException(source, "Division by zero cannot be traced.");
            }

            if (left is int leftInt && right is int rightInt)
            {
                return leftInt % rightInt;
            }

            return ToDouble(left, source) % divisor;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1859:Use concrete types when possible",
            Justification = "The interpreter deliberately preserves either int or double values in its object value model.")]
        private static object Numeric(
            object left,
            object right,
            SyntaxNode source,
            Func<double, double, double> operation)
        {
            var result = operation(ToDouble(left, source), ToDouble(right, source));
            if (left is int && right is int && result is >= int.MinValue and <= int.MaxValue)
            {
                return (int)result;
            }

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1859:Use concrete types when possible",
            Justification = "The interpreter deliberately preserves either int or double values in its object value model.")]
        private static object Negate(object value, SyntaxNode source) => value switch
        {
            int integer => -integer,
            double number => -number,
            _ => throw new TraceUnsupportedException(source, "Unary minus requires a number."),
        };

        private static double ToDouble(object value, SyntaxNode source) => value switch
        {
            int integer => integer,
            double number => number,
            _ => throw new TraceUnsupportedException(source, "This operator requires numeric values."),
        };

        private static int Compare(object left, object right, SyntaxNode source)
        {
            if (left is int or double && right is int or double)
            {
                return ToDouble(left, source).CompareTo(ToDouble(right, source));
            }

            if (left is char leftChar && right is char rightChar)
            {
                return leftChar.CompareTo(rightChar);
            }

            throw new TraceUnsupportedException(source, "This comparison requires compatible numbers or characters.");
        }

        private static bool AreEqual(object left, object right) =>
            left is int or double && right is int or double
                ? Convert.ToDouble(left, CultureInfo.InvariantCulture)
                    .Equals(Convert.ToDouble(right, CultureInfo.InvariantCulture))
                : Equals(left, right);

        private static object Coerce(object value, SyntaxKind type, SyntaxNode source) => type switch
        {
            SyntaxKind.IntKeyword when value is int => value,
            SyntaxKind.DoubleKeyword when value is int integer => (double)integer,
            SyntaxKind.DoubleKeyword when value is double => value,
            SyntaxKind.BoolKeyword when value is bool => value,
            SyntaxKind.StringKeyword when value is string => value,
            SyntaxKind.CharKeyword when value is char => value,
            _ => throw new TraceUnsupportedException(
                source,
                $"Value '{Format(value)}' is not compatible with the declared {type.ToString().Replace("Keyword", string.Empty, StringComparison.Ordinal).ToLowerInvariant()} type."),
        };

        private static string Format(object value) => value switch
        {
            bool boolean => boolean ? "true" : "false",
            double number => number.ToString("G", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private sealed class OperationLimitReachedException : Exception;

    private sealed class TraceUnsupportedException(SyntaxNode node, string message)
        : Exception(message)
    {
        public SyntaxNode Node { get; } = node;
    }
}
