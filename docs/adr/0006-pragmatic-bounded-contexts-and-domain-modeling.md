# ADR 0006: Pragmatic Bounded Contexts and Domain Modeling

- Status: Proposed
- Date: 2026-11-01

## Context

CodeLogic now contains curriculum, exercises, progress, code analysis, and Identity behavior. The project boundaries are clear, but technical layers alone do not describe which business language owns each rule. A DDD review is needed without turning a beginner-focused modular monolith into many projects or services.

Not every model has the same behavioral needs. Lessons and progress snapshots protect invariants, while diagnostics, graph nodes, trace steps, and persistence rows primarily carry data.

## Decision

Recognize four logical bounded contexts inside the existing modular monolith:

- **Learning:** curriculum, lessons, authoring/publication, navigation, completion, and progress views;
- **Practice:** exercises, deterministic evaluation, attempts, authored cases, and feedback;
- **Analysis:** supported syntax, diagnostics, graph IR, Mermaid serialization contracts, and bounded traces;
- **Identity:** accounts, credentials, cookies, roles, and trusted subject identity.

Keep the current four physical projects organized by dependency direction. Use feature namespaces/folders inside those projects rather than creating a project per context today.

Use rich models where constructors or behavior protect real invariants. Use simple immutable records for boundary results and graph/trace data, simple EF entities for persistence mapping, and framework Identity types behind Infrastructure/Web boundaries.

Allow the current Application `CurriculumLesson` and `LearningSessionProgress` types to form a small Learning/Practice shared kernel. Revisit that seam only when independent ownership or external contracts create a concrete need.

## Alternatives Considered

### One bounded context for the entire application

This minimizes terminology, but mixes curriculum, assessment, code semantics, and account security into one model. Ownership and future extraction seams would remain ambiguous.

### A project or service for every bounded context

This provides stronger physical separation, but would multiply projects, mapping code, configuration, and potentially deployments for a small single-owner application. Logical boundaries plus architecture tests provide enough protection now.

### Rich entities for every record

Adding methods and identity semantics to diagnostics, graph nodes, and DTOs would create ceremony without invariants or lifecycle. Data models remain appropriate at these boundaries.

### An anemic domain model everywhere

Public setters and post-construction validation would allow invalid lessons, identifiers, attempts, and snapshots. Existing constructor-enforced invariants remain valuable.

## Consequences and Tradeoffs

### Benefits

- Business language has explicit ownership without changing deployment topology.
- Rich modeling effort is spent only where invalid state or behavior warrants it.
- Analysis and Identity framework details remain outside core contracts.
- Future extraction can follow an already named context seam.

### Costs

- Logical context discipline still depends partly on review; projects do not enforce every context boundary.
- The Learning/Practice shared kernel creates deliberate coupling.
- Some types may move later if context ownership becomes independent.

## Future Review Triggers

Create a superseding ADR before splitting contexts physically. Evidence should include independent team ownership, release cadence, security isolation, scaling, external API stability, or asynchronous coordination—not architectural fashion.
