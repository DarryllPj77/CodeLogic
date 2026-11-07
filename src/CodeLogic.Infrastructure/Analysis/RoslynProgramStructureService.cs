using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeLogic.Infrastructure.Analysis;

/// <summary>
/// Syntax-only program-shape operations. Nothing here compiles or runs the learner's code.
/// </summary>
public sealed class RoslynProgramStructureService : IProgramStructureService
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    public ProgramForm DetectForm(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > CodeAnalysisDefaults.MaximumCharacters)
        {
            return ProgramForm.Unknown;
        }

        return ProgramStructure.Read(CSharpSyntaxTree.ParseText(source, ParseOptions).GetRoot()).Form;
    }

    public SourceRewriteResult RewriteLocalFunctionAsStaticMethod(string source, int line, int column)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > CodeAnalysisDefaults.MaximumCharacters)
        {
            return Refuse("The source is longer than the editor limit.");
        }

        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        var root = tree.GetRoot();
        var text = tree.GetText();
        if (line < 1 || line > text.Lines.Count)
        {
            return Refuse("That position is outside the source.");
        }

        var lineInfo = text.Lines[line - 1];
        var position = Math.Clamp(lineInfo.Start + Math.Max(0, column - 1), lineInfo.Start, lineInfo.End);
        var function = root.FindToken(position).Parent?.AncestorsAndSelf()
            .OfType<LocalFunctionStatementSyntax>()
            .FirstOrDefault();
        if (function is null)
        {
            return Refuse("No local function was found at that position.");
        }

        var structure = ProgramStructure.Read(root);
        var enclosing = function.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (structure.Form != ProgramForm.Classic || structure.ProgramClass is null || enclosing?.Parent != structure.ProgramClass)
        {
            return Refuse("Rewriting needs the classic form: a class with static Main. In top-level mode there is no class to move the function into.");
        }

        var name = function.Identifier.ValueText;
        if (structure.ProgramClass.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == name))
        {
            return Refuse($"The class already has a method named {name}. Rename one of them first.");
        }

        var captured = CapturedNames(function, enclosing);
        if (captured.Length > 0)
        {
            return Refuse(
                $"{name} uses {string.Join(", ", captured)} from {enclosing.Identifier.ValueText}, which a static method cannot see. "
                + $"Add a parameter for each one and pass it in the call, then try again.");
        }

        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var methodIndent = LeadingWhitespace(text, enclosing.SpanStart);
        var methodText = Reindent(
            text,
            function,
            methodIndent,
            function.Modifiers.Any(SyntaxKind.StaticKeyword) ? string.Empty : "static ",
            newline);

        // Insert after the enclosing method first, because removal shifts earlier offsets only.
        var rewritten = source.Insert(enclosing.Span.End, newline + newline + methodText);
        var removal = LineRangeToRemove(text, function);
        rewritten = rewritten.Remove(removal.Start, removal.Length);

        var check = CSharpSyntaxTree.ParseText(rewritten, ParseOptions);
        if (check.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return Refuse("The rewrite would not produce valid C#, so the source was left unchanged.");
        }

        return new SourceRewriteResult(true, rewritten, $"{name} is now a static method of {structure.ProgramClass.Identifier.ValueText}.");
    }

    private static SourceRewriteResult Refuse(string message) => new(false, null, message);

    // Names the function reads that belong to the enclosing method (its parameters or locals) rather
    // than to the function itself.
    private static string[] CapturedNames(LocalFunctionStatementSyntax function, MethodDeclarationSyntax enclosing)
    {
        var outer = enclosing.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText)
            .Concat(enclosing.DescendantNodes()
                .Where(node => !function.Span.Contains(node.Span))
                .OfType<VariableDeclaratorSyntax>()
                .Select(variable => variable.Identifier.ValueText))
            .ToHashSet(StringComparer.Ordinal);
        var own = function.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText)
            .Concat(function.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(variable => variable.Identifier.ValueText))
            .ToHashSet(StringComparer.Ordinal);

        return function.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(identifier => identifier.Identifier.ValueText)
            .Where(identifier => outer.Contains(identifier) && !own.Contains(identifier))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Reindent(SourceText text, LocalFunctionStatementSyntax function, string indent, string prefix, string newline)
    {
        var originalIndent = LeadingWhitespace(text, function.SpanStart).Length;
        var lines = function.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new List<string> { indent + prefix + lines[0] };
        foreach (var line in lines.Skip(1))
        {
            var leading = line.Length - line.TrimStart(' ', '\t').Length;
            var trimmed = line[Math.Min(leading, originalIndent)..];
            result.Add(trimmed.Length == 0 ? string.Empty : indent + trimmed);
        }

        return string.Join(newline, result);
    }

    private static string LeadingWhitespace(SourceText text, int position)
    {
        var line = text.Lines.GetLineFromPosition(position);
        var prefix = text.ToString(TextSpan.FromBounds(line.Start, position));
        return string.IsNullOrWhiteSpace(prefix) ? prefix : string.Empty;
    }

    // Whole lines occupied by the function, plus one following blank line so no double gap remains.
    private static TextSpan LineRangeToRemove(SourceText text, LocalFunctionStatementSyntax function)
    {
        var firstLine = text.Lines.GetLineFromPosition(function.SpanStart);
        var lastLine = text.Lines.GetLineFromPosition(function.Span.End);
        var start = string.IsNullOrWhiteSpace(text.ToString(TextSpan.FromBounds(firstLine.Start, function.SpanStart)))
            ? firstLine.Start
            : function.SpanStart;
        var end = string.IsNullOrWhiteSpace(text.ToString(TextSpan.FromBounds(function.Span.End, lastLine.End)))
            ? lastLine.EndIncludingLineBreak
            : function.Span.End;

        var nextLineIndex = lastLine.LineNumber + 1;
        if (end == lastLine.EndIncludingLineBreak
            && nextLineIndex < text.Lines.Count
            && string.IsNullOrWhiteSpace(text.Lines[nextLineIndex].ToString()))
        {
            end = text.Lines[nextLineIndex].EndIncludingLineBreak;
        }

        return TextSpan.FromBounds(start, end);
    }
}
