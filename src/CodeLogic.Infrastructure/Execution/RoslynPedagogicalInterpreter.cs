using System.Globalization;
using System.Text;

using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;

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
        var structure = ProgramStructure.Read(root);
        var unsupported = FindUnsupportedTraceSyntax(root, structure);
        if (unsupported is not null)
        {
            return new ExecutionTraceResult(
                ExecutionTraceStatus.Unsupported,
                Limits.MaximumOperations,
                diagnostics: [unsupported]);
        }

        var interpreter = new Interpreter(
            translation.Graph!,
            structure,
            Limits.MaximumOperations,
            cancellationToken);

        try
        {
            return interpreter.Run();
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

    private static CodeDiagnostic? FindUnsupportedTraceSyntax(SyntaxNode root, ProgramStructure structure)
    {
        // Main's string[] args is the one array the trace accepts: it exists but stays empty.
        var mainParameters = structure.Main?.ParameterList;
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (mainParameters is not null && mainParameters.Span.Contains(node.Span))
            {
                continue;
            }

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

    /// <summary>
    /// Steps through the program one statement at a time. Each method call pushes a frame with its own
    /// variables, so every step shows exactly the variables in scope.
    /// </summary>
    private sealed class Interpreter
    {
        private const int MaximumCallDepth = 32;

        private readonly FlowGraph graph;
        private readonly ProgramStructure structure;
        private readonly int maximumOperations;
        private readonly CancellationToken cancellationToken;
        private readonly Stack<Frame> frames = new();
        private readonly List<string> output = [];
        private readonly List<ExecutionTraceStep> steps = [];
        private bool outputLineOpen;
        private int operations;

        public Interpreter(
            FlowGraph graph,
            ProgramStructure structure,
            int maximumOperations,
            CancellationToken cancellationToken)
        {
            this.graph = graph;
            this.structure = structure;
            this.maximumOperations = maximumOperations;
            this.cancellationToken = cancellationToken;
            frames.Push(new Frame(Scope: null));
        }

        public IReadOnlyList<ExecutionTraceStep> Steps => steps;

        private Dictionary<string, object> variables => frames.Peek().Variables;

        private Dictionary<string, SyntaxKind> variableTypes => frames.Peek().Types;

        public ExecutionTraceResult Run()
        {
            RecordSynthetic(FlowNodeType.Start, scope: null, structure.Main is null
                ? "Trace ready. No learner code has been run."
                : "Trace ready. The program starts at Main. No learner code has been run.");
            object? exitValue = null;
            try
            {
                ExecuteStatements(structure.EntryStatements);
            }
            catch (ReturnSignal signal)
            {
                exitValue = signal.Value;
            }

            RecordSynthetic(FlowNodeType.End, scope: null, exitValue is null
                ? "The bounded simulation reached the end."
                : $"Main returned {Format(exitValue)}. The bounded simulation reached the end.");
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
                    var explanation = expressionStatement.Expression switch
                    {
                        InvocationExpressionSyntax call when IsConsoleCall(call, "WriteLine") => $"Added {Format(value)} to output.",
                        InvocationExpressionSyntax call when IsConsoleCall(call, "Write") => $"Wrote {Format(value)} without ending the line.",
                        InvocationExpressionSyntax call when structure.CalledMethodName(call) is { } name => value is VoidResult
                            ? $"Called {name}; it finished and control came back here."
                            : $"Called {name}; it returned {Format(value)}, which is not stored.",
                        AssignmentExpressionSyntax or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => "Updated the variable state.",
                        _ => $"Evaluated the statement; its value is {Format(value)}.",
                    };
                    RecordOperation(statement, explanation);
                    return;
                case ReturnStatementSyntax exit:
                    var returned = exit.Expression is null ? null : Evaluate(exit.Expression);
                    if (returned is VoidResult)
                    {
                        throw new TraceUnsupportedException(exit, "A void method has no value to return.");
                    }

                    RecordOperation(exit, returned is null
                        ? "Returned to the caller."
                        : $"Returned {Format(returned)} to the caller.");
                    throw new ReturnSignal(returned);
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
            if (IsMainArgs(identifier))
            {
                throw new TraceUnsupportedException(identifier, "args is read-only in the simulation.");
            }

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
            if (structure.CalledMethodName(invocation) is { } method)
            {
                return CallMethod(method, invocation);
            }

            var writesLine = IsConsoleCall(invocation, "WriteLine");
            if (!writesLine && !IsConsoleCall(invocation, "Write"))
            {
                throw new TraceUnsupportedException(invocation, "Only Console.WriteLine, Console.Write, and your static methods are supported by traces.");
            }

            if (invocation.ArgumentList.Arguments.Count > 1)
            {
                throw new TraceUnsupportedException(invocation, "Console output traces accept zero or one argument.");
            }

            var value = invocation.ArgumentList.Arguments.Count == 0
                ? string.Empty
                : Evaluate(invocation.ArgumentList.Arguments[0].Expression);
            if (value is VoidResult)
            {
                throw new TraceUnsupportedException(invocation, "A void method has no value to print.");
            }

            WriteOutput(Format(value), writesLine);
            return value;
        }

        // Write keeps the current line open; WriteLine finishes it.
        private void WriteOutput(string text, bool endLine)
        {
            if (outputLineOpen)
            {
                output[^1] += text;
            }
            else
            {
                output.Add(text);
            }

            outputLineOpen = !endLine;
        }

        private object CallMethod(string name, InvocationExpressionSyntax invocation)
        {
            if (frames.Count >= MaximumCallDepth)
            {
                throw new TraceUnsupportedException(invocation, "The call stack is too deep to trace.");
            }

            var method = structure.Methods[name];
            var parameters = method.ParameterList.Parameters;
            var arguments = invocation.ArgumentList.Arguments
                .Select(argument => Evaluate(argument.Expression))
                .ToArray();
            if (arguments.Length != parameters.Count)
            {
                throw new TraceUnsupportedException(invocation, $"{name} expects {parameters.Count} argument(s).");
            }

            var frame = new Frame(name);
            for (var index = 0; index < parameters.Count; index++)
            {
                var type = TypeOf(parameters[index].Type!);
                var parameterName = parameters[index].Identifier.ValueText;
                frame.Variables[parameterName] = Coerce(arguments[index], type, invocation.ArgumentList.Arguments[index]);
                frame.Types[parameterName] = type;
            }

            frames.Push(frame);
            try
            {
                var bindings = parameters.Count == 0
                    ? "no arguments"
                    : string.Join(", ", parameters.Select(parameter => $"{parameter.Identifier.ValueText} = {Format(frame.Variables[parameter.Identifier.ValueText])}"));
                RecordSynthetic(FlowNodeType.Start, name, $"Called {name} with {bindings}. Its variables are separate from the caller's.");

                object? returned = null;
                try
                {
                    ExecuteStatements(ProgramStructure.BodyOf(method));
                }
                catch (ReturnSignal signal)
                {
                    returned = signal.Value;
                }

                var isVoid = method.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
                if (!isVoid && returned is null)
                {
                    throw new TraceUnsupportedException(invocation, $"{name} ended without returning a value.");
                }

                var result = isVoid ? VoidResult.Instance : Coerce(returned!, TypeOf(method.ReturnType), invocation);
                RecordSynthetic(FlowNodeType.End, name, isVoid
                    ? $"{name} finished. Control goes back to the caller."
                    : $"{name} returned {Format(result)} to the caller.");
                return result;
            }
            finally
            {
                frames.Pop();
            }
        }

        private static SyntaxKind TypeOf(TypeSyntax type) => type is PredefinedTypeSyntax predefined
            ? predefined.Keyword.Kind()
            : throw new TraceUnsupportedException(type, $"Type '{type}' is not supported by traces.");

        private object GetVariable(IdentifierNameSyntax identifier)
        {
            if (variables.TryGetValue(identifier.Identifier.ValueText, out var value))
            {
                return value;
            }

            if (IsMainArgs(identifier))
            {
                throw new TraceUnsupportedException(
                    identifier,
                    "args is read-only and empty in the simulation, and arrays are not traced yet.");
            }

            throw new TraceUnsupportedException(
                identifier,
                $"Variable '{identifier.Identifier.ValueText}' has no traced value.");
        }

        private bool IsMainArgs(IdentifierNameSyntax identifier) =>
            frames.Count == 1
            && structure.Main?.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText is { } argsName
            && identifier.Identifier.ValueText == argsName;

        private void RecordSynthetic(FlowNodeType type, string? scope, string explanation)
        {
            var node = graph.Nodes.Single(item => item.Type == type && item.Scope == scope);
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

        private static bool IsConsoleCall(InvocationExpressionSyntax invocation, string member) =>
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "Console" },
                Name.Identifier.ValueText: var name,
            }
            && name == member;

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

    private sealed record Frame(string? Scope)
    {
        public Dictionary<string, object> Variables { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SyntaxKind> Types { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>The value of a call to a void method; it cannot be stored, printed, or compared.</summary>
    private sealed class VoidResult
    {
        public static VoidResult Instance { get; } = new();

        public override string ToString() => "(no value)";
    }

    /// <summary>Unwinds a method body when a return statement runs.</summary>
    private sealed class ReturnSignal(object? value) : Exception
    {
        public object? Value { get; } = value;
    }

    private sealed class OperationLimitReachedException : Exception;

    private sealed class TraceUnsupportedException(SyntaxNode node, string message)
        : Exception(message)
    {
        public SyntaxNode Node { get; } = node;
    }
}
