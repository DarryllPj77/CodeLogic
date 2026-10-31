using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF-generated seed arrays are passed once during migration execution

namespace CodeLogic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurriculumTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumLessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TopicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Objective = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExerciseTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExercisePrompt = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ExerciseKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SampleCode = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    AllowedConceptsJson = table.Column<string>(type: "TEXT", nullable: false),
                    QuizResponseKind = table.Column<int>(type: "INTEGER", nullable: false),
                    QuizAssessmentGoal = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    QuizExpectedAnswer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    QuizNormalization = table.Column<int>(type: "INTEGER", nullable: false),
                    QuizExplanation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    QuizAnswerOptionsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumLessons_CurriculumTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "CurriculumTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CurriculumTopics",
                columns: new[] { "Id", "Description", "Order", "Slug", "Title" },
                values: new object[,]
                {
                    { new Guid("00000001-0000-0000-0000-000000000001"), "Store information using clear names and suitable C# types.", 1, "variables-and-types", "Variables and types" },
                    { new Guid("00000001-0000-0000-0000-000000000002"), "Combine and compare values with arithmetic, comparison, and Boolean operators.", 2, "operators", "Operators" },
                    { new Guid("00000001-0000-0000-0000-000000000003"), "Read simple text input and present useful console output.", 3, "input-and-output", "Input and output" },
                    { new Guid("00000001-0000-0000-0000-000000000004"), "Trace statements in the order a program evaluates them.", 4, "sequence", "Sequence" },
                    { new Guid("00000001-0000-0000-0000-000000000005"), "Choose one path with conditions and if/else statements.", 5, "branching", "Branching" },
                    { new Guid("00000001-0000-0000-0000-000000000006"), "Repeat bounded work and recognize loop control parts.", 6, "loops", "Loops" },
                    { new Guid("00000001-0000-0000-0000-000000000007"), "Name reusable behavior with parameters and return values.", 7, "methods", "Methods" },
                    { new Guid("00000001-0000-0000-0000-000000000008"), "Turn a small problem into ordered, testable steps.", 8, "algorithms", "Algorithms" },
                    { new Guid("00000001-0000-0000-0000-000000000009"), "Observe a program carefully and locate simple mistakes.", 9, "debugging", "Debugging" }
                });

            migrationBuilder.InsertData(
                table: "CurriculumLessons",
                columns: new[] { "Id", "AllowedConceptsJson", "Difficulty", "ExerciseId", "ExerciseKind", "ExercisePrompt", "ExerciseTitle", "Objective", "Order", "QuizAnswerOptionsJson", "QuizAssessmentGoal", "QuizExpectedAnswer", "QuizExplanation", "QuizNormalization", "QuizResponseKind", "SampleCode", "SectionsJson", "Status", "Title", "TopicId" },
                values: new object[,]
                {
                    { new Guid("00000002-0000-0000-0000-000000000001"), "[\"local-variable-declaration\",\"literal\",\"assignment\",\"arithmetic-expression\",\"console-write-line\",\"sequential-statements\"]", 1, new Guid("00000004-0000-0000-0000-000000000001"), 1, "Which C# type should store the number of completed lessons?", "Choose a whole-number type", "Declare variables with clear names and explain the difference between a variable's type and value.", 1, "[\"int\",\"double\",\"string\"]", "Identify the type that stores a whole number.", "int", "The int type stores whole numbers such as 5 or 42.", 1, 1, "int score = 5;\nstring learnerName = \"Mina\";\n\nscore = score + 1;\nConsole.WriteLine($\"{learnerName}: {score}\");", "[{\"id\":\"00000003-0000-0000-0000-000000000101\",\"heading\":\"Names, types, and values\",\"content\":\"A variable gives a value a meaningful name. Its type describes which kind of value C# will allow.\",\"order\":1},{\"id\":\"00000003-0000-0000-0000-000000000102\",\"heading\":\"Declare, then update\",\"content\":\"A declaration creates the variable. A later assignment can replace its value with another value of the same compatible type.\",\"order\":2}]", 1, "Variables hold typed values", new Guid("00000001-0000-0000-0000-000000000001") },
                    { new Guid("00000002-0000-0000-0000-000000000002"), "[\"local-variable-declaration\",\"literal\",\"arithmetic-expression\",\"comparison-expression\",\"boolean-expression\",\"console-write-line\",\"sequential-statements\"]", 1, new Guid("00000004-0000-0000-0000-000000000002"), 2, "What value is stored in isReady after the sample runs?", "Predict an operator result", "Use arithmetic, comparison, and Boolean operators to calculate values and answer true-or-false questions.", 2, "[]", "Predict the Boolean result after evaluating the sample expression.", "true", "The total is 10, so both comparisons are true and true && true evaluates to true.", 3, 2, "int total = 6 + 4;\nbool isReady = total >= 10 && total != 0;\n\nConsole.WriteLine(isReady);", "[{\"id\":\"00000003-0000-0000-0000-000000000201\",\"heading\":\"Calculate with arithmetic\",\"content\":\"Arithmetic operators such as \\u002B and - produce a new numeric value from existing values.\",\"order\":1},{\"id\":\"00000003-0000-0000-0000-000000000202\",\"heading\":\"Compare and combine conditions\",\"content\":\"Comparison operators produce true or false. Boolean operators such as \\u0026\\u0026 combine those results.\",\"order\":2}]", 1, "Operators create new information", new Guid("00000001-0000-0000-0000-000000000002") },
                    { new Guid("00000002-0000-0000-0000-000000000003"), "[\"local-variable-declaration\",\"console-read-line\",\"console-write-line\",\"sequential-statements\"]", 1, new Guid("00000004-0000-0000-0000-000000000003"), 2, "If the input is Ana, what line does the program display?", "Check input-and-output", "Read a short text value and display a clear response.", 3, "[]", "Check the learner can predict one result from communicate with a console program.", "Hello, Ana!", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "string? name = Console.ReadLine();\nConsole.WriteLine($\"Hello, {name}!\");", "[{\"id\":\"00000003-0000-0000-0000-000000000301\",\"heading\":\"Draft outline\",\"content\":\"Introduce console input, nullable text, and readable output without file or network access.\",\"order\":1}]", 2, "Communicate with a console program", new Guid("00000001-0000-0000-0000-000000000003") },
                    { new Guid("00000002-0000-0000-0000-000000000004"), "[\"local-variable-declaration\",\"literal\",\"assignment\",\"arithmetic-expression\",\"console-write-line\",\"sequential-statements\"]", 1, new Guid("00000004-0000-0000-0000-000000000004"), 2, "What value is displayed after the three statements run in order?", "Check sequence", "Trace a short program from the first statement to the last.", 4, "[]", "Check the learner can predict one result from follow statements in sequence.", "5", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "int value = 2;\nvalue = value + 3;\nConsole.WriteLine(value);", "[{\"id\":\"00000003-0000-0000-0000-000000000401\",\"heading\":\"Draft outline\",\"content\":\"Make the default top-to-bottom execution order visible before introducing branches or loops.\",\"order\":1}]", 2, "Follow statements in sequence", new Guid("00000001-0000-0000-0000-000000000004") },
                    { new Guid("00000002-0000-0000-0000-000000000005"), "[\"local-variable-declaration\",\"literal\",\"comparison-expression\",\"if-else\",\"console-write-line\"]", 2, new Guid("00000004-0000-0000-0000-000000000005"), 2, "Which word is displayed when score is 8?", "Check branching", "Trace a condition and explain why one branch runs while another is skipped.", 5, "[]", "Check the learner can predict one result from choose a path with if and else.", "Pass", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "int score = 8;\nif (score >= 5)\n{\n    Console.WriteLine(\"Pass\");\n}\nelse\n{\n    Console.WriteLine(\"Try again\");\n}", "[{\"id\":\"00000003-0000-0000-0000-000000000501\",\"heading\":\"Draft outline\",\"content\":\"Identify the condition, true path, false path, and the single path selected for a given value.\",\"order\":1}]", 2, "Choose a path with if and else", new Guid("00000001-0000-0000-0000-000000000005") },
                    { new Guid("00000002-0000-0000-0000-000000000006"), "[\"local-variable-declaration\",\"literal\",\"comparison-expression\",\"for-loop\",\"console-write-line\"]", 2, new Guid("00000004-0000-0000-0000-000000000006"), 2, "How many times does the loop body run?", "Check loops", "Identify loop initialization, condition, body, and update while tracing a small fixed number of repetitions.", 6, "[]", "Check the learner can predict one result from repeat bounded work with loops.", "3", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "for (int count = 1; count <= 3; count++)\n{\n    Console.WriteLine(count);\n}", "[{\"id\":\"00000003-0000-0000-0000-000000000601\",\"heading\":\"Draft outline\",\"content\":\"Start with a bounded for loop, then compare its parts with a simple while loop.\",\"order\":1}]", 2, "Repeat bounded work with loops", new Guid("00000001-0000-0000-0000-000000000006") },
                    { new Guid("00000002-0000-0000-0000-000000000007"), "[\"method-declaration\",\"method-call\",\"return-statement\",\"local-variable-declaration\",\"arithmetic-expression\",\"literal\"]", 2, new Guid("00000004-0000-0000-0000-000000000007"), 2, "What value is stored in total?", "Check methods", "Call a simple method and connect its parameters and return value to the result.", 7, "[]", "Check the learner can predict one result from name reusable behavior with methods.", "5", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "int Add(int left, int right)\n{\n    return left + right;\n}\n\nint total = Add(2, 3);", "[{\"id\":\"00000003-0000-0000-0000-000000000701\",\"heading\":\"Draft outline\",\"content\":\"Separate the method declaration from the call and trace values into parameters and back through return.\",\"order\":1}]", 2, "Name reusable behavior with methods", new Guid("00000001-0000-0000-0000-000000000007") },
                    { new Guid("00000002-0000-0000-0000-000000000008"), "[\"array-declaration\",\"array-indexing\",\"local-variable-declaration\",\"assignment\",\"comparison-expression\",\"for-loop\",\"if-else\",\"literal\"]", 3, new Guid("00000004-0000-0000-0000-000000000008"), 2, "What value remains in largest after the algorithm finishes?", "Check algorithms", "Describe and trace a small algorithm that finds the largest value in a short array.", 8, "[]", "Check the learner can predict one result from turn a problem into ordered steps.", "7", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "int[] values = [3, 7, 4];\nint largest = values[0];\n\nfor (int index = 1; index < values.Length; index++)\n{\n    if (values[index] > largest)\n    {\n        largest = values[index];\n    }\n}", "[{\"id\":\"00000003-0000-0000-0000-000000000801\",\"heading\":\"Draft outline\",\"content\":\"Combine sequence, a bounded loop, indexing, and a branch into one understandable algorithm.\",\"order\":1}]", 2, "Turn a problem into ordered steps", new Guid("00000001-0000-0000-0000-000000000008") },
                    { new Guid("00000002-0000-0000-0000-000000000009"), "[\"local-variable-declaration\",\"literal\",\"arithmetic-expression\",\"comment\",\"console-write-line\",\"sequential-statements\"]", 3, new Guid("00000004-0000-0000-0000-000000000009"), 2, "Which operator should replace + to calculate rectangle area?", "Check debugging", "Locate a simple mistake by comparing the intended result with values produced at each step.", 9, "[]", "Check the learner can predict one result from debug by observing expected and actual values.", "*", "The final explanation will be reviewed when the draft lesson is completed.", 3, 2, "int width = 4;\nint height = 3;\nint area = width + height; // Bug: should multiply\nConsole.WriteLine(area);", "[{\"id\":\"00000003-0000-0000-0000-000000000901\",\"heading\":\"Draft outline\",\"content\":\"Use a small incorrect expression to practise reading values, forming a hypothesis, and making one focused correction.\",\"order\":1}]", 2, "Debug by observing expected and actual values", new Guid("00000001-0000-0000-0000-000000000009") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumLessons_ExerciseId",
                table: "CurriculumLessons",
                column: "ExerciseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumLessons_Order",
                table: "CurriculumLessons",
                column: "Order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumLessons_TopicId",
                table: "CurriculumLessons",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTopics_Order",
                table: "CurriculumTopics",
                column: "Order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTopics_Slug",
                table: "CurriculumTopics",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurriculumLessons");

            migrationBuilder.DropTable(
                name: "CurriculumTopics");
        }
    }
}
