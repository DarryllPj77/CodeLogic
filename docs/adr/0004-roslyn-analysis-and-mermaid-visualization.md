# ADR 0004: Roslyn Analysis with Graph IR and Mermaid First

- Status: Accepted; parsing, graph IR, Mermaid, loops, and bounded trace slices implemented
- Date: 2026-10-31

## Context

CodeLogic intends to explain supported C# source through flowcharts and bounded traces. Parsing C# correctly is complex, while coupling the application directly to either Roslyn trees or a diagram format would make the teaching model difficult to test and evolve.

## Decision

Introduce Roslyn in dedicated tasks for syntax and later semantic analysis of an explicitly supported C# subset. Convert validated analysis results into a custom graph intermediate representation (IR). Render that IR through a Mermaid adapter first.

The pipeline is:

1. Validate input size and supported constructs.
2. Parse source with Roslyn without executing it.
3. Perform bounded semantic/control-flow analysis.
4. Map results into a renderer-neutral graph IR.
5. Escape labels and render Mermaid text.

Do not compile or execute arbitrary student code inside the web application process.

## Prompt 14 Implementation Note

Prompt 14 implements steps 1 and 2 plus syntax-tree-only subset validation. Application owns the analysis contract; Infrastructure owns the pinned Roslyn adapter; Web is the composition root and result presenter. Input is limited to 1,200 characters, control-flow nesting to three levels, and analysis to two seconds. Semantic/control-flow analysis, graph IR, and Mermaid remain future work.

## Prompt 15 Implementation Note

Prompt 15 introduces graph schema 1.0 in Application and an Infrastructure translator that walks Roslyn syntax nodes. Stable nodes, edges, source spans, and true/false branches are implemented for simple sequence and one-level `if/else`. Nested branches return an explicit diagnostic. Broader control flow and Mermaid rendering remain future work.

## Prompt 16 Implementation Note

Prompt 16 implements a safe Mermaid serializer, pinned Mermaid 12.2.0 browser rendering, an equivalent node/connection list, and exact source selection. Strict Mermaid configuration, label escaping, SVG DOM parsing/sanitization, and `replaceChildren` avoid direct untrusted HTML injection. Broader control flow and execution traces remain future work.

## Prompt 17 Implementation Note

Prompt 17 updates the graph contract to schema 1.1 and adds the explicit `LoopBack` edge. The Roslyn translator recursively supports nested `if/else`, `while`, `for`, and `do-while` within the established nesting bound. Golden tests lock loop exits, back edges, nesting, and infinite-looking syntax. `break` and `continue` remain deliberately unsupported because their jump targets need a separate tested design. Static graph construction still does not evaluate conditions, compile, or execute source.

## Prompt 18 Implementation Note

Prompt 18 adds a bounded pedagogical interpreter behind an Application interface. The Infrastructure adapter evaluates only a documented scalar/control-flow subset from Roslyn syntax nodes and maps every modeled operation to the existing graph IR. Immutable snapshots expose program counter, active node, variables, and cumulative output. A 100-operation limit safely terminates infinite-looking traces. No compilation, assembly emission, reflection, process launch, or learner-defined method invocation is introduced; the model must not be presented as general .NET execution.

## Alternatives Considered

### Regular expressions or a handwritten C# parser

Appears simple for demonstrations but fails on real C# syntax and would recreate compiler work poorly.

### Bind the UI directly to Roslyn syntax trees

Avoids an intermediate model but exposes compiler detail to teaching and presentation code. Tests and alternate rendering become harder.

### Generate Mermaid directly during analysis

Fewer types initially, but couples language analysis to one renderer and mixes escaping/presentation concerns with program understanding.

### Build a custom canvas renderer first

Allows maximum visual control but creates substantial layout, accessibility, export, and interaction work before the learning model is validated.

### Execute code to discover behavior

Could show real runtime results, but arbitrary execution creates major security and resource-isolation risks. It is explicitly out of scope for this phase.

## Consequences and Tradeoffs

### Benefits

- Roslyn provides language-correct parsing and semantic information.
- The graph IR gives analysis and rendering independent tests.
- Mermaid output is readable, inspectable, and quick to integrate.
- The supported subset can produce clear teaching feedback instead of unsafe execution.

### Costs

- Roslyn has a learning curve and adds package weight.
- Defining a useful graph IR requires careful scope control.
- Mermaid layout and interaction options are limited compared with a custom renderer.
- Static analysis cannot reproduce every runtime behavior.

## Future Extraction Seams

Any parser can emit the graph IR, and any renderer can consume it. Analysis may move to an isolated worker if measured CPU/memory load or security policy requires process isolation. A custom SVG/canvas renderer can replace Mermaid without changing Roslyn analysis.
