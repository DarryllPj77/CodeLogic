# Roslyn Parse and Diagnostics

## Document Status

- Task: Prompt 14 — Roslyn parse and diagnostics
- Last updated: 2026-11-01
- Scope: syntax parsing, fixed-subset validation, beginner explanations, and bounded server-side analysis

## What the Analyzer Does

The Playground's **Check syntax** action sends the current editor text to an in-process application service. The Infrastructure adapter uses `Microsoft.CodeAnalysis.CSharp` to parse C# 14 syntax, reports Roslyn syntax diagnostics with one-based line and column positions, and then checks the syntax tree against CodeLogic's deliberately small learning subset.

The Application project owns the `ICodeAnalysisService` contract and result types. Infrastructure owns the Roslyn implementation. Web registers the adapter and presents the result without depending on Roslyn types.

## Fixed Supported Subset

Prompt 14 accepts:

- top-level statements and small local functions;
- `int`, `double`, `bool`, `string`, `char`, and arrays of those types;
- declarations, assignments, `if`/`else`, `for`, `while`, `do-while`, and `return`;
- number, string, character, and Boolean literals;
- basic arithmetic, comparison, Boolean, assignment, and increment/decrement operators;
- array creation, initialization, and element access;
- interpolated strings;
- `Console.WriteLine`, `Console.ReadLine`, and calls to local functions declared in the snippet.

The validator returns an actionable unsupported-feature diagnostic for constructs outside that list, including `break`, `continue`, namespaces, type declarations, `using` directives, `foreach`, `switch`, exception handling, object creation, lambdas, general member access, and framework APIs. Preprocessor directives are rejected too.

## Limits and Outcomes

Analysis applies these explicit limits before or during parsing:

| Limit | Value | Reason |
| --- | --- | --- |
| Source length | 1,200 characters | Matches the curriculum and editor policy. |
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

The time limit is a cooperative in-process cancellation bound, not a process-isolation security boundary. The 1,200-character input limit is applied first. Moving analysis to an isolated worker remains a future option if measured load or security policy justifies it.

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
