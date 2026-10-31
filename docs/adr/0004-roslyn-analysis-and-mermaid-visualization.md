# ADR 0004: Roslyn Analysis with Graph IR and Mermaid First

- Status: Proposed
- Date: 2026-10-31

## Context

CodeLogic intends to explain supported C# source through flowcharts and bounded traces. Parsing C# correctly is complex, while coupling the application directly to either Roslyn trees or a diagram format would make the teaching model difficult to test and evolve.

## Decision

Introduce Roslyn in a later dedicated task for syntax and semantic analysis of an explicitly supported C# subset. Convert validated analysis results into a custom graph intermediate representation (IR). Render that IR through a Mermaid adapter first.

The pipeline is:

1. Validate input size and supported constructs.
2. Parse source with Roslyn without executing it.
3. Perform bounded semantic/control-flow analysis.
4. Map results into a renderer-neutral graph IR.
5. Escape labels and render Mermaid text.

Do not compile or execute arbitrary student code inside the web application process.

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
