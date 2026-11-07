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
                case InvocationExpressionSyntax invocation when IsConsoleReadLine(invocation):
                    return Diagnostic(
                        invocation,
                        "ET1001",
                        "Console.ReadLine is not supported by deterministic traces.",
                        "The trace has no live console input. Replace input with an authored literal value.");
                case AssignmentExpressionSyntax assignment
                    when assignment.Left is not (IdentifierNameSyntax or ElementAccessExpressionSyntax):
                    return Diagnostic(
                        assignment.Left,
                        "ET1001",
                        "Only variables and array items can be assigned in traces.",
                        "Assign to a named variable or to an item such as scores[0].");
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
        private const int MaximumArrayLength = 50;

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

        private Dictionary<string, TraceType> variableTypes => frames.Peek().Types;

        public ExecutionTraceResult Run()
        {
            RecordSynthetic(FlowNodeType.Start, scope: null, structure.Main is null
                ? "Trace ready. No learner code has been run."
                : "Trace ready. The program starts at Main. No learner code has been run.");
            if (structure.Main?.ParameterList.Parameters.FirstOrDefault() is { } argsParameter)
            {
                // args exists in the simulation but is always empty: there is no command line.
                variables[argsParameter.Identifier.ValueText] = new ArrayValue(SyntaxKind.StringKeyword, []);
                variableTypes[argsParameter.Identifier.ValueText] = new TraceType(SyntaxKind.StringKeyword, IsArray: true);
            }

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
                        InvocationExpressionSyntax call when IsConsoleCall(call, "WriteLine") && value is ArrayValue =>
                            $"Added {FormatForOutput(value)} to output. Printing an array shows its type, not its items; use foreach to print each one.",
                        InvocationExpressionSyntax call when IsConsoleCall(call, "WriteLine") => $"Added {FormatForOutput(value)} to output.",
                        InvocationExpressionSyntax call when IsConsoleCall(call, "Write") => $"Wrote {FormatForOutput(value)} without ending the line.",
                        InvocationExpressionSyntax call when structure.CalledMethodName(call) is { } name => value is VoidResult
                            ? $"Called {name}; it finished and control came back here."
                            : $"Called {name}; it returned {Format(value)}, which is not stored.",
                        AssignmentExpressionSyntax assigned => $"Set {assigned.Left} to {Format(value)}.",
                        PrefixUnaryExpressionSyntax { Operand: var target } => $"{target} is now {Format(Evaluate(target))}.",
                        PostfixUnaryExpressionSyntax { Operand: var target } => $"{target} is now {Format(Evaluate(target))}.",
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
                        if (!condition || !RunLoopBody(loop.Statement))
                        {
                            return;
                        }
                    }
                case ForStatementSyntax loop:
                    ExecuteFor(loop);
                    return;
                case SwitchStatementSyntax choice:
                    ExecuteSwitch(choice);
                    return;
                case ForEachStatementSyntax loop:
                    ExecuteForEach(loop);
                    return;
                case BreakStatementSyntax:
                    RecordOperation(statement, "break leaves the nearest loop or switch straight away.");
                    throw new BreakSignal();
                case ContinueStatementSyntax:
                    RecordOperation(statement, "continue skips the rest of this repetition and goes to the next one.");
                    throw new ContinueSignal();
                case DoStatementSyntax loop:
                    do
                    {
                        if (!RunLoopBody(loop.Statement))
                        {
                            return;
                        }

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

                if (!RunLoopBody(loop.Statement))
                {
                    return;
                }

                foreach (var incrementor in loop.Incrementors)
                {
                    Evaluate(incrementor);
                    RecordOperation(incrementor, "Applied the for iterator and updated the variable state.");
                }
            }
        }

        /// <summary>Runs one repetition of a loop body. Returns false when a break ended the loop.</summary>
        private bool RunLoopBody(StatementSyntax body)
        {
            try
            {
                ExecuteEmbedded(body);
            }
            catch (BreakSignal)
            {
                return false;
            }
            catch (ContinueSignal)
            {
                // The rest of the body is skipped; the loop goes on to its iterator or condition.
            }

            return true;
        }

        /// <summary>
        /// Evaluates the switch value once, checks each case in order, and runs the first match, or the
        /// default section when nothing matches. A break inside the section leaves only the switch;
        /// continue passes through to the enclosing loop.
        /// </summary>
        private void ExecuteSwitch(SwitchStatementSyntax choice)
        {
            var value = Evaluate(choice.Expression);
            RecordOperation(choice.Expression, $"The switch value is {Format(value)}.");

            SwitchSectionSyntax? chosen = null;
            foreach (var section in choice.Sections)
            {
                if (section.Labels.Any(label => label is DefaultSwitchLabelSyntax))
                {
                    continue;
                }

                var values = section.Labels.OfType<CaseSwitchLabelSyntax>().ToArray();
                var matched = values.Any(label => AreEqual(value, Evaluate(label.Value)));
                var text = string.Join(" or ", values.Select(label => label.Value.ToString()));
                RecordOperation(values[0], matched
                    ? $"{Format(value)} matches case {text}, so this case runs."
                    : $"{Format(value)} does not match case {text}.");
                if (matched)
                {
                    chosen = section;
                    break;
                }
            }

            chosen ??= choice.Sections.FirstOrDefault(section => section.Labels.Any(label => label is DefaultSwitchLabelSyntax));
            if (chosen is null)
            {
                return;
            }

            try
            {
                ExecuteStatements(ProgramStructure.SwitchSectionBody(chosen));
            }
            catch (BreakSignal)
            {
                // break leaves the switch only.
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
            var type = TypeOf(declaration.Type);
            foreach (var declarator in declaration.Variables)
            {
                var name = declarator.Identifier.ValueText;
                var initial = declarator.Initializer!.Value is InitializerExpressionSyntax items && type.IsArray
                    ? CreateArray(type.Element, items)
                    : Evaluate(declarator.Initializer.Value);
                variables[name] = Coerce(initial, type, declarator);
                variableTypes[name] = type;
            }
        }

        /// <summary>
        /// Runs foreach over an array (or the characters of a string). Each repetition records the
        /// "more items?" decision and the step that copies the next item into the loop variable.
        /// </summary>
        private void ExecuteForEach(ForEachStatementSyntax loop)
        {
            var collection = Evaluate(loop.Expression);
            var name = loop.Identifier.ValueText;
            var type = TypeOf(loop.Type);
            var collectionText = loop.Expression.ToString();
            var count = collection switch
            {
                ArrayValue array => array.Items.Length,
                string text => text.Length,
                _ => throw new TraceUnsupportedException(loop.Expression, "foreach needs an array or a string to take items from."),
            };

            try
            {
                for (var index = 0; index < count; index++)
                {
                    RecordOperation(loop.Expression, $"{collectionText} has another item (item {index + 1} of {count}), so the loop continues.");
                    var item = collection is ArrayValue array ? array.Items[index] : ((string)collection)[index];
                    variables[name] = Coerce(item, type, loop.Type);
                    variableTypes[name] = type;
                    RecordOperation(loop.Type, $"Set {name} to {Format(variables[name])} (item {index + 1} of {count}).");
                    if (!RunLoopBody(loop.Statement))
                    {
                        return;
                    }
                }

                RecordOperation(loop.Expression, count == 0
                    ? $"{collectionText} has no items, so the loop body never runs."
                    : $"All {count} items of {collectionText} have been used, so the loop ends.");
            }
            finally
            {
                // The loop variable exists only inside the loop.
                variables.Remove(name);
                variableTypes.Remove(name);
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
            ElementAccessExpressionSyntax access => ReadItem(access),
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } length => Evaluate(length.Expression) switch
            {
                ArrayValue array => array.Items.Length,
                string text => text.Length,
                _ => throw new TraceUnsupportedException(length, ".Length needs an array or a string."),
            },
            ArrayCreationExpressionSyntax creation => CreateArray(creation),
            ImplicitArrayCreationExpressionSyntax creation => CreateImplicitArray(creation),
            _ => throw new TraceUnsupportedException(
                expression,
                $"{expression.Kind()} is not supported by traces."),
        };

        private ArrayValue CreateArray(ArrayCreationExpressionSyntax creation)
        {
            var element = TypeOf(creation.Type.ElementType).Element;
            if (creation.Initializer is not null)
            {
                return CreateArray(element, creation.Initializer);
            }

            var size = creation.Type.RankSpecifiers[0].Sizes[0];
            if (Evaluate(size) is not int length || length < 0)
            {
                throw new TraceUnsupportedException(size, "An array size must be a whole number of 0 or more.");
            }

            CheckArrayLength(length, creation);
            return new ArrayValue(element, [.. Enumerable.Repeat(DefaultValue(element), length)]);
        }

        private ArrayValue CreateArray(SyntaxKind element, InitializerExpressionSyntax initializer)
        {
            CheckArrayLength(initializer.Expressions.Count, initializer);
            return new ArrayValue(
                element,
                [.. initializer.Expressions.Select(item => CoerceScalar(Evaluate(item), element, item))]);
        }

        // new[] { ... } takes its item type from the values: whole numbers mixed with decimals become double.
        private ArrayValue CreateImplicitArray(ImplicitArrayCreationExpressionSyntax creation)
        {
            var values = creation.Initializer.Expressions.Select(Evaluate).ToArray();
            CheckArrayLength(values.Length, creation);
            var element = values switch
            {
                [] => throw new TraceUnsupportedException(creation, "new[] { } needs at least one item to know its type."),
                _ when values.All(value => value is int) => SyntaxKind.IntKeyword,
                _ when values.All(value => value is int or double) => SyntaxKind.DoubleKeyword,
                _ when values.All(value => value is bool) => SyntaxKind.BoolKeyword,
                _ when values.All(value => value is string) => SyntaxKind.StringKeyword,
                _ when values.All(value => value is char) => SyntaxKind.CharKeyword,
                _ => throw new TraceUnsupportedException(creation, "All items in new[] { } must have the same type."),
            };
            return new ArrayValue(element, [.. values.Select((value, index) => CoerceScalar(value, element, creation.Initializer.Expressions[index]))]);
        }

        private static void CheckArrayLength(int length, SyntaxNode source)
        {
            if (length > MaximumArrayLength)
            {
                throw new TraceUnsupportedException(source, $"Arrays in the trace are limited to {MaximumArrayLength} items so every item stays visible.");
            }
        }

        private object ReadItem(ElementAccessExpressionSyntax access)
        {
            var target = Evaluate(access.Expression);
            var index = IndexOf(access, target);
            return target is ArrayValue array ? array.Items[index] : ((string)target)[index];
        }

        private int IndexOf(ElementAccessExpressionSyntax access, object target)
        {
            var length = target switch
            {
                ArrayValue array => array.Items.Length,
                string text => text.Length,
                _ => throw new TraceUnsupportedException(access.Expression, "Only arrays and strings can be indexed with [ ]."),
            };
            var argument = access.ArgumentList.Arguments[0].Expression;
            if (Evaluate(argument) is not int index)
            {
                throw new TraceUnsupportedException(argument, "An index must be a whole number.");
            }

            if (index < 0 || index >= length)
            {
                throw new TraceUnsupportedException(
                    access,
                    length == 0
                        ? $"Index {index} is outside {access.Expression}, which has no items. Real C# would stop here with an IndexOutOfRangeException."
                        : $"Index {index} is outside {access.Expression} (valid indexes are 0 to {length - 1}). Real C# would stop here with an IndexOutOfRangeException.");
            }

            return index;
        }

        private object WriteItem(ElementAccessExpressionSyntax access, Func<object, object> update, SyntaxNode source)
        {
            var target = Evaluate(access.Expression);
            if (target is string)
            {
                throw new TraceUnsupportedException(access, "Strings cannot be changed one character at a time; build a new string instead.");
            }

            var index = IndexOf(access, target);
            var array = (ArrayValue)target;
            var updated = CoerceScalar(update(array.Items[index]), array.Element, source);
            array.Items[index] = updated;
            return updated;
        }

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
            if (assignment.Left is ElementAccessExpressionSyntax item)
            {
                var right = Evaluate(assignment.Right);
                return WriteItem(item, current => Combine(assignment, current, right), assignment);
            }

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

            var rightValue = Evaluate(assignment.Right);
            var value = Combine(assignment, assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? null : GetVariable(identifier), rightValue);
            var coerced = Coerce(value, variableTypes[name], assignment);
            variables[name] = coerced;
            return coerced;
        }

        private static object Combine(AssignmentExpressionSyntax assignment, object? current, object right) => assignment.Kind() switch
        {
            SyntaxKind.SimpleAssignmentExpression => right,
            SyntaxKind.AddAssignmentExpression => Add(current!, right, assignment),
            SyntaxKind.SubtractAssignmentExpression => Numeric(current!, right, assignment, (a, b) => a - b),
            SyntaxKind.MultiplyAssignmentExpression => Numeric(current!, right, assignment, (a, b) => a * b),
            SyntaxKind.DivideAssignmentExpression => Divide(current!, right, assignment),
            _ => throw new TraceUnsupportedException(assignment, $"Assignment '{assignment.OperatorToken.Text}' is not supported by traces."),
        };

        private object EvaluatePrefix(PrefixUnaryExpressionSyntax prefix)
        {
            if (prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression)
            {
                var increment = prefix.IsKind(SyntaxKind.PreIncrementExpression);
                return prefix.Operand switch
                {
                    IdentifierNameSyntax identifier => ChangeByOne(identifier, increment, prefix),
                    ElementAccessExpressionSyntax item => WriteItem(item, current => Numeric(current, 1, prefix, increment ? (a, b) => a + b : (a, b) => a - b), prefix),
                    _ => throw new TraceUnsupportedException(prefix, "Increment and decrement need a variable or an array item."),
                };
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
            var increment = postfix.IsKind(SyntaxKind.PostIncrementExpression);
            if (postfix.Operand is ElementAccessExpressionSyntax item)
            {
                object? before = null;
                WriteItem(item, current =>
                {
                    before = current;
                    return Numeric(current, 1, postfix, increment ? (a, b) => a + b : (a, b) => a - b);
                }, postfix);
                return before!;
            }

            if (postfix.Operand is not IdentifierNameSyntax identifier)
            {
                throw new TraceUnsupportedException(postfix, "Increment and decrement need a variable or an array item.");
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
                        builder.Append(FormatForOutput(Evaluate(interpolation.Expression)));
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

            WriteOutput(FormatForOutput(value), writesLine);
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

        private static TraceType TypeOf(TypeSyntax type) => type switch
        {
            PredefinedTypeSyntax predefined => new TraceType(predefined.Keyword.Kind(), IsArray: false),
            ArrayTypeSyntax { ElementType: PredefinedTypeSyntax element } => new TraceType(element.Keyword.Kind(), IsArray: true),
            _ => throw new TraceUnsupportedException(type, $"Type '{type}' is not supported by traces."),
        };

        private static object DefaultValue(SyntaxKind element) => element switch
        {
            SyntaxKind.IntKeyword => 0,
            SyntaxKind.DoubleKeyword => 0.0,
            SyntaxKind.BoolKeyword => false,
            SyntaxKind.CharKeyword => '\0',
            _ => NullValue.Instance,
        };

        private object GetVariable(IdentifierNameSyntax identifier)
        {
            if (variables.TryGetValue(identifier.Identifier.ValueText, out var value))
            {
                return value is NullValue
                    ? throw new TraceUnsupportedException(identifier, $"{identifier.Identifier.ValueText} is null. Give it a value before using it.")
                    : value;
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
                return FormatForOutput(left) + FormatForOutput(right);
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
        // Each arm is boxed separately: a switch over int and double arms would otherwise widen
        // the int result to double, turning -5 into -5.0.
        private static object Negate(object value, SyntaxNode source) => value switch
        {
            int integer => (object)-integer,
            double number => (object)-number,
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

        private static object Coerce(object value, TraceType type, SyntaxNode source)
        {
            if (!type.IsArray)
            {
                return CoerceScalar(value, type.Element, source);
            }

            return value is ArrayValue array && array.Element == type.Element
                ? array
                : throw new TraceUnsupportedException(source, $"Value '{Format(value)}' is not compatible with the declared {type} type.");
        }

        private static object CoerceScalar(object value, SyntaxKind type, SyntaxNode source) => type switch
        {
            SyntaxKind.StringKeyword when value is NullValue => value,
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

        /// <summary>How a value appears in the variables panel: arrays list their items.</summary>
        private static string Format(object value) => value switch
        {
            ArrayValue array => array.Items.Length == 0
                ? "{ } (empty)"
                : $"{{ {string.Join(", ", array.Items.Select(FormatItem))} }}",
            NullValue => "null",
            bool boolean => boolean ? "true" : "false",
            double number => number.ToString("G", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        private static string FormatItem(object item) => item switch
        {
            string text => $"\"{text}\"",
            char character => character == '\0' ? "'\\0'" : $"'{character}'",
            _ => Format(item),
        };

        /// <summary>
        /// What Console output and string building produce. Like real C#, an array prints its type name,
        /// and null prints nothing.
        /// </summary>
        private static string FormatForOutput(object value) => value switch
        {
            ArrayValue array => $"System.{ClrName(array.Element)}[]",
            NullValue => string.Empty,
            _ => Format(value),
        };

        private static string ClrName(SyntaxKind element) => element switch
        {
            SyntaxKind.IntKeyword => "Int32",
            SyntaxKind.DoubleKeyword => "Double",
            SyntaxKind.BoolKeyword => "Boolean",
            SyntaxKind.CharKeyword => "Char",
            _ => "String",
        };
    }

    private sealed record Frame(string? Scope)
    {
        public Dictionary<string, object> Variables { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TraceType> Types { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>A declared type: a scalar keyword such as int, or a one-dimensional array of one.</summary>
    private readonly record struct TraceType(SyntaxKind Element, bool IsArray)
    {
        public override string ToString()
        {
            var name = Element.ToString().Replace("Keyword", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            return IsArray ? $"{name}[]" : name;
        }
    }

    /// <summary>
    /// An array is shared, not copied: assigning it to another variable or passing it to a method gives
    /// both names the same items, exactly as in C#.
    /// </summary>
    private sealed class ArrayValue(SyntaxKind element, object[] items)
    {
        public SyntaxKind Element { get; } = element;

        public object[] Items { get; } = items;
    }

    /// <summary>The default value of a string item in new string[n].</summary>
    private sealed class NullValue
    {
        public static NullValue Instance { get; } = new();
    }

    /// <summary>The value of a call to a void method; it cannot be stored, printed, or compared.</summary>
    private sealed class VoidResult
    {
        public static VoidResult Instance { get; } = new();

        public override string ToString() => "(no value)";
    }

    /// <summary>Unwinds to the nearest loop or switch when a break statement runs.</summary>
    private sealed class BreakSignal : Exception;

    /// <summary>Unwinds to the nearest loop when a continue statement runs.</summary>
    private sealed class ContinueSignal : Exception;

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
