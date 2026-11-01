# Flowchart Intermediate Representation

## Document Status

- Tasks: Prompts 15 and 17 — graph IR, loops, and nested control flow
- Last updated: 2026-11-01
- Schema version: 1.1
- Scope: deterministic graph translation for sequence, nested `if/else`, `while`, `for`, and `do-while`

## Purpose and Boundary

The flowchart intermediate representation (IR) is the stable boundary between C# analysis and rendering. Application owns the Roslyn-free graph contracts. Infrastructure walks Roslyn syntax nodes and emits those contracts. Prompt 16's Mermaid adapter consumes the graph without needing to understand C# syntax trees.

The translator does not run source, compile source, or infer runtime behavior.

## Schema 1.1

`FlowGraph` contains:

- `SchemaVersion`: currently `1.1`;
- an ordered, read-only list of `FlowNode` values;
- an ordered, read-only list of `FlowEdge` values.

Every graph has exactly one start node and one end node. Node and edge IDs are unique, and every edge must reference existing nodes. Construction rejects an invalid graph rather than allowing broken renderer input.

### Nodes

| Type | Meaning |
| --- | --- |
| `Start` | Synthetic entry point. |
| `End` | Synthetic exit point. |
| `Action` | Declaration, assignment, calculation, or other supported expression. |
| `Decision` | An `if` or loop condition. |
| `Input` | A supported statement containing `Console.ReadLine`. |
| `Output` | A direct `Console.WriteLine` statement. |

Source-backed nodes contain a `SourceSpan` with zero-based character offset and length plus one-based start/end line and column positions. Synthetic start/end nodes have no source span.

### Edges

| Type | Label | Meaning |
| --- | --- | --- |
| `Next` | none | Ordinary sequence. |
| `True` | `True` | The condition evaluated true. |
| `False` | `False` | The condition evaluated false. |
| `LoopBack` | `Repeat` or `True · repeat` | Control returns to a loop condition or the start of a `do-while` body. |

Both branch exits connect to the next source statement. Loop decisions keep an explicit false exit even when the condition text looks permanently true, because the graph describes syntax rather than proving runtime termination.

## Determinism Rules

- IDs use zero-padded source-order counters: `node-000`, `node-001`, and `edge-000`, `edge-001`.
- Nodes are added in source traversal order: start, preceding sequence, decision, true branch, false branch, following sequence, end.
- Edges are added when each destination is connected, with true-branch exits before false-branch exits.
- Labels come from Roslyn's normalized syntax nodes, not regex or hand-written text parsing.
- The same accepted source and application version produces the same ordered graph.

Golden tests lock these rules so a future renderer can rely on them. A deliberate schema change requires a schema-version decision and updated golden expectations.

## Translation Outcomes

`IFlowchartTranslator` returns one of:

- `Success`, with a complete validated graph;
- `InvalidInput`, `SyntaxError`, `Unsupported`, or `TimedOut`, with diagnostics and no graph.

The translator first reuses Prompt 14 analysis. It then parses the already-bounded source and walks Roslyn `StatementSyntax` and `ExpressionSyntax` nodes. It never translates by matching source strings or regular expressions.

## Supported Translation Slice

Schema 1.1 translates:

- top-level declarations and expression statements as sequence;
- `Console.ReadLine` statements as input;
- direct `Console.WriteLine` statements as output;
- nested `if/else` up to the analyzer's three-level control-flow limit;
- `while`, `for`, and `do-while` loops;
- `for` declarations/initializers and iterators as explicit action nodes;
- simple sequence, branches, and supported loops inside one another;
- source following a branch or loop, with deterministic merge and exit edges.

`break` and `continue` are explicitly unsupported as `CL1006` and `CL1007`. Supporting them later requires tested jump targets, especially inside nested loops. Other constructs outside this translation slice return positioned diagnostics with no partial graph.

## Known Limitations

- Local functions and `return` are not translated.
- `break` and `continue` are not translated.
- Conditions are displayed but not semantically evaluated; `while (true)` and `for (;;)` therefore retain a structural false exit alongside the repeat edge.
- Node labels describe syntax, not evaluated values or program output.
- Input/output classification is deliberately limited to supported `Console` calls.
- Prompt 16 provides Mermaid browser visualization and an equivalent structural text map; there is no execution trace.

Renderer behavior and escaping are documented in [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md).

## Manual Visual Studio Check

1. Open `CodeLogic.slnx` in Visual Studio.
2. Open Test Explorer and run `RoslynFlowchartTranslatorTests`.
3. Confirm the sequence, nested branch, `while`, `for`, and `do-while` golden tests pass.
4. Debug `WhileLoopMatchesGoldenGraphWithExitAndBackEdge` and inspect `result.Graph`.
5. Confirm the decision has a True body edge, a False exit, and a `LoopBack` edge from the body.
6. Run the infinite-looking loop cases and confirm each keeps both a repeat edge and a structural exit.
7. Run the `break` and `continue` cases and confirm each returns a specific diagnostic and no graph.
8. Search the translator for `Regex`, `Split`, compilation, or execution APIs and confirm none are used.
