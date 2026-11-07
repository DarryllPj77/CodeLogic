using CodeLogic.Application.Analysis;

namespace CodeLogic.Web.Playground;

/// <summary>
/// Curated Playground examples in the classic program form, plus one top-level example. The first
/// entry is the default editor content and the Reset target. Lesson pages keep their own samples.
/// </summary>
public static class PlaygroundSnippets
{
    public const string DefaultSnippetId = "hello-world";

    public static IReadOnlyList<PlaygroundSnippet> All { get; } =
    [
        new(
            DefaultSnippetId,
            "Hello World (classic program)",
            "The standard C# program shape: using, namespace, class, and a static Main entry point.",
            ProgramTemplates.ClassicHelloWorld),
        new(
            "variables",
            "Variables hold typed values",
            "Declare variables with clear names and see each value change as Main runs.",
            ProgramTemplates.Classic("""
                int score = 5;
                string learnerName = "Mina";
                double average = 82.5;
                bool passed = average >= 75;

                score = score + 1;
                Console.WriteLine($"{learnerName}: {score} points");
                Console.WriteLine($"Passed: {passed}");
                """)),
        new(
            "if-else",
            "Decisions with if / else if / else",
            "Each condition is a True/False decision; exactly one branch runs.",
            ProgramTemplates.Classic("""
                int age = 16;

                if (age >= 18)
                {
                    Console.WriteLine("Adult");
                }
                else if (age >= 13)
                {
                    Console.WriteLine("Teen");
                }
                else
                {
                    Console.WriteLine("Child");
                }
                """)),
        new(
            "while",
            "Counting with while",
            "A while loop checks its condition before every repetition.",
            ProgramTemplates.Classic("""
                int countdown = 3;

                while (countdown > 0)
                {
                    Console.WriteLine(countdown);
                    countdown--;
                }

                Console.WriteLine("Liftoff!");
                """)),
        new(
            "for",
            "Repeating with for",
            "A for loop keeps its counter, condition, and step together.",
            ProgramTemplates.Classic("""
                int total = 0;

                for (int number = 1; number <= 4; number++)
                {
                    total += number;
                }

                Console.WriteLine($"Total: {total}");
                """)),
        new(
            "do-while",
            "do / while runs at least once",
            "The body runs first, then the condition decides whether to repeat.",
            ProgramTemplates.Classic("""
                int attempts = 0;

                do
                {
                    attempts++;
                    Console.WriteLine($"Attempt {attempts}");
                }
                while (attempts < 2);
                """)),
        new(
            "arrays-foreach",
            "Arrays and foreach",
            "Store several scores in one array, change an item by its index, then add them up with foreach.",
            ProgramTemplates.Classic("""
                int[] scores = { 90, 72, 85 };
                scores[1] = 75;

                int total = 0;
                foreach (int score in scores)
                {
                    total += score;
                }

                Console.WriteLine($"{scores.Length} scores, total {total}");
                """)),
        new(
            "static-method",
            "A static method with parameters",
            "Main calls a static method, passes arguments, and uses the value it returns.",
            ProgramTemplates.Classic(
                """
                int width = 3;
                int height = 4;
                int area = Area(width, height);
                Console.WriteLine($"Area: {area}");
                """,
                """
                static int Area(int width, int height)
                {
                    int result = width * height;
                    return result;
                }
                """)),
        new(
            "top-level",
            "Hello World (top-level mode)",
            "The same idea written as top-level statements, without a class or Main.",
            """
            string greeting = "Hello";
            Console.WriteLine($"{greeting} from top-level statements!");
            """),
    ];
}
