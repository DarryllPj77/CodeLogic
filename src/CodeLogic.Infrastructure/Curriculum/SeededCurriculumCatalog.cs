using System.Globalization;

using CodeLogic.Application.Curriculum;
using CodeLogic.Domain;
using CodeLogic.Domain.Curriculum;
using CodeLogic.Domain.Practice;

namespace CodeLogic.Infrastructure.Curriculum;

public sealed class SeededCurriculumCatalog : ICurriculumCatalog
{
    private static readonly CurriculumManifest Manifest = CreateManifest();

    public CurriculumManifest GetManifest() => Manifest;

    private static CurriculumManifest CreateManifest()
    {
        var topics = CreateTopics();
        var topicsBySlug = topics.ToDictionary(topic => topic.Slug, StringComparer.Ordinal);
        var lessonSeeds = CreateLessonSeeds();
        var lessons = lessonSeeds
            .Select(seed => CreateLesson(seed, topicsBySlug[seed.TopicSlug]))
            .ToArray();

        var manifest = new CurriculumManifest(
            schemaVersion: 1,
            CreateSafeSnippetPolicy(),
            topics,
            lessons);

        var errors = CurriculumManifestValidator.Validate(manifest);
        if (errors.Count != 0)
        {
            throw new InvalidOperationException(
                $"The seeded curriculum manifest is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return manifest;
    }

    private static Topic[] CreateTopics() =>
    [
        CreateTopic(1, "variables-and-types", "Variables and types", "Store information using clear names and suitable C# types."),
        CreateTopic(2, "operators", "Operators", "Combine and compare values with arithmetic, comparison, and Boolean operators."),
        CreateTopic(3, "input-and-output", "Input and output", "Read simple text input and present useful console output."),
        CreateTopic(4, "sequence", "Sequence", "Trace statements in the order a program evaluates them."),
        CreateTopic(5, "branching", "Branching", "Choose one path with conditions and if/else statements."),
        CreateTopic(6, "loops", "Loops", "Repeat bounded work and recognize loop control parts."),
        CreateTopic(7, "methods", "Methods", "Name reusable behavior with parameters and return values."),
        CreateTopic(8, "algorithms", "Algorithms", "Turn a small problem into ordered, testable steps."),
        CreateTopic(9, "debugging", "Debugging", "Observe a program carefully and locate simple mistakes."),
    ];

    private static LessonSeed[] CreateLessonSeeds() =>
    [
        new(
            Order: 1,
            TopicSlug: "variables-and-types",
            Status: CurriculumLessonStatus.Complete,
            Difficulty: CurriculumDifficulty.BeginnerOne,
            Title: "Variables hold typed values",
            Objective: "Declare variables with clear names and explain the difference between a variable's type and value.",
            SampleCode: """
                int score = 5;
                string learnerName = "Mina";

                score = score + 1;
                Console.WriteLine($"{learnerName}: {score}");
                """,
            Sections:
            [
                new("Names, types, and values", "A variable gives a value a meaningful name. Its type describes which kind of value C# will allow."),
                new("Declare, then update", "A declaration creates the variable. A later assignment can replace its value with another value of the same compatible type."),
            ],
            Quiz: new(
                QuizResponseKind.MultipleChoice,
                "Identify the type that stores a whole number.",
                "int",
                AnswerNormalization.Exact,
                "The int type stores whole numbers such as 5 or 42.",
                ["int", "double", "string"]),
            ExerciseTitle: "Choose a whole-number type",
            ExercisePrompt: "Which C# type should store the number of completed lessons?",
            AllowedConcepts:
            [
                "local-variable-declaration",
                "literal",
                "assignment",
                "arithmetic-expression",
                "console-write-line",
                "sequential-statements",
            ]),
        new(
            Order: 2,
            TopicSlug: "operators",
            Status: CurriculumLessonStatus.Complete,
            Difficulty: CurriculumDifficulty.BeginnerOne,
            Title: "Operators create new information",
            Objective: "Use arithmetic, comparison, and Boolean operators to calculate values and answer true-or-false questions.",
            SampleCode: """
                int total = 6 + 4;
                bool isReady = total >= 10 && total != 0;

                Console.WriteLine(isReady);
                """,
            Sections:
            [
                new("Calculate with arithmetic", "Arithmetic operators such as + and - produce a new numeric value from existing values."),
                new("Compare and combine conditions", "Comparison operators produce true or false. Boolean operators such as && combine those results."),
            ],
            Quiz: new(
                QuizResponseKind.ValueInput,
                "Predict the Boolean result after evaluating the sample expression.",
                "true",
                AnswerNormalization.TrimAndIgnoreCase,
                "The total is 10, so both comparisons are true and true && true evaluates to true.",
                []),
            ExerciseTitle: "Predict an operator result",
            ExercisePrompt: "What value is stored in isReady after the sample runs?",
            AllowedConcepts:
            [
                "local-variable-declaration",
                "literal",
                "arithmetic-expression",
                "comparison-expression",
                "boolean-expression",
                "console-write-line",
                "sequential-statements",
            ]),
        CreateDraft(
            order: 3,
            topicSlug: "input-and-output",
            difficulty: CurriculumDifficulty.BeginnerOne,
            title: "Communicate with a console program",
            objective: "Read a short text value and display a clear response.",
            sampleCode: "string? name = Console.ReadLine();\nConsole.WriteLine($\"Hello, {name}!\");",
            outline: "Introduce console input, nullable text, and readable output without file or network access.",
            expectedAnswer: "Hello, Ana!",
            exercisePrompt: "If the input is Ana, what line does the program display?",
            allowedConcepts: ["local-variable-declaration", "console-read-line", "console-write-line", "sequential-statements"]),
        CreateDraft(
            order: 4,
            topicSlug: "sequence",
            difficulty: CurriculumDifficulty.BeginnerOne,
            title: "Follow statements in sequence",
            objective: "Trace a short program from the first statement to the last.",
            sampleCode: "int value = 2;\nvalue = value + 3;\nConsole.WriteLine(value);",
            outline: "Make the default top-to-bottom execution order visible before introducing branches or loops.",
            expectedAnswer: "5",
            exercisePrompt: "What value is displayed after the three statements run in order?",
            allowedConcepts: ["local-variable-declaration", "literal", "assignment", "arithmetic-expression", "console-write-line", "sequential-statements"]),
        CreateDraft(
            order: 5,
            topicSlug: "branching",
            difficulty: CurriculumDifficulty.BeginnerTwo,
            title: "Choose a path with if and else",
            objective: "Trace a condition and explain why one branch runs while another is skipped.",
            sampleCode: "int score = 8;\nif (score >= 5)\n{\n    Console.WriteLine(\"Pass\");\n}\nelse\n{\n    Console.WriteLine(\"Try again\");\n}",
            outline: "Identify the condition, true path, false path, and the single path selected for a given value.",
            expectedAnswer: "Pass",
            exercisePrompt: "Which word is displayed when score is 8?",
            allowedConcepts: ["local-variable-declaration", "literal", "comparison-expression", "if-else", "console-write-line"]),
        CreateDraft(
            order: 6,
            topicSlug: "loops",
            difficulty: CurriculumDifficulty.BeginnerTwo,
            title: "Repeat bounded work with loops",
            objective: "Identify loop initialization, condition, body, and update while tracing a small fixed number of repetitions.",
            sampleCode: "for (int count = 1; count <= 3; count++)\n{\n    Console.WriteLine(count);\n}",
            outline: "Start with a bounded for loop, then compare its parts with a simple while loop.",
            expectedAnswer: "3",
            exercisePrompt: "How many times does the loop body run?",
            allowedConcepts: ["local-variable-declaration", "literal", "comparison-expression", "for-loop", "console-write-line"]),
        CreateDraft(
            order: 7,
            topicSlug: "methods",
            difficulty: CurriculumDifficulty.BeginnerTwo,
            title: "Name reusable behavior with methods",
            objective: "Call a simple method and connect its parameters and return value to the result.",
            sampleCode: "int Add(int left, int right)\n{\n    return left + right;\n}\n\nint total = Add(2, 3);",
            outline: "Separate the method declaration from the call and trace values into parameters and back through return.",
            expectedAnswer: "5",
            exercisePrompt: "What value is stored in total?",
            allowedConcepts: ["method-declaration", "method-call", "return-statement", "local-variable-declaration", "arithmetic-expression", "literal"]),
        CreateDraft(
            order: 8,
            topicSlug: "algorithms",
            difficulty: CurriculumDifficulty.BeginnerThree,
            title: "Turn a problem into ordered steps",
            objective: "Describe and trace a small algorithm that finds the largest value in a short array.",
            sampleCode: "int[] values = [3, 7, 4];\nint largest = values[0];\n\nfor (int index = 1; index < values.Length; index++)\n{\n    if (values[index] > largest)\n    {\n        largest = values[index];\n    }\n}",
            outline: "Combine sequence, a bounded loop, indexing, and a branch into one understandable algorithm.",
            expectedAnswer: "7",
            exercisePrompt: "What value remains in largest after the algorithm finishes?",
            allowedConcepts: ["array-declaration", "array-indexing", "local-variable-declaration", "assignment", "comparison-expression", "for-loop", "if-else", "literal"]),
        CreateDraft(
            order: 9,
            topicSlug: "debugging",
            difficulty: CurriculumDifficulty.BeginnerThree,
            title: "Debug by observing expected and actual values",
            objective: "Locate a simple mistake by comparing the intended result with values produced at each step.",
            sampleCode: "int width = 4;\nint height = 3;\nint area = width + height; // Bug: should multiply\nConsole.WriteLine(area);",
            outline: "Use a small incorrect expression to practise reading values, forming a hypothesis, and making one focused correction.",
            expectedAnswer: "*",
            exercisePrompt: "Which operator should replace + to calculate rectangle area?",
            allowedConcepts: ["local-variable-declaration", "literal", "arithmetic-expression", "comment", "console-write-line", "sequential-statements"]),
    ];

    private static LessonSeed CreateDraft(
        int order,
        string topicSlug,
        CurriculumDifficulty difficulty,
        string title,
        string objective,
        string sampleCode,
        string outline,
        string expectedAnswer,
        string exercisePrompt,
        string[] allowedConcepts) =>
        new(
            order,
            topicSlug,
            CurriculumLessonStatus.Draft,
            difficulty,
            title,
            objective,
            sampleCode,
            [new SectionSeed("Draft outline", outline)],
            new QuizSeed(
                QuizResponseKind.ValueInput,
                $"Check the learner can predict one result from {title.ToLowerInvariant()}.",
                expectedAnswer,
                AnswerNormalization.TrimAndIgnoreCase,
                "The final explanation will be reviewed when the draft lesson is completed.",
                []),
            $"Check {topicSlug}",
            exercisePrompt,
            allowedConcepts);

    private static CurriculumLesson CreateLesson(LessonSeed seed, Topic topic)
    {
        var lessonId = new LessonId(SeedGuid(category: 2, seed.Order));
        var sections = seed.Sections
            .Select((section, index) => new LessonSection(
                new LessonSectionId(SeedGuid(category: 3, (seed.Order * 100) + index + 1)),
                section.Heading,
                section.Content,
                index + 1))
            .ToArray();
        var lesson = new Lesson(
            lessonId,
            topic.Id,
            seed.Title,
            seed.Objective,
            sections);
        var exerciseKind = seed.Quiz.ResponseKind == QuizResponseKind.MultipleChoice
            ? ExerciseKind.MultipleChoice
            : ExerciseKind.ValueInput;
        var exercise = new Exercise(
            new ExerciseId(SeedGuid(category: 4, seed.Order)),
            lessonId,
            seed.ExerciseTitle,
            seed.ExercisePrompt,
            exerciseKind,
            order: 1);
        var quizCriteria = new QuizCriteria(
            seed.Quiz.ResponseKind,
            seed.Quiz.AssessmentGoal,
            seed.Quiz.ExpectedAnswer,
            seed.Quiz.Normalization,
            seed.Quiz.Explanation,
            Array.AsReadOnly(seed.Quiz.AnswerOptions.ToArray()));

        return new CurriculumLesson(
            seed.Order,
            seed.Status,
            seed.Difficulty,
            lesson,
            exercise,
            seed.SampleCode,
            Array.AsReadOnly(seed.AllowedConcepts.ToArray()),
            quizCriteria);
    }

    private static Topic CreateTopic(int order, string slug, string title, string description) =>
        new(new TopicId(SeedGuid(category: 1, order)), slug, title, description);

    private static SafeSnippetPolicy CreateSafeSnippetPolicy() =>
        new(
            MaximumCharacters: 1_200,
            MaximumNestingDepth: 3,
            ExecutionPolicy: "static-analysis-only",
            AllowedConstructs:
            [
                "local-variable-declaration",
                "literal",
                "assignment",
                "arithmetic-expression",
                "comparison-expression",
                "boolean-expression",
                "console-read-line",
                "console-write-line",
                "sequential-statements",
                "if-else",
                "for-loop",
                "while-loop",
                "method-declaration",
                "method-call",
                "return-statement",
                "array-declaration",
                "array-indexing",
                "comment",
            ],
            ForbiddenCapabilities:
            [
                "file-system",
                "network-access",
                "process-launch",
                "reflection",
                "unsafe-code",
                "dynamic-code-generation",
                "package-loading",
                "threading",
            ]);

    private static Guid SeedGuid(int category, int value)
    {
        var text = FormattableString.Invariant($"{category:00000000}-0000-0000-0000-{value:000000000000}");
        return Guid.Parse(text);
    }

    private sealed record SectionSeed(string Heading, string Content);

    private sealed record QuizSeed(
        QuizResponseKind ResponseKind,
        string AssessmentGoal,
        string ExpectedAnswer,
        AnswerNormalization Normalization,
        string Explanation,
        IReadOnlyList<string> AnswerOptions);

    private sealed record LessonSeed(
        int Order,
        string TopicSlug,
        CurriculumLessonStatus Status,
        CurriculumDifficulty Difficulty,
        string Title,
        string Objective,
        string SampleCode,
        IReadOnlyList<SectionSeed> Sections,
        QuizSeed Quiz,
        string ExerciseTitle,
        string ExercisePrompt,
        IReadOnlyList<string> AllowedConcepts);
}
