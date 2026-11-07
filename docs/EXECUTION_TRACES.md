# Bounded Pedagogical Execution Traces

## Document Status

- Task: Prompt 18 — Explain execution without running arbitrary code
- Last updated: 2026-11-07 (classic programs and static method calls)
- Model: deterministic CodeLogic interpreter, not the .NET runtime
- Limit: 100 modeled operations per trace

## Purpose and Safety Boundary

CodeLogic explains a small authored subset by interpreting Roslyn syntax nodes with its own value model. It does not create a `CSharpCompilation`, emit an assembly, invoke learner-defined methods, use reflection, start a process, or run learner source through .NET.

This distinction matters: a trace is a teaching explanation of syntax that CodeLogic explicitly models. It is not proof that the snippet compiles, and it must not be described as the program's real .NET execution.

## Trace Contract

Every successful trace is an immutable ordered list of snapshots. A snapshot shows:

- a zero-based program counter counting modeled source operations;
- the active graph node ID, type, and label;
- scalar variable names and display values after that operation;
- cumulative simulated `Console.WriteLine` output;
- a short explanation of the modeled action or decision.

Synthetic Start and End snapshots do not consume the operation budget. The same accepted source and CodeLogic version produces the same snapshots in the same order.

## Supported Trace Subset

The first interpreter slice supports:

- initialized `int`, `double`, `bool`, `string`, and `char` local variables, and one-dimensional arrays of them (up to 50 items). Arrays are shared references, as in C#: assigning or passing one does not copy it. The variables panel shows `{ 90, 75, 85 }`, and `new T[n]` starts with default values (`0`, `false`, `null`);
- reading and changing array items (`scores[1] = 75`, `+=`, `++`), `.Length`, and string indexing to get a `char`. An index outside the array stops the trace with an IndexOutOfRangeException-style explanation. Printing an array outputs `System.Int32[]`, as real C# does;
- `foreach` over arrays and strings: each repetition records the "has another item" decision and `Set score to 72 (item 2 of 3).`, and the loop variable disappears after the loop;
- direct variable assignment and `+=`, `-=`, `*=`, and `/=`;
- arithmetic, comparison, equality, Boolean, unary, increment, and decrement expressions already admitted by analysis;
- interpolated strings;
- `Console.WriteLine` and `Console.Write` with zero or one argument (`Write` continues the current output line);
- sequence, nested `if/else`, `while`, `for`, and `do-while`;
- classic programs starting at `Main`. An `int` Main's `return` value is reported at End. `args` is an empty `string[]` (`args.Length` is 0), and replacing it is rejected as read-only;
- calls to the program's static methods. Each call pushes a frame with its own parameters and locals, adds `Called Name with ...` and `Name returned ...` steps at the method's Start and Return nodes, and returns its value to the caller. The variable list shows the current frame only;
- `return` anywhere in a method, including inside loops;
- `break` (leaves the nearest loop or switch) and `continue` (skips to the next repetition; a `for` loop still runs its iterator), each recorded as its own step;
- `switch`: the value is evaluated once (`The switch value is 2.`), each case is compared in order (`2 does not match case 1.`), and the first match runs, or `default` when nothing matches;
- omitted `for` conditions, subject to the operation limit.

The trace slice explicitly rejects multi-dimensional and jagged arrays, changing characters of a string, live `Console.ReadLine`, local functions, and recursion. Some of these remain valid in the wider curriculum or analysis subset; passing syntax analysis is not a promise that tracing supports them.

The interpreter is intentionally not a C# type checker. Incompatible values, undeclared variables, unsupported targets, invalid numeric operations, and other cases it cannot explain return a positioned diagnostic instead of guessing.

## Operation Limit

`ExecutionTraceDefaults.MaximumOperations` is 100. Each declaration, expression statement, condition check, `for` initializer, and `for` iterator consumes one operation. If the next operation would exceed the limit, tracing stops with `ET0002` and retains the bounded snapshots already produced.

This makes `while (true)`, `for (;;)`, and other infinite-looking patterns safe and deterministic. The limit is a resource and teaching bound, not a claim that the source would terminate at that point.

## Playground Controls

- **Build step trace** analyzes and simulates the current supported snippet.
- **Next step** advances through the already-built immutable snapshots; it does not rerun source.
- **Reset trace** returns to the initial Start snapshot with empty variables and output.
- Editing, loading, or resetting source discards the old trace so stale state is never shown as current.

Prediction practice is optional and deterministic. For a decision step it asks which existing graph edge (True or False) the trace will take. For a step that changes a variable, it asks for the next recorded value. The comparison trims surrounding whitespace and ignores letter casing, then advances to the same precomputed snapshot whether the prediction is correct or incorrect. Empty answers do not advance. Play is disabled in prediction mode so every applicable step remains an intentional learner check.

The trace panel uses native buttons, headings, definition lists, a variable table, text output, and live status messages. Its information does not depend on diagram color or animation.

Prompt 19 reuses this interpreter for instructor-defined logical cases. Those results compare fixed expected and actual outputs; they do not expand the interpreter subset or turn traces into proof of general correctness. See [LOGICAL_FEEDBACK.md](LOGICAL_FEEDBACK.md).

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, run `CodeLogic.Web`, and open **Playground**.
2. Build a trace for the Variables sample. Step through it and confirm `score` changes from `5` to `6`, then output becomes `Mina: 6`.
3. Enter an `if/else` sample with a known true condition. Confirm only the true output appears; change the value and confirm only the false output appears.
4. Enter a three-iteration `for` loop. Confirm each condition, body, and iterator appears in order.
5. Enter `while (true) { }`. Confirm the trace stops with `ET0002` at 100 operations and the page remains responsive.
6. Select **Reset trace** and confirm the program counter returns to zero with no variables or output.
7. Enter `Console.ReadLine()` or an array. Confirm an `ET1001` diagnostic appears and no partial trace is presented.
8. Repeat using only the keyboard, at 200% zoom, and at 320 CSS pixels.
