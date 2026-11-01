using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeLogic.Infrastructure.Analysis;

internal static class SupportedSyntaxValidator
{
    private static readonly HashSet<SyntaxKind> AllowedBinaryExpressions =
    [
        SyntaxKind.AddExpression,
        SyntaxKind.SubtractExpression,
        SyntaxKind.MultiplyExpression,
        SyntaxKind.DivideExpression,
        SyntaxKind.ModuloExpression,
        SyntaxKind.EqualsExpression,
        SyntaxKind.NotEqualsExpression,
        SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.LogicalAndExpression,
        SyntaxKind.LogicalOrExpression,
    ];

    private static readonly HashSet<SyntaxKind> AllowedAssignments =
    [
        SyntaxKind.SimpleAssignmentExpression,
        SyntaxKind.AddAssignmentExpression,
        SyntaxKind.SubtractAssignmentExpression,
        SyntaxKind.MultiplyAssignmentExpression,
        SyntaxKind.DivideAssignmentExpression,
    ];

    private static readonly HashSet<SyntaxKind> AllowedUnaryExpressions =
    [
        SyntaxKind.LogicalNotExpression,
        SyntaxKind.UnaryMinusExpression,
        SyntaxKind.UnaryPlusExpression,
        SyntaxKind.PreIncrementExpression,
        SyntaxKind.PreDecrementExpression,
        SyntaxKind.PostIncrementExpression,
        SyntaxKind.PostDecrementExpression,
    ];

    private static readonly HashSet<SyntaxKind> AllowedLiteralExpressions =
    [
        SyntaxKind.NumericLiteralExpression,
        SyntaxKind.StringLiteralExpression,
        SyntaxKind.CharacterLiteralExpression,
        SyntaxKind.TrueLiteralExpression,
        SyntaxKind.FalseLiteralExpression,
    ];

    private static readonly HashSet<SyntaxKind> AllowedPredefinedTypes =
    [
        SyntaxKind.IntKeyword,
        SyntaxKind.DoubleKeyword,
        SyntaxKind.BoolKeyword,
        SyntaxKind.StringKeyword,
        SyntaxKind.CharKeyword,
    ];

    public static IReadOnlyList<CodeDiagnostic> Validate(
        SyntaxNode root,
        int maximumNestingDepth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNestingDepth);

        var diagnostics = new List<CodeDiagnostic>();
        var rejectedSpans = new List<TextSpan>();
        var declaredFunctions = root.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Select(function => function.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trivia.IsDirective)
            {
                AddUnsupported(
                    trivia.GetStructure() ?? root,
                    "CL1005",
                    "Preprocessor directives are not supported.",
                    "Remove directives such as #if, #define, or #pragma from this short learning snippet.",
                    diagnostics,
                    rejectedSpans);
            }
        }

        foreach (var node in root.DescendantNodesAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rejectedSpans.Any(span => span.Contains(node.Span)))
            {
                continue;
            }

            switch (node)
            {
                case MemberDeclarationSyntax member when member is not GlobalStatementSyntax:
                    AddUnsupported(
                        node,
                        "CL1001",
                        $"{FriendlyName(node)} is outside the supported snippet subset.",
                        "Use top-level statements and small local functions instead of namespaces, classes, records, or other type declarations.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case UsingDirectiveSyntax:
                    AddUnsupported(
                        node,
                        "CL1001",
                        "Using directives are not supported.",
                        "The supported examples use built-in C# types and Console directly, without imports.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case BreakStatementSyntax:
                    AddUnsupported(
                        node,
                        "CL1006",
                        "A break statement is not supported.",
                        "Rewrite this learning snippet so the loop exits through its condition.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case ContinueStatementSyntax:
                    AddUnsupported(
                        node,
                        "CL1007",
                        "A continue statement is not supported.",
                        "Rewrite this learning snippet so control reaches the end of the loop body.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case StatementSyntax statement when !IsAllowedStatement(statement):
                    AddUnsupported(
                        node,
                        "CL1001",
                        $"{FriendlyName(node)} is not supported.",
                            "Use declarations, assignments, if/else, for, while, do/while, return, or a small local function.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case ExpressionSyntax expression when !IsAllowedExpression(expression):
                    AddUnsupported(
                        node,
                        "CL1001",
                        $"{FriendlyName(node)} is not supported.",
                        "Rewrite this expression using literals, variables, basic operators, arrays, or an allowed method call.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case VariableDeclarationSyntax declaration when !IsAllowedType(declaration.Type):
                    AddUnsupportedType(node, declaration.Type.ToString(), diagnostics, rejectedSpans);
                    break;

                case LocalFunctionStatementSyntax function when !IsAllowedReturnType(function.ReturnType):
                    AddUnsupportedType(node, function.ReturnType.ToString(), diagnostics, rejectedSpans);
                    break;

                case ParameterSyntax parameter when parameter.Type is null || !IsAllowedType(parameter.Type):
                    AddUnsupportedType(node, parameter.Type?.ToString() ?? "missing", diagnostics, rejectedSpans);
                    break;

                case BinaryExpressionSyntax binary when !AllowedBinaryExpressions.Contains(binary.Kind()):
                    AddUnsupportedOperator(node, binary.OperatorToken.Text, diagnostics, rejectedSpans);
                    break;

                case AssignmentExpressionSyntax assignment when !AllowedAssignments.Contains(assignment.Kind()):
                    AddUnsupportedOperator(node, assignment.OperatorToken.Text, diagnostics, rejectedSpans);
                    break;

                case PrefixUnaryExpressionSyntax prefix when !AllowedUnaryExpressions.Contains(prefix.Kind()):
                    AddUnsupportedOperator(node, prefix.OperatorToken.Text, diagnostics, rejectedSpans);
                    break;

                case PostfixUnaryExpressionSyntax postfix when !AllowedUnaryExpressions.Contains(postfix.Kind()):
                    AddUnsupportedOperator(node, postfix.OperatorToken.Text, diagnostics, rejectedSpans);
                    break;

                case LiteralExpressionSyntax literal when !AllowedLiteralExpressions.Contains(literal.Kind()):
                    AddUnsupported(
                        node,
                        "CL1001",
                        $"The literal '{literal}' is not supported.",
                        "Use number, string, character, true, or false literals.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case InvocationExpressionSyntax invocation when !IsAllowedInvocation(invocation, declaredFunctions):
                    AddUnsupported(
                        node,
                        "CL1002",
                        $"The call '{invocation.Expression}' is not supported.",
                        "Only Console.WriteLine, Console.ReadLine, and local functions declared in this snippet may be called.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case MemberAccessExpressionSyntax memberAccess when !IsAllowedConsoleMember(memberAccess):
                    AddUnsupported(
                        node,
                        "CL1002",
                        $"Member access '{memberAccess}' is not supported.",
                        "File, network, process, reflection, package, and general framework APIs are outside the learning subset.",
                        diagnostics,
                        rejectedSpans);
                    break;

                case IfStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
                    var depth = node.AncestorsAndSelf().Count(IsNestingStatement);
                    if (depth > maximumNestingDepth)
                    {
                        AddUnsupported(
                            node,
                            "CL1004",
                            $"Control-flow nesting depth {depth} exceeds the limit of {maximumNestingDepth}.",
                            "Reduce nested if statements or loops so the example remains easy to trace.",
                            diagnostics,
                            rejectedSpans);
                    }

                    break;
            }
        }

        return diagnostics;
    }

    private static bool IsAllowedStatement(StatementSyntax statement) => statement is
        BlockSyntax
        or LocalDeclarationStatementSyntax
        or ExpressionStatementSyntax
        or IfStatementSyntax
        or ForStatementSyntax
        or WhileStatementSyntax
        or DoStatementSyntax
        or ReturnStatementSyntax
        or LocalFunctionStatementSyntax
        or EmptyStatementSyntax;

    private static bool IsAllowedExpression(ExpressionSyntax expression) => expression is
        LiteralExpressionSyntax
        or IdentifierNameSyntax
        or BinaryExpressionSyntax
        or AssignmentExpressionSyntax
        or InvocationExpressionSyntax
        or MemberAccessExpressionSyntax
        or ParenthesizedExpressionSyntax
        or PrefixUnaryExpressionSyntax
        or PostfixUnaryExpressionSyntax
        or InterpolatedStringExpressionSyntax
        or ElementAccessExpressionSyntax
        or ArrayCreationExpressionSyntax
        or ImplicitArrayCreationExpressionSyntax
        or InitializerExpressionSyntax
        or OmittedArraySizeExpressionSyntax
        or TypeSyntax;

    private static bool IsAllowedType(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => AllowedPredefinedTypes.Contains(predefined.Keyword.Kind()),
        ArrayTypeSyntax array => IsAllowedType(array.ElementType),
        _ => false,
    };

    private static bool IsAllowedReturnType(TypeSyntax type) =>
        type is PredefinedTypeSyntax predefined
        && (predefined.Keyword.IsKind(SyntaxKind.VoidKeyword)
            || AllowedPredefinedTypes.Contains(predefined.Keyword.Kind()));

    private static bool IsAllowedInvocation(
        InvocationExpressionSyntax invocation,
        HashSet<string> declaredFunctions) => invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => IsAllowedConsoleMember(memberAccess),
            IdentifierNameSyntax identifier => declaredFunctions.Contains(identifier.Identifier.ValueText),
            _ => false,
        };

    private static bool IsAllowedConsoleMember(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Expression is IdentifierNameSyntax expression
        && expression.Identifier.ValueText == "Console"
        && memberAccess.Name.Identifier.ValueText is "WriteLine" or "ReadLine";

    private static bool IsNestingStatement(SyntaxNode node) =>
        node is IfStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax;

    private static void AddUnsupportedType(
        SyntaxNode node,
        string type,
        ICollection<CodeDiagnostic> diagnostics,
        ICollection<TextSpan> rejectedSpans) =>
        AddUnsupported(
            node,
            "CL1003",
            $"The type '{type}' is not supported.",
            "Use int, double, bool, string, char, or an array of one of those types.",
            diagnostics,
            rejectedSpans);

    private static void AddUnsupportedOperator(
        SyntaxNode node,
        string operatorText,
        ICollection<CodeDiagnostic> diagnostics,
        ICollection<TextSpan> rejectedSpans) =>
        AddUnsupported(
            node,
            "CL1001",
            $"The operator '{operatorText}' is not supported.",
            "Use basic arithmetic, comparison, Boolean, assignment, or increment/decrement operators.",
            diagnostics,
            rejectedSpans);

    private static void AddUnsupported(
        SyntaxNode node,
        string code,
        string message,
        string explanation,
        ICollection<CodeDiagnostic> diagnostics,
        ICollection<TextSpan> rejectedSpans)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        diagnostics.Add(new CodeDiagnostic(
            code,
            CodeDiagnosticCategory.Unsupported,
            CodeDiagnosticSeverity.Error,
            message,
            explanation,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            node.Span.Length));
        rejectedSpans.Add(node.Span);
    }

    private static string FriendlyName(SyntaxNode node) => node.Kind() switch
    {
        SyntaxKind.ClassDeclaration => "A class declaration",
        SyntaxKind.NamespaceDeclaration or SyntaxKind.FileScopedNamespaceDeclaration => "A namespace declaration",
        SyntaxKind.ObjectCreationExpression or SyntaxKind.ImplicitObjectCreationExpression => "Object creation",
        SyntaxKind.ForEachStatement => "A foreach loop",
        SyntaxKind.BreakStatement => "A break statement",
        SyntaxKind.ContinueStatement => "A continue statement",
        SyntaxKind.SwitchStatement or SyntaxKind.SwitchExpression => "A switch",
        SyntaxKind.TryStatement => "Try/catch",
        SyntaxKind.SimpleLambdaExpression or SyntaxKind.ParenthesizedLambdaExpression => "A lambda expression",
        _ => node.Kind().ToString(),
    };
}
