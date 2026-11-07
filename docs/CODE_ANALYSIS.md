# Roslyn Parse and Diagnostics

## Document Status

- Task: Prompt 14 — Roslyn parse and diagnostics
- Last updated: 2026-11-07 (classic programs, actionable diagnostics, supported-feature registry)
- Scope: syntax parsing, fixed-subset validation, beginner explanations, and bounded server-side analysis

## What the Analyzer Does

The Playground's **Check syntax** action sends the current editor text to an in-process application service. The Infrastructure adapter uses `Microsoft.CodeAnalysis.CSharp` to parse C# 14 syntax, reports Roslyn syntax diagnostics with one-based line and column positions, and then checks the syntax tree against CodeLogic's deliberately small learning subset.

The Application project owns the `ICodeAnalysisService` contract and result types. Infrastructure owns the Roslyn implementation. Web registers the adapter and presents the result without depending on Roslyn types.

## Program Forms

`ProgramStructure` (Infrastructure) reads the shape of the source once, and the analyzer, flowchart translator, and trace interpreter all use it:

- **Classic (default):** plain `using` directives, at most one namespace (block or file-scoped), and one class containing exactly one `static Main` (`void` or `int`, no parameters or one `string[]`), plus any other `static` methods. Using directives are kept in the source and ignored for flow. The flowchart and trace start from Main's body.
- **Top-level statements (secondary):** statements written directly in the file, as before.
- **Mixed:** top-level statements *and* a Main method. This is rejected (`CL1010`) because there would be two entry points.

The form is detected, not chosen. The Playground shows it as a badge. Zero Main methods give `CL1008`, two or more give `CL1009`, and an unsupported Main signature or a non-static method gives `CL1014`.

## Supported Subset and the Registry

`SupportedFeatureRegistry` (Application) is the single source of truth for what is accepted. Each entry has a section (Program structure, Statements, Not supported yet), a complete example program, and, for unsupported features, the diagnostic code with its what / why / instead text. The validator builds those diagnostics from the registry, and the Playground's **What's supported** dialog renders the same entries, so the two cannot drift. `SupportedFeatureRegistryTests` runs every example: supported ones must analyze, translate, and trace, and unsupported ones must produce exactly the registry's diagnostic.

Accepted inside Main and static methods:

- `int`, `double`, `bool`, `string`, `char`, and one-dimensional arrays of those types (`int[,]` and jagged `int[][]` are reported as not supported yet);
- array creation (`{ 1, 2 }`, `new int[3]`, `new int[] { ... }`, `new[] { ... }`), indexing with one index, and `.Length` on arrays and strings;
- `foreach` with an explicit item type (`var` asks for the type). Assigning the loop variable is reported as read-only (`CL1016`), as in C#;
- initialized declarations, assignments, expression statements, `if` / `else if` / `else`, `for`, `while`, `do-while`;
- `switch` statements whose cases are literal values (several labels per section are allowed). Every section must end with `break` or `return`, because there is no fall-through. Pattern cases, `when` clauses, and named constants are rejected with an explanation;
- `break` inside a loop or switch (`CL1006` otherwise) and `continue` inside a loop (`CL1007` otherwise). Neither can leave a method;
- `return` anywhere, including inside loops;
- number, string, character, and Boolean literals; arithmetic, comparison, Boolean, assignment, and increment/decrement operators; interpolated strings;
- `Console.WriteLine`, `Console.Write`, `Console.ReadLine`, and calls to the program's own static methods (argument count is checked, `CL1015`; recursion is rejected, `CL1013`).

An `else if` chain does not add nesting depth; a `switch` counts as one level.

## Diagnostic Format

Every unsupported-feature diagnostic says **what** is wrong (`Message`), **why** (`Explanation`), and **what to do instead** (`Suggestion`). It also names a registry entry (`HelpTopicId`) that the Playground's **What's supported** button opens. Examples:

- `CL1001` switch expression: "switch expressions are not supported yet." Instead: use a switch statement that assigns the variable in each case, or if/else if/else.
- `CL1004` depth: "This code is nested 4 levels deep; the limit is 3." Instead: extract the innermost block into a static method.
- `FL1002` local functions: suggests moving the function into the class. In classic programs the Playground offers **Rewrite as static method**, which `IProgramStructureService` performs syntactically. It refuses when the function uses the enclosing method's variables.

Not supported yet (each with its own diagnostic): switch expressions, local functions, records, properties and fields, generics, `async`/`await`, LINQ, lambdas, object creation, `foreach`, exception handling, and framework APIs (arbitrary .NET execution). Preprocessor directives, `using static`, and alias directives are rejected too.

## Limits and Outcomes

Analysis applies these explicit limits before or during parsing:

| Limit | Value | Reason |
| --- | --- | --- |
| Source length | 10,000 characters | Matches the shared curriculum, editor, authoring, and storage policy. |
| Control-flow nesting | 3 levels | Keeps later explanations and traces understandable. |
| Analysis duration | 2 seconds | Stops unusually expensive analysis, including cold-start overhead. |

Results distinguish five outcomes:

- `Valid`: syntax is understood and every construct is in the supported subset.
- `InvalidInput`: input is empty or exceeds the size limit.
- `SyntaxError`: Roslyn reports malformed C# with line, column, message, and a short explanation.
- `Unsupported`: the C# syntax is parseable but uses a construct outside CodeLogic's subset.
- `TimedOut`: analysis reached its duration limit.

Editing, loading, or resetting source clears the displayed result so stale feedback is not presented as current.

## Safety Boundary

The adapter calls `CSharpSyntaxTree.ParseText` and inspects syntax nodes only. It does not create a `CSharpCompilation`, resolve or load assemblies, emit binaries, use reflection, start a process, or execute learner source. Parsing permission is not execution permission.

The time limit is a cooperative in-process cancellation bound, not a process-isolation security boundary. The 10,000-character input limit is applied first. Live flowchart work is debounced by 400 milliseconds, and boundary tests require analysis and translation at the maximum accepted length to complete inside the existing two-second budget. Moving analysis to an isolated worker remains a future option if measured load or security policy justifies it.

## Known Limitations

- Prompt 14 performs syntax and subset checks, not semantic or type analysis. For example, a type mismatch may parse successfully.
- The accepted subset is intentionally smaller than the complete C# language.
- Diagnostics point to source positions but do not yet highlight editor ranges.
- Prompt 18 adds a narrower bounded interpreter after analysis and graph translation. A passing analysis result does not guarantee that tracing supports every accepted construct.
- A passing result means only “parseable and currently supported,” not “compiles” or “is correct.”

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, set `CodeLogic.Web` as the startup project, and press **Ctrl+F5**.
2. Open **Playground** and select **Check syntax** on either curated sample. Confirm the success message says the code was not compiled or run.
3. Remove a closing parenthesis and check again. Confirm the result includes a Roslyn `CS` code, a one-based line and column, and a plain-language explanation.
4. Enter `foreach (int value in values) { Console.WriteLine(value); }`. Confirm it is described as valid syntax but unsupported by CodeLogic.
5. Enter `File.ReadAllText("secret.txt");`. Confirm general framework access is rejected and nothing executes.
6. Edit the source after a result appears. Confirm the old result disappears and the editor text remains intact.
7. Use only the keyboard to reach the editor and **Check syntax**, then verify the live status and diagnostic content are understandable at 200% zoom and a 320 CSS-pixel viewport.

The flowchart IR and its narrower translation slice are documented in [FLOWCHART_IR.md](FLOWCHART_IR.md).
The still narrower interpreter subset and its non-execution boundary are documented in [EXECUTION_TRACES.md](EXECUTION_TRACES.md).
