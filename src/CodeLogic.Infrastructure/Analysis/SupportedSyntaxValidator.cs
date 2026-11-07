using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeLogic.Infrastructure.Analysis;

/// <summary>
/// Decides whether parsed source stays inside CodeLogic's learning subset. Every diagnostic says what
/// is wrong (Message), why (Explanation), and what to do instead (Suggestion). Features listed under
/// "Not supported yet" take their wording from <see cref="SupportedFeatureRegistry"/>.
/// </summary>
internal static class SupportedSyntaxValidator
{
    private const string GeneralWhy =
        "CodeLogic's flowchart and trace model only declarations, assignments, Console output, if/else, loops, and calls to your own static methods.";

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

    private static readonly HashSet<string> LinqMethodNames = new(StringComparer.Ordinal)
    {
        "Where", "Select", "SelectMany", "Count", "Sum", "Min", "Max", "Average", "Any", "All",
        "First", "FirstOrDefault", "Last", "LastOrDefault", "Single", "OrderBy", "OrderByDescending",
        "ThenBy", "GroupBy", "Take", "Skip", "Distinct", "ToList", "ToArray", "Aggregate", "Contains",
    };

    public static IReadOnlyList<CodeDiagnostic> Validate(
        SyntaxNode root,
        int maximumNestingDepth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNestingDepth);

        var report = new Reporter();
        var structure = ProgramStructure.Read(root);
        var localFunctionNames = root.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Select(function => function.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trivia.IsDirective)
            {
                report.Add(
                    trivia.GetStructure() ?? root,
                    "CL1005",
                    "Preprocessor directives such as #if, #define, or #pragma are not supported.",
                    "They change which code exists before it runs, which CodeLogic does not model.",
                    "Remove the directive and keep only the code you want to explain.");
            }
        }

        ValidateProgramShape(root, structure, report);

        foreach (var node in root.DescendantNodesAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (report.IsRejected(node))
            {
                continue;
            }

            ValidateNode(node, structure, localFunctionNames, maximumNestingDepth, report);
        }

        return report.Diagnostics;
    }

    private static void ValidateProgramShape(SyntaxNode root, ProgramStructure structure, Reporter report)
    {
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (!directive.StaticKeyword.IsKind(SyntaxKind.None)
                || directive.Alias is not null
                || !directive.GlobalKeyword.IsKind(SyntaxKind.None))
            {
                report.Add(
                    directive,
                    "CL1001",
                    "Only plain using directives such as using System; are supported.",
                    "using static, global using, and alias directives change how names resolve, which CodeLogic does not model.",
                    "Write using System; and call Console.WriteLine in full.",
                    SupportedFeatureIds.UsingDirectives);
            }
        }

        if (structure.Form == ProgramForm.Mixed)
        {
            var firstStatement = root.ChildNodes().OfType<GlobalStatementSyntax>().First();
            report.Add(
                firstStatement,
                "CL1010",
                "This file mixes top-level statements with a Main method.",
                "Both would be entry points, so the program would have two places to start.",
                "Move these statements into Main, or delete the class to keep only top-level statements.",
                SupportedFeatureIds.TopLevel);
        }

        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToArray();
        foreach (var extra in namespaces.Skip(1))
        {
            report.Add(
                extra,
                "CL1001",
                "Only one namespace is supported.",
                "A learning program needs a single namespace around its one class.",
                "Keep one namespace and put the class inside it.",
                SupportedFeatureIds.Namespace);
        }

        if (structure.Form == ProgramForm.Classic)
        {
            ValidateEntryPoint(root, structure, report);
        }

        foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (type == structure.ProgramClass || report.IsRejected(type))
            {
                continue;
            }

            if (type is RecordDeclarationSyntax)
            {
                report.Add(type, SupportedFeatureIds.Records);
            }
            else if (structure.Form == ProgramForm.TopLevel)
            {
                report.Add(
                    type,
                    "CL1001",
                    $"A {TypeKind(type)} next to top-level statements is not supported.",
                    "Top-level mode is for statements only; classes belong to the classic program form.",
                    "Keep only statements, or move everything into a class with a static Main method.",
                    SupportedFeatureIds.TopLevel);
            }
            else if (structure.ProgramClass is not null)
            {
                report.Add(
                    type,
                    "CL1001",
                    $"A second type declaration ({TypeKind(type)} {type.Identifier.ValueText}) is not supported yet.",
                    "CodeLogic programs contain one class that holds Main and its static methods.",
                    "Move the logic into static methods of the class that contains Main.",
                    SupportedFeatureIds.Class);
            }
        }

        foreach (var delegateDeclaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
        {
            report.Add(delegateDeclaration, SupportedFeatureIds.Lambdas);
        }

        if (structure.ProgramClass is not null)
        {
            ValidateClassMembers(structure, report);
            ValidateCalls(structure, report);
        }
    }

    private static void ValidateEntryPoint(SyntaxNode root, ProgramStructure structure, Reporter report)
    {
        if (structure.MainCandidates.Count == 0)
        {
            var anchor = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                ?? root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()
                ?? (SyntaxNode?)root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()
                ?? root;
            report.Add(
                anchor,
                "CL1008",
                "No Main method was found.",
                "A classic C# program starts running at static void Main(string[] args), so CodeLogic does not know where to begin.",
                "Add static void Main(string[] args) inside your class, or remove the class and use top-level statements.",
                SupportedFeatureIds.Main);
            return;
        }

        if (structure.MainCandidates.Count > 1)
        {
            foreach (var extra in structure.MainCandidates.Skip(1))
            {
                report.Add(
                    extra,
                    "CL1009",
                    $"There are {structure.MainCandidates.Count} Main methods.",
                    "A program can have only one entry point, so CodeLogic would not know where to start.",
                    "Keep one Main and rename or remove the others.",
                    SupportedFeatureIds.Main);
            }

            return;
        }

        var main = structure.Main!;
        if (main.Parent is not ClassDeclarationSyntax)
        {
            report.Add(
                main,
                "CL1014",
                "Main must be declared inside a class.",
                "CodeLogic reads the classic program as one class that holds Main and its static methods.",
                "Put static void Main(string[] args) inside class Program.",
                SupportedFeatureIds.Main);
            return;
        }

        if (main.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            report.Add(main, SupportedFeatureIds.Async);
            return;
        }

        var parameters = main.ParameterList.Parameters;
        var hasValidParameters = parameters.Count == 0
            || (parameters.Count == 1
                && parameters[0].Modifiers.Count == 0
                && parameters[0].Default is null
                && parameters[0].Type is ArrayTypeSyntax { ElementType: PredefinedTypeSyntax element, RankSpecifiers.Count: 1 }
                && element.Keyword.IsKind(SyntaxKind.StringKeyword));
        var returnsVoidOrInt = main.ReturnType is PredefinedTypeSyntax returnType
            && returnType.Keyword.Kind() is SyntaxKind.VoidKeyword or SyntaxKind.IntKeyword;

        if (!main.Modifiers.Any(SyntaxKind.StaticKeyword) || !hasValidParameters || !returnsVoidOrInt || main.Body is null)
        {
            report.Add(
                main,
                "CL1014",
                "This Main signature is not supported.",
                "The entry point must be static, return void or int, take no parameters or one string[] parameter, and have a block body.",
                "Use static void Main(string[] args) { ... } or static int Main(string[] args) { ... return 0; }.",
                SupportedFeatureIds.Main);
        }
    }

    private static void ValidateClassMembers(ProgramStructure structure, Reporter report)
    {
        foreach (var member in structure.ProgramClass!.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method when method == structure.Main:
                    break;
                case MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.AsyncKeyword):
                    report.Add(method, SupportedFeatureIds.Async);
                    break;
                case MethodDeclarationSyntax method when method.TypeParameterList is not null:
                    report.Add(method, SupportedFeatureIds.Generics);
                    break;
                case MethodDeclarationSyntax method when !method.Modifiers.Any(SyntaxKind.StaticKeyword):
                    report.Add(
                        method.Identifier,
                        method,
                        "CL1014",
                        $"The method {method.Identifier.ValueText} must be static.",
                        "Main is static and has no object to call instance methods on.",
                        $"Add the static keyword: static {method.ReturnType} {method.Identifier.ValueText}(...).",
                        SupportedFeatureIds.StaticMethods);
                    break;
                case MethodDeclarationSyntax method when method.Body is null:
                    report.Add(
                        method,
                        "CL1001",
                        $"The expression-bodied method {method.Identifier.ValueText} is not supported yet.",
                        "The flowchart draws each method from the statements in its block body.",
                        $"Use a block body: {{ return ...; }}.",
                        SupportedFeatureIds.StaticMethods);
                    break;
                case MethodDeclarationSyntax method when structure.Methods.TryGetValue(method.Identifier.ValueText, out var first) && first != method:
                    report.Add(
                        method.Identifier,
                        method,
                        "CL1001",
                        $"Another method named {method.Identifier.ValueText} already exists.",
                        "Overloads would make it unclear which method a call steps into.",
                        "Give each static method a different name.",
                        SupportedFeatureIds.StaticMethods);
                    break;
                case MethodDeclarationSyntax method:
                    ValidateMethodSignature(method, report);
                    break;
                case FieldDeclarationSyntax or PropertyDeclarationSyntax or EventFieldDeclarationSyntax or EventDeclarationSyntax:
                    report.Add(member, SupportedFeatureIds.Properties);
                    break;
                case BaseTypeDeclarationSyntax:
                    // Nested types are reported by the type-declaration pass.
                    break;
                default:
                    report.Add(
                        member,
                        "CL1011",
                        "Constructors and other class members are not supported yet.",
                        "The class is only a container for Main and static methods; objects are not created or traced.",
                        "Keep only static methods in the class.",
                        SupportedFeatureIds.Properties);
                    break;
            }
        }
    }

    private static void ValidateMethodSignature(MethodDeclarationSyntax method, Reporter report)
    {
        if (!IsAllowedReturnType(method.ReturnType))
        {
            report.AddUnsupportedType(method.ReturnType, method.ReturnType);
            return;
        }

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Modifiers.Count > 0 || parameter.Default is not null)
            {
                report.Add(
                    parameter,
                    "CL1001",
                    $"The parameter '{parameter}' is not supported yet.",
                    "ref, out, params, and default values change how arguments flow, which the trace does not model.",
                    "Use plain parameters such as int count, and pass every argument explicitly.",
                    SupportedFeatureIds.StaticMethods);
            }
        }
    }

    private static void ValidateCalls(ProgramStructure structure, Reporter report)
    {
        var callGraph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var bodies = structure.Methods.Values.Append(structure.Main!).Where(method => method.Body is not null);
        foreach (var method in bodies)
        {
            var callees = new HashSet<string>(StringComparer.Ordinal);
            foreach (var invocation in method.Body!.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = structure.CalledMethodName(invocation);
                if (name is null)
                {
                    if (invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "Main" })
                    {
                        name = "Main";
                    }
                    else
                    {
                        continue;
                    }
                }

                callees.Add(name);
                if (name != "Main"
                    && structure.Methods[name].ParameterList.Parameters.Count is var expected
                    && invocation.ArgumentList.Arguments.Count != expected)
                {
                    report.Add(
                        invocation,
                        "CL1015",
                        $"{name} expects {expected} argument(s) but this call passes {invocation.ArgumentList.Arguments.Count}.",
                        "Each argument is copied into the parameter in the same position.",
                        $"Call it as {ProgramStructure.Signature(structure.Methods[name])} with one value per parameter.",
                        SupportedFeatureIds.StaticMethods);
                }
            }

            callGraph[method.Identifier.ValueText] = callees;
        }

        foreach (var method in bodies)
        {
            if (ReachesItself(method.Identifier.ValueText, callGraph))
            {
                report.Add(
                    method.Identifier,
                    method,
                    "CL1013",
                    $"Recursion is not supported yet: {method.Identifier.ValueText} eventually calls itself.",
                    "A method that calls itself makes the trace's call stack grow without a fixed shape to draw.",
                    "Use a loop instead of the recursive call.",
                    SupportedFeatureIds.StaticMethods);
            }
        }
    }

    private static bool ReachesItself(string start, IReadOnlyDictionary<string, HashSet<string>> callGraph)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(callGraph.GetValueOrDefault(start) ?? []);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == start)
            {
                return true;
            }

            if (visited.Add(current))
            {
                foreach (var next in callGraph.GetValueOrDefault(current) ?? [])
                {
                    pending.Push(next);
                }
            }
        }

        return false;
    }

    private static void ValidateNode(
        SyntaxNode node,
        ProgramStructure structure,
        HashSet<string> localFunctionNames,
        int maximumNestingDepth,
        Reporter report)
    {
        switch (node)
        {
            // Program shell: checked by ValidateProgramShape.
            case CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or UsingDirectiveSyntax
                or GlobalStatementSyntax or AttributeListSyntax:
                return;
            case BaseTypeDeclarationSyntax or MethodDeclarationSyntax or ParameterListSyntax
                or TypeParameterListSyntax or BaseListSyntax:
                return;
            case NameSyntax when node.Parent is BaseNamespaceDeclarationSyntax or UsingDirectiveSyntax:
                return;

            case LocalFunctionStatementSyntax:
                report.Add(node, SupportedFeatureIds.LocalFunctions);
                return;
            case SwitchStatementSyntax or SwitchExpressionSyntax:
                report.Add(node, SupportedFeatureIds.Switch);
                return;
            case BreakStatementSyntax:
                report.Add(node, SupportedFeatureIds.Break);
                return;
            case ContinueStatementSyntax:
                report.Add(node, SupportedFeatureIds.Continue);
                return;
            case ReturnStatementSyntax when node.Ancestors()
                .TakeWhile(ancestor => ancestor is not (MethodDeclarationSyntax or CompilationUnitSyntax))
                .Any(IsLoop):
                report.Add(node, SupportedFeatureIds.ReturnInLoop);
                return;
            case AwaitExpressionSyntax:
                report.Add(node, SupportedFeatureIds.Async);
                return;
            case LambdaExpressionSyntax or AnonymousMethodExpressionSyntax:
                report.Add(node, SupportedFeatureIds.Lambdas);
                return;
            case QueryExpressionSyntax:
                report.Add(node, SupportedFeatureIds.Linq);
                return;
            case ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax:
                report.Add(
                    node,
                    "CL1001",
                    "Creating objects with new is not supported yet.",
                    "Objects carry hidden state that the step trace does not model.",
                    "Use the built-in types int, double, bool, string, and char.",
                    SupportedFeatureIds.Declarations);
                return;

            case StatementSyntax statement when !IsAllowedStatement(statement):
                report.Add(
                    node,
                    "CL1001",
                    $"{FriendlyName(node)} is not supported yet.",
                    GeneralWhy,
                    StatementAlternative(node),
                    SupportedFeatureIds.Declarations);
                return;

            case ExpressionSyntax expression when !IsAllowedExpression(expression):
                report.Add(
                    node,
                    "CL1001",
                    $"{FriendlyName(node)} is not supported yet.",
                    GeneralWhy,
                    "Rewrite this expression with literals, variables, basic operators, or a call to one of your static methods.",
                    SupportedFeatureIds.Assignments);
                return;

            case VariableDeclarationSyntax declaration when !IsAllowedType(declaration.Type):
                if (declaration.Variables.Any(variable => variable.Initializer?.Value is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax))
                {
                    report.Add(declaration, SupportedFeatureIds.Lambdas);
                }
                else
                {
                    report.AddUnsupportedType(declaration, declaration.Type);
                }

                return;

            case ParameterSyntax parameter when parameter.Type is null || !IsAllowedType(parameter.Type):
                report.AddUnsupportedType(node, parameter.Type);
                return;

            case BinaryExpressionSyntax binary when !AllowedBinaryExpressions.Contains(binary.Kind()):
                report.AddUnsupportedOperator(node, binary.OperatorToken.Text);
                return;
            case AssignmentExpressionSyntax assignment when !AllowedAssignments.Contains(assignment.Kind()):
                report.AddUnsupportedOperator(node, assignment.OperatorToken.Text);
                return;
            case PrefixUnaryExpressionSyntax prefix when !AllowedUnaryExpressions.Contains(prefix.Kind()):
                report.AddUnsupportedOperator(node, prefix.OperatorToken.Text);
                return;
            case PostfixUnaryExpressionSyntax postfix when !AllowedUnaryExpressions.Contains(postfix.Kind()):
                report.AddUnsupportedOperator(node, postfix.OperatorToken.Text);
                return;

            case LiteralExpressionSyntax literal when !AllowedLiteralExpressions.Contains(literal.Kind()):
                report.Add(
                    node,
                    "CL1001",
                    $"The literal '{literal}' is not supported yet.",
                    "The trace models only number, string, character, and true/false values.",
                    "Use a number, string, character, true, or false literal.",
                    SupportedFeatureIds.Declarations);
                return;

            case InvocationExpressionSyntax invocation:
                ValidateInvocation(invocation, structure, localFunctionNames, report);
                return;

            case MemberAccessExpressionSyntax memberAccess when !IsAllowedConsoleMember(memberAccess):
                ReportLibraryUse(memberAccess, memberAccess.Name.Identifier.ValueText, report);
                return;

            case IfStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
                var depth = NestingDepth(node);
                if (depth > maximumNestingDepth)
                {
                    report.Add(
                        node,
                        "CL1004",
                        $"This code is nested {depth} levels deep; the limit is {maximumNestingDepth}.",
                        "Every extra level of ifs and loops makes the flowchart and the step trace harder to follow.",
                        "Extract the innermost block into a static method and call it from here.",
                        SupportedFeatureIds.Nesting);
                }

                return;
        }
    }

    private static void ValidateInvocation(
        InvocationExpressionSyntax invocation,
        ProgramStructure structure,
        HashSet<string> localFunctionNames,
        Reporter report)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess when IsAllowedConsoleMember(memberAccess):
                return;
            case MemberAccessExpressionSyntax memberAccess:
                ReportLibraryUse(invocation, memberAccess.Name.Identifier.ValueText, report);
                return;
            case IdentifierNameSyntax identifier when structure.Methods.ContainsKey(identifier.Identifier.ValueText):
                return;
            case IdentifierNameSyntax identifier when localFunctionNames.Contains(identifier.Identifier.ValueText):
                // The local function itself is reported once with the rewrite suggestion.
                return;
            case IdentifierNameSyntax identifier when identifier.Identifier.ValueText == "Main":
                // Calling Main is reported as recursion by ValidateCalls.
                return;
            case IdentifierNameSyntax identifier:
                report.Add(
                    invocation,
                    "CL1002",
                    $"The call '{identifier.Identifier.ValueText}(...)' does not match a static method in this program.",
                    "Only Console.WriteLine, Console.Write, and static methods declared in your class can be called.",
                    structure.Form == ProgramForm.Classic
                        ? $"Declare static void {identifier.Identifier.ValueText}(...) in the class, or check the spelling."
                        : "Switch to the classic form (class Program with static Main) to declare static methods.",
                    SupportedFeatureIds.StaticMethods);
                return;
            default:
                report.Add(invocation, SupportedFeatureIds.DotNetExecution);
                return;
        }
    }

    private static void ReportLibraryUse(SyntaxNode node, string memberName, Reporter report) =>
        report.Add(node, LinqMethodNames.Contains(memberName) ? SupportedFeatureIds.Linq : SupportedFeatureIds.DotNetExecution);

    /// <summary>Counts enclosing ifs and loops. An else-if continues its chain rather than nesting deeper.</summary>
    private static int NestingDepth(SyntaxNode node) => node.AncestorsAndSelf()
        .TakeWhile(ancestor => ancestor is not (MethodDeclarationSyntax or CompilationUnitSyntax))
        .Count(ancestor => IsLoop(ancestor) || (ancestor is IfStatementSyntax && ancestor.Parent is not ElseClauseSyntax));

    private static bool IsLoop(SyntaxNode node) =>
        node is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax or ForEachStatementSyntax;

    private static bool IsAllowedStatement(StatementSyntax statement) => statement is
        BlockSyntax
        or LocalDeclarationStatementSyntax
        or ExpressionStatementSyntax
        or IfStatementSyntax
        or ForStatementSyntax
        or WhileStatementSyntax
        or DoStatementSyntax
        or ReturnStatementSyntax
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

    private static bool IsAllowedConsoleMember(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Expression is IdentifierNameSyntax expression
        && expression.Identifier.ValueText == "Console"
        && memberAccess.Name.Identifier.ValueText is "WriteLine" or "Write" or "ReadLine";

    private static string TypeKind(BaseTypeDeclarationSyntax type) => type switch
    {
        ClassDeclarationSyntax => "class",
        StructDeclarationSyntax => "struct",
        InterfaceDeclarationSyntax => "interface",
        EnumDeclarationSyntax => "enum",
        RecordDeclarationSyntax => "record",
        _ => "type",
    };

    private static string StatementAlternative(SyntaxNode node) => node.Kind() switch
    {
        SyntaxKind.ForEachStatement => "Use a for loop with an index variable instead.",
        SyntaxKind.TryStatement => "Remove try/catch; the supported statements cannot throw exceptions you need to catch.",
        SyntaxKind.GotoStatement or SyntaxKind.LabeledStatement => "Use if/else or a loop condition instead of jumping.",
        SyntaxKind.ThrowStatement => "Print a message with Console.WriteLine instead of throwing.",
        _ => "Rewrite it with the statements listed under What's supported.",
    };

    private static string FriendlyName(SyntaxNode node) => node.Kind() switch
    {
        SyntaxKind.ForEachStatement => "A foreach loop",
        SyntaxKind.TryStatement => "Try/catch",
        SyntaxKind.ThrowStatement or SyntaxKind.ThrowExpression => "throw",
        SyntaxKind.GotoStatement => "goto",
        SyntaxKind.LockStatement => "lock",
        SyntaxKind.UsingStatement => "A using statement",
        SyntaxKind.YieldReturnStatement or SyntaxKind.YieldBreakStatement => "yield",
        SyntaxKind.ConditionalExpression => "The ?: conditional operator",
        SyntaxKind.CastExpression => "A type cast",
        SyntaxKind.TypeOfExpression => "typeof",
        SyntaxKind.IsPatternExpression or SyntaxKind.IsExpression => "The is operator",
        SyntaxKind.ElementAccessExpression => "Indexing",
        _ => node.Kind().ToString(),
    };

    /// <summary>Collects diagnostics and remembers rejected spans so nested nodes are not reported twice.</summary>
    private sealed class Reporter
    {
        private readonly List<CodeDiagnostic> diagnostics = [];
        private readonly List<TextSpan> rejectedSpans = [];

        public IReadOnlyList<CodeDiagnostic> Diagnostics => diagnostics;

        public bool IsRejected(SyntaxNode node) => rejectedSpans.Any(span => span.Contains(node.Span));

        public void Add(SyntaxNode node, string featureId)
        {
            var feature = SupportedFeatureRegistry.Get(featureId);
            Add(node, feature.DiagnosticCode!, feature.Summary, feature.Why!, feature.Instead!, feature.Id);
        }

        public void Add(SyntaxNode node, string code, string message, string why, string instead, string? helpTopicId = null) =>
            Add(node.GetLocation(), node, code, message, why, instead, helpTopicId);

        public void Add(SyntaxToken anchor, SyntaxNode rejected, string code, string message, string why, string instead, string? helpTopicId = null) =>
            Add(anchor.GetLocation(), rejected, code, message, why, instead, helpTopicId);

        public void AddUnsupportedType(SyntaxNode node, TypeSyntax? type)
        {
            if (type?.DescendantNodesAndSelf().Any(part => part is GenericNameSyntax) == true)
            {
                Add(node, SupportedFeatureIds.Generics);
                return;
            }

            var name = type?.ToString() ?? "missing";
            Add(
                node,
                "CL1003",
                name == "var" ? "var is not supported yet." : $"The type '{name}' is not supported yet.",
                "The trace models only int, double, bool, string, and char values.",
                name == "var"
                    ? "Write the type explicitly, for example int count = 0;."
                    : "Use int, double, bool, string, or char instead.",
                SupportedFeatureIds.Declarations);
        }

        public void AddUnsupportedOperator(SyntaxNode node, string operatorText) => Add(
            node,
            "CL1001",
            $"The operator '{operatorText}' is not supported yet.",
            "Only arithmetic, comparison, logical, assignment, and ++/-- operators are modelled.",
            "Rewrite the expression with those operators.",
            SupportedFeatureIds.Assignments);

        private void Add(Location location, SyntaxNode rejected, string code, string message, string why, string instead, string? helpTopicId)
        {
            var lineSpan = location.GetLineSpan();
            diagnostics.Add(new CodeDiagnostic(
                code,
                CodeDiagnosticCategory.Unsupported,
                CodeDiagnosticSeverity.Error,
                message,
                why,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1,
                location.SourceSpan.Length)
            {
                Suggestion = instead,
                HelpTopicId = helpTopicId,
            });
            rejectedSpans.Add(rejected.Span);
        }
    }
}
