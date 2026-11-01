# Flowchart Intermediate Representation

## Document Status

- Task: Prompt 15 — Flowchart intermediate representation
- Last updated: 2026-11-01
- Schema version: 1.0
- Scope: deterministic graph translation for simple sequence and one-level `if/else`

## Purpose and Boundary

The flowchart intermediate representation (IR) is the stable boundary between C# analysis and rendering. Application owns the Roslyn-free graph contracts. Infrastructure walks Roslyn syntax nodes and emits those contracts. Prompt 16's Mermaid adapter consumes the graph without needing to understand C# syntax trees.

Prompt 15 does not render a diagram, run source, compile source, or infer runtime behavior.

## Schema 1.0

`FlowGraph` contains:

- `SchemaVersion`: currently `1.0`;
- an ordered, read-only list of `FlowNode` values;
- an ordered, read-only list of `FlowEdge` values.

Every graph has exactly one start node and one end node. Node and edge IDs are unique, and every edge must reference existing nodes. Construction rejects an invalid graph rather than allowing broken renderer input.

### Nodes

| Type | Meaning |
| --- | --- |
| `Start` | Synthetic entry point. |
| `End` | Synthetic exit point. |
| `Action` | Declaration, assignment, calculation, or other supported expression. |
| `Decision` | An `if` condition. |
| `Input` | A supported statement containing `Console.ReadLine`. |
| `Output` | A direct `Console.WriteLine` statement. |

Source-backed nodes contain a `SourceSpan` with zero-based character offset and length plus one-based start/end line and column positions. Synthetic start/end nodes have no source span.

### Edges

| Type | Label | Meaning |
| --- | --- | --- |
| `Next` | none | Ordinary sequence. |
| `True` | `True` | The condition evaluated true. |
| `False` | `False` | The condition evaluated false. |

Both branch exits connect to the next source statement. If there is no later statement, they connect to the end node. An `if` without `else` keeps an explicit false path.

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

Schema 1.0 translates:

- top-level declarations and expression statements as sequence;
- `Console.ReadLine` statements as input;
- direct `Console.WriteLine` statements as output;
- one top-level `if` with an optional `else`;
- simple statements inside each branch;
- source following a branch, with deterministic merge edges.

Nested `if/else` returns `FL1001`. Other constructs that passed general syntax analysis but are outside this translation slice, such as loops, return `FL1002`. Diagnostics include a source location, explanation, and no partial graph.

## Known Limitations

- Nested decisions are intentionally rejected in schema 1.0.
- `for` and `while` are accepted by the wider Prompt 14 subset but are not translated yet.
- Local functions and `return` are not translated.
- Node labels describe syntax, not evaluated values or program output.
- Input/output classification is deliberately limited to supported `Console` calls.
- Prompt 16 provides Mermaid browser visualization and an equivalent structural text map; there is no execution trace.

Renderer behavior and escaping are documented in [FLOWCHART_RENDERER.md](FLOWCHART_RENDERER.md).

## Manual Visual Studio Check

1. Open `CodeLogic.slnx` in Visual Studio.
2. Open Test Explorer and run `RoslynFlowchartTranslatorTests`.
3. Confirm the sequence and `if/else` golden tests pass.
4. Debug `IfElseMatchesGoldenGraphAndMergesIntoNextAction` and inspect `result.Graph`.
5. Confirm IDs and collection order match the source, true/false edges leave the decision, and both branches join `node-005`.
6. Debug the nested-branch test and confirm it returns `FL1001`, a line/column, and no graph.
7. Search the translator for `Regex`, `Split`, compilation, or execution APIs and confirm none are used.
