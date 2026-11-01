# Bounded Pedagogical Execution Traces

## Document Status

- Task: Prompt 18 — Explain execution without running arbitrary code
- Last updated: 2026-11-01
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

- initialized `int`, `double`, `bool`, `string`, and `char` local variables;
- direct variable assignment and `+=`, `-=`, `*=`, and `/=`;
- arithmetic, comparison, equality, Boolean, unary, increment, and decrement expressions already admitted by analysis;
- interpolated strings;
- `Console.WriteLine` with zero or one argument;
- sequence, nested `if/else`, `while`, `for`, and `do-while`;
- omitted `for` conditions, subject to the operation limit.

The trace slice explicitly rejects arrays, element access, live `Console.ReadLine`, local functions, method calls, `return`, `break`, and `continue`. Some of these remain valid in the wider curriculum or analysis subset; passing syntax analysis is not a promise that tracing supports them.

The interpreter is intentionally not a C# type checker. Incompatible values, undeclared variables, unsupported targets, invalid numeric operations, and other cases it cannot explain return a positioned diagnostic instead of guessing.

## Operation Limit

`ExecutionTraceDefaults.MaximumOperations` is 100. Each declaration, expression statement, condition check, `for` initializer, and `for` iterator consumes one operation. If the next operation would exceed the limit, tracing stops with `ET0002` and retains the bounded snapshots already produced.

This makes `while (true)`, `for (;;)`, and other infinite-looking patterns safe and deterministic. The limit is a resource and teaching bound, not a claim that the source would terminate at that point.

## Playground Controls

- **Build step trace** analyzes and simulates the current supported snippet.
- **Next step** advances through the already-built immutable snapshots; it does not rerun source.
- **Reset trace** returns to the initial Start snapshot with empty variables and output.
- Editing, loading, or resetting source discards the old trace so stale state is never shown as current.

The trace panel uses native buttons, headings, definition lists, a variable table, text output, and live status messages. Its information does not depend on diagram color or animation.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, run `CodeLogic.Web`, and open **Playground**.
2. Build a trace for the Variables sample. Step through it and confirm `score` changes from `5` to `6`, then output becomes `Mina: 6`.
3. Enter an `if/else` sample with a known true condition. Confirm only the true output appears; change the value and confirm only the false output appears.
4. Enter a three-iteration `for` loop. Confirm each condition, body, and iterator appears in order.
5. Enter `while (true) { }`. Confirm the trace stops with `ET0002` at 100 operations and the page remains responsive.
6. Select **Reset trace** and confirm the program counter returns to zero with no variables or output.
7. Enter `Console.ReadLine()` or an array. Confirm an `ET1001` diagnostic appears and no partial trace is presented.
8. Repeat using only the keyboard, at 200% zoom, and at 320 CSS pixels.
