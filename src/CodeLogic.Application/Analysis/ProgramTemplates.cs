namespace CodeLogic.Application.Analysis;

/// <summary>The two program forms CodeLogic accepts, detected from the source rather than chosen.</summary>
public enum ProgramForm
{
    /// <summary>Blank or unparseable source: no form can be decided yet.</summary>
    Unknown = 0,

    /// <summary>using / namespace / class with a single static Main entry point.</summary>
    Classic = 1,

    /// <summary>Statements written directly in the file without a class (secondary mode).</summary>
    TopLevel = 2,

    /// <summary>Both a Main method and top-level statements: there would be two entry points.</summary>
    Mixed = 3,
}

/// <summary>Canonical starting programs and a helper that wraps statements in the classic shell.</summary>
public static class ProgramTemplates
{
    public const string ClassicHelloWorld = """
        using System;

        namespace MyProgram
        {
            class Program
            {
                static void Main(string[] args)
                {
                    Console.WriteLine("Hello World!");
                }
            }
        }
        """;

    public const string TopLevelHelloWorld = """
        Console.WriteLine("Hello World!");
        """;

    /// <summary>
    /// Builds a classic program whose Main contains <paramref name="mainBody"/>, followed by any
    /// <paramref name="staticMethods"/>. Both arguments are written without indentation.
    /// </summary>
    public static string Classic(string mainBody, string? staticMethods = null)
    {
        ArgumentNullException.ThrowIfNull(mainBody);

        var methods = string.IsNullOrWhiteSpace(staticMethods)
            ? string.Empty
            : "\n\n" + Indent(staticMethods, 8);
        return "using System;\n\n"
            + "namespace MyProgram\n{\n"
            + "    class Program\n    {\n"
            + "        static void Main(string[] args)\n        {\n"
            + Indent(mainBody, 12) + "\n"
            + "        }"
            + methods + "\n"
            + "    }\n}\n";
    }

    private static string Indent(string text, int spaces)
    {
        var prefix = new string(' ', spaces);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n').Split('\n');
        return string.Join('\n', lines.Select(line => line.Length == 0 ? line : prefix + line));
    }
}

public interface IProgramStructureService
{
    /// <summary>Detects whether the source is a classic program, top-level statements, or a mix.</summary>
    ProgramForm DetectForm(string? source);

    /// <summary>
    /// Moves the local function at the given 1-based position out of its method and into the class as a
    /// static method. Refuses (without changing anything) when the function uses the enclosing method's
    /// variables, because a static method could not see them.
    /// </summary>
    SourceRewriteResult RewriteLocalFunctionAsStaticMethod(string source, int line, int column);
}

public sealed record SourceRewriteResult(bool Succeeded, string? Source, string Message);
