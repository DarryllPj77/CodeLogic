using System.Collections.ObjectModel;

namespace CodeLogic.Application.Analysis;

public enum SupportedFeatureSection
{
    ProgramStructure = 1,
    Statements = 2,
    NotSupportedYet = 3,
}

/// <summary>
/// One row of the "What's supported" view. For supported features, <see cref="Summary"/> explains the
/// feature. For unsupported ones it is the diagnostic message (what is wrong), and
/// <see cref="Why"/> and <see cref="Instead"/> complete the explanation. Every <see cref="Example"/> is
/// a complete program that can be loaded into the editor.
/// </summary>
public sealed record SupportedFeature(
    string Id,
    SupportedFeatureSection Section,
    string Title,
    string Summary,
    string Example,
    string? DiagnosticCode = null,
    string? Why = null,
    string? Instead = null)
{
    public bool IsSupported => Section != SupportedFeatureSection.NotSupportedYet;
}

public static class SupportedFeatureIds
{
    public const string UsingDirectives = "using-directives";
    public const string Namespace = "namespace";
    public const string Class = "class";
    public const string Main = "main";
    public const string TopLevel = "top-level";
    public const string Declarations = "declarations";
    public const string Assignments = "assignments";
    public const string ConsoleOutput = "console-output";
    public const string IfElse = "if-else";
    public const string While = "while";
    public const string For = "for";
    public const string DoWhile = "do-while";
    public const string Nesting = "nesting";
    public const string StaticMethods = "static-methods";
    public const string Arrays = "arrays";
    public const string ForEach = "foreach";
    public const string MultiDimensionalArrays = "multi-dimensional-arrays";
    public const string Switch = "switch";
    public const string SwitchExpression = "switch-expression";
    public const string Break = "break";
    public const string Continue = "continue";
    public const string ReturnInLoop = "return-in-loop";
    public const string LocalFunctions = "local-functions";
    public const string Records = "records";
    public const string Properties = "properties";
    public const string Generics = "generics";
    public const string Async = "async";
    public const string Linq = "linq";
    public const string Lambdas = "lambdas";
    public const string DotNetExecution = "dotnet-execution";
}

/// <summary>
/// The single source of truth for what CodeLogic accepts. The analyzer builds its "not supported yet"
/// diagnostics from these entries, and the Playground's "What's supported" view renders them, so the
/// two cannot drift. Tests run every example through the real analyzer, translator, and trace.
/// </summary>
public static class SupportedFeatureRegistry
{
    public static IReadOnlyList<SupportedFeature> All { get; } = new ReadOnlyCollection<SupportedFeature>(
    [
        // Program structure
        new(
            SupportedFeatureIds.UsingDirectives,
            SupportedFeatureSection.ProgramStructure,
            "using directives",
            "Allowed and kept in your source. They are ignored by the flowchart because they do not run.",
            ProgramTemplates.ClassicHelloWorld),
        new(
            SupportedFeatureIds.Namespace,
            SupportedFeatureSection.ProgramStructure,
            "namespace",
            "Wraps the program. Both the block form and the file-scoped form (namespace MyProgram;) work.",
            """
            using System;

            namespace MyProgram;

            class Program
            {
                static void Main(string[] args)
                {
                    Console.WriteLine("A file-scoped namespace");
                }
            }
            """),
        new(
            SupportedFeatureIds.Class,
            SupportedFeatureSection.ProgramStructure,
            "class",
            "One class holds Main and any static helper methods. A namespace is optional.",
            """
            class Program
            {
                static void Main()
                {
                    Console.WriteLine("A class without a namespace");
                }
            }
            """),
        new(
            SupportedFeatureIds.Main,
            SupportedFeatureSection.ProgramStructure,
            "static Main(string[] args)",
            "Exactly one entry point. It may return void or int. args is read-only and empty in the simulation.",
            """
            using System;

            class Program
            {
                static int Main(string[] args)
                {
                    Console.WriteLine("Main can return an exit code.");
                    return 0;
                }
            }
            """),
        new(
            SupportedFeatureIds.TopLevel,
            SupportedFeatureSection.ProgramStructure,
            "Top-level statements (secondary mode)",
            "Statements written without a class still work. CodeLogic detects this form automatically.",
            """
            int count = 3;
            Console.WriteLine($"Count: {count}");
            """),

        // Statements
        new(
            SupportedFeatureIds.Declarations,
            SupportedFeatureSection.Statements,
            "Variable declarations",
            "int, double, bool, string, and char variables, each with an initial value.",
            ProgramTemplates.Classic("""
                int age = 16;
                double price = 4.5;
                bool isMember = true;
                string name = "Ada";
                char grade = 'A';
                Console.WriteLine($"{name} is {age}");
                """)),
        new(
            SupportedFeatureIds.Assignments,
            SupportedFeatureSection.Statements,
            "Assignments and expression statements",
            "=, +=, -=, *=, /=, ++, and -- update a variable that is already declared.",
            ProgramTemplates.Classic("""
                int score = 10;
                score += 5;
                score++;
                score = score * 2;
                Console.WriteLine(score);
                """)),
        new(
            SupportedFeatureIds.ConsoleOutput,
            SupportedFeatureSection.Statements,
            "Console.WriteLine and Console.Write",
            "WriteLine ends the line; Write keeps writing on the same line.",
            ProgramTemplates.Classic("""
                Console.Write("Hello, ");
                Console.WriteLine("World!");
                Console.WriteLine("Done");
                """)),
        new(
            SupportedFeatureIds.IfElse,
            SupportedFeatureSection.Statements,
            "if / else if / else",
            "Each condition becomes a True/False decision in the flowchart. An else if chain does not add nesting depth.",
            ProgramTemplates.Classic("""
                int temperature = 18;

                if (temperature > 25)
                {
                    Console.WriteLine("Hot");
                }
                else if (temperature > 15)
                {
                    Console.WriteLine("Mild");
                }
                else
                {
                    Console.WriteLine("Cold");
                }
                """)),
        new(
            SupportedFeatureIds.While,
            SupportedFeatureSection.Statements,
            "while",
            "Repeats while the condition is true and exits through the condition.",
            ProgramTemplates.Classic("""
                int count = 1;

                while (count <= 3)
                {
                    Console.WriteLine(count);
                    count++;
                }
                """)),
        new(
            SupportedFeatureIds.For,
            SupportedFeatureSection.Statements,
            "for",
            "The initializer, condition, and iterator each appear as their own flowchart step.",
            ProgramTemplates.Classic("""
                for (int lap = 1; lap <= 3; lap++)
                {
                    Console.WriteLine($"Lap {lap}");
                }
                """)),
        new(
            SupportedFeatureIds.DoWhile,
            SupportedFeatureSection.Statements,
            "do / while",
            "Runs the body once, then checks the condition.",
            ProgramTemplates.Classic("""
                int tries = 0;

                do
                {
                    tries++;
                    Console.WriteLine($"Try {tries}");
                }
                while (tries < 3);
                """)),
        new(
            SupportedFeatureIds.Nesting,
            SupportedFeatureSection.Statements,
            "Nested control flow (up to depth 3)",
            "Ifs and loops can be nested three levels deep. Move deeper logic into a static method.",
            ProgramTemplates.Classic("""
                for (int row = 1; row <= 2; row++)
                {
                    for (int column = 1; column <= 2; column++)
                    {
                        if (row == column)
                        {
                            Console.WriteLine($"Diagonal {row}");
                        }
                    }
                }
                """)),
        new(
            SupportedFeatureIds.StaticMethods,
            SupportedFeatureSection.Statements,
            "Static methods called from Main",
            "Parameters and return values. Each method gets its own sub-flowchart, and the trace steps into every call. Recursion is not supported.",
            ProgramTemplates.Classic(
                """
                int area = Area(3, 4);
                Console.WriteLine($"Area: {area}");
                Greet("Ada");
                """,
                """
                static int Area(int width, int height)
                {
                    return width * height;
                }

                static void Greet(string name)
                {
                    Console.WriteLine($"Hello, {name}!");
                }
                """)),

        new(
            SupportedFeatureIds.Arrays,
            SupportedFeatureSection.Statements,
            "Arrays",
            "One-dimensional arrays of int, double, bool, string, or char: create them, read and change items by index (starting at 0), and use .Length. The trace shows every item.",
            ProgramTemplates.Classic("""
                int[] scores = { 90, 72, 85 };
                scores[1] = 75;
                scores[2] += 5;

                Console.WriteLine($"First score: {scores[0]}");
                Console.WriteLine($"Number of scores: {scores.Length}");

                string[] names = new string[2];
                names[0] = "Ada";
                Console.WriteLine(names[0]);
                """)),
        new(
            SupportedFeatureIds.ForEach,
            SupportedFeatureSection.Statements,
            "foreach",
            "Takes each item of an array (or each character of a string) in order. The loop variable is read-only; use a for loop with an index to change items.",
            ProgramTemplates.Classic("""
                int[] scores = { 90, 72, 85 };
                int total = 0;

                foreach (int score in scores)
                {
                    total += score;
                }

                Console.WriteLine($"Total: {total}");
                """)),
        new(
            SupportedFeatureIds.Switch,
            SupportedFeatureSection.Statements,
            "switch",
            "Each case with a literal value becomes one equality decision; default runs when no case matches. Every case ends with break (or return).",
            ProgramTemplates.Classic("""
                int day = 2;

                switch (day)
                {
                    case 1:
                        Console.WriteLine("Monday");
                        break;
                    case 2:
                    case 3:
                        Console.WriteLine("Midweek");
                        break;
                    default:
                        Console.WriteLine("Another day");
                        break;
                }
                """)),
        new(
            SupportedFeatureIds.Break,
            SupportedFeatureSection.Statements,
            "break",
            "Leaves the nearest loop or switch straight away. The flowchart draws a thick break arrow to where the program continues.",
            ProgramTemplates.Classic("""
                int count = 0;

                while (count < 10)
                {
                    count++;
                    if (count == 3)
                    {
                        break;
                    }
                }

                Console.WriteLine($"Stopped at {count}");
                """)),
        new(
            SupportedFeatureIds.Continue,
            SupportedFeatureSection.Statements,
            "continue",
            "Skips the rest of the loop body and goes to the next repetition (through the iterator in a for loop).",
            ProgramTemplates.Classic("""
                for (int number = 0; number < 5; number++)
                {
                    if (number == 2)
                    {
                        continue;
                    }

                    Console.WriteLine(number);
                }
                """)),
        new(
            SupportedFeatureIds.ReturnInLoop,
            SupportedFeatureSection.Statements,
            "return inside loops",
            "A return can leave a method from inside a loop; the flowchart connects it straight to the method's Return node.",
            ProgramTemplates.Classic(
                """
                Console.WriteLine(FirstEven(7));
                """,
                """
                static int FirstEven(int limit)
                {
                    for (int number = 1; number <= limit; number++)
                    {
                        if (number % 2 == 0)
                        {
                            return number;
                        }
                    }

                    return -1;
                }
                """)),

        // Not supported yet
        new(
            SupportedFeatureIds.SwitchExpression,
            SupportedFeatureSection.NotSupportedYet,
            "switch expressions",
            "switch expressions are not supported yet.",
            ProgramTemplates.Classic("""
                int day = 2;
                string name = day switch
                {
                    1 => "Monday",
                    _ => "Another day",
                };
                Console.WriteLine(name);
                """),
            "CL1001",
            "A switch expression hides several decisions inside one value, so there is no single statement to draw each path from.",
            "Use a switch statement that assigns the variable in each case, or if/else if/else."),
        new(
            SupportedFeatureIds.MultiDimensionalArrays,
            SupportedFeatureSection.NotSupportedYet,
            "Multi-dimensional and jagged arrays",
            "Multi-dimensional and jagged arrays are not supported yet.",
            ProgramTemplates.Classic("""
                int[,] grid = new int[2, 2];
                Console.WriteLine("A grid of numbers");
                """),
            "CL1003",
            "The trace shows each array as one row of items, so grids and arrays of arrays have no clear picture yet.",
            "Use a one-dimensional array, or one array per row."),
        new(
            SupportedFeatureIds.LocalFunctions,
            SupportedFeatureSection.NotSupportedYet,
            "Local functions",
            "Local functions are not supported yet.",
            ProgramTemplates.Classic("""
                int Double(int value)
                {
                    return value * 2;
                }

                Console.WriteLine(Double(4));
                """),
            "FL1002",
            "A function declared inside Main would need its own flowchart drawn inside Main's flowchart.",
            "Move it into the class as a static method. Rewrite as static method does this for you when the function does not use Main's variables."),
        new(
            SupportedFeatureIds.Records,
            SupportedFeatureSection.NotSupportedYet,
            "Records",
            "Records are not supported yet.",
            """
            using System;

            namespace MyProgram
            {
                record Point(int X, int Y);

                class Program
                {
                    static void Main(string[] args)
                    {
                        Console.WriteLine("Records hold data");
                    }
                }
            }
            """,
            "CL1001",
            "CodeLogic programs contain one class with static methods. Other type declarations are not modelled.",
            "Use separate variables, or pass the values to a static method as parameters."),
        new(
            SupportedFeatureIds.Properties,
            SupportedFeatureSection.NotSupportedYet,
            "Properties and fields",
            "Properties and fields are not supported yet.",
            """
            using System;

            class Program
            {
                static int Count { get; set; }

                static void Main(string[] args)
                {
                    Console.WriteLine("Properties live on the class");
                }
            }
            """,
            "CL1011",
            "Class-level state is shared by every method, and the step trace only models local variables.",
            "Declare variables inside Main and pass them to static methods as parameters."),
        new(
            SupportedFeatureIds.Generics,
            SupportedFeatureSection.NotSupportedYet,
            "Generics",
            "Generics are not supported yet.",
            ProgramTemplates.Classic("""
                List<int> numbers = new List<int>();
                Console.WriteLine("A generic list");
                """),
            "CL1003",
            "The trace models only int, double, bool, string, and char values and one-dimensional arrays of them.",
            "Use an array such as int[] instead of a List<int>."),
        new(
            SupportedFeatureIds.Async,
            SupportedFeatureSection.NotSupportedYet,
            "async and await",
            "async and await are not supported yet.",
            """
            using System;

            class Program
            {
                static async void Main(string[] args)
                {
                    Console.WriteLine("Async Main");
                }
            }
            """,
            "CL1001",
            "Asynchronous code can run out of order, which a step-by-step trace cannot show deterministically.",
            "Write ordinary statements that run one after another."),
        new(
            SupportedFeatureIds.Linq,
            SupportedFeatureSection.NotSupportedYet,
            "LINQ",
            "LINQ is not supported yet.",
            ProgramTemplates.Classic("""
                int[] numbers = { 1, 2, 3 };
                int count = numbers.Count(number => number > 1);
                Console.WriteLine(count);
                """),
            "CL1002",
            "LINQ hides its loop inside a library call, so there is no flow to draw or step through.",
            "Write the loop yourself with foreach or for and a counter variable."),
        new(
            SupportedFeatureIds.Lambdas,
            SupportedFeatureSection.NotSupportedYet,
            "Lambdas",
            "Lambdas are not supported yet.",
            ProgramTemplates.Classic("""
                var square = (int x) => x * x;
                Console.WriteLine(square(4));
                """),
            "CL1001",
            "A lambda is a function inside an expression, and its flow cannot be drawn there.",
            "Write a static method and call it by name."),
        new(
            SupportedFeatureIds.DotNetExecution,
            SupportedFeatureSection.NotSupportedYet,
            "Arbitrary .NET execution",
            "Calls to .NET libraries are not supported.",
            ProgramTemplates.Classic("""
                System.IO.File.WriteAllText("notes.txt", "hi");
                """),
            "CL1002",
            "CodeLogic never compiles or runs your code. It explains a small, safe subset, so file, network, process, and other framework APIs are off limits.",
            "Use Console.WriteLine or Console.Write for output, and static methods you write yourself for logic."),
    ]);

    private static readonly Dictionary<string, SupportedFeature> ById =
        All.ToDictionary(feature => feature.Id, StringComparer.Ordinal);

    public static SupportedFeature Get(string id) => ById.TryGetValue(id, out var feature)
        ? feature
        : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown supported-feature id.");

    public static bool TryGet(string? id, out SupportedFeature? feature)
    {
        feature = null;
        return id is not null && ById.TryGetValue(id, out feature);
    }

    public static IEnumerable<SupportedFeature> InSection(SupportedFeatureSection section) =>
        All.Where(feature => feature.Section == section);

    public static string SectionTitle(SupportedFeatureSection section) => section switch
    {
        SupportedFeatureSection.ProgramStructure => "Program structure",
        SupportedFeatureSection.Statements => "Statements",
        SupportedFeatureSection.NotSupportedYet => "Not supported yet",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown section."),
    };
}
