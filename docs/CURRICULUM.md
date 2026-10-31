# CodeLogic Curriculum Specification

## Document Status

- Status: Implemented for Prompt 07
- Last updated: 2026-10-31
- Manifest type: Typed seed
- Schema version: 1
- Complete pilots: 2
- Draft lessons: 7

## Purpose

The curriculum manifest is the single data source for the initial learning sequence. It describes what each lesson intends to teach without placing seed content inside Blazor components.

The manifest contract lives in `CodeLogic.Application/Curriculum`. The seed lives in `CodeLogic.Infrastructure/Curriculum/SeededCurriculumCatalog.cs`. The Web project requests it through `ICurriculumCatalog` and renders the lesson listing.

This separation allows a later JSON, database, or authoring adapter to implement the same catalog contract without moving curriculum rules into the UI.

## Manifest Shape

| Element | Purpose |
| --- | --- |
| `SchemaVersion` | Identifies the supported manifest contract. The initial version is `1`. |
| `SafeSnippetPolicy` | Defines size, nesting, allowed constructs, forbidden capabilities, and the no-execution policy. |
| `Topics` | Supplies stable topic IDs, URL-safe slugs, titles, and descriptions. |
| `Lessons` | Supplies ordering, status, difficulty, Domain lesson and exercise models, sample code, allowed concepts, and quiz criteria. |
| `QuizCriteria` | Defines the response kind, assessment goal, expected answer, normalization rule, and human-authored explanation. |

The manifest reuses the framework-independent `Topic`, `Lesson`, `LessonSection`, and `Exercise` Domain types. Stable seed GUIDs make relationships deterministic across application starts.

## Initial Curriculum

| Order | Topic | Status | Difficulty | Assessment focus |
| --- | --- | --- | --- | --- |
| 1 | Variables and types | Complete pilot | Beginner 1 | Choose an appropriate whole-number type. |
| 2 | Operators | Complete pilot | Beginner 1 | Predict an arithmetic and Boolean result. |
| 3 | Input and output | Draft | Beginner 1 | Predict output from a short text input. |
| 4 | Sequence | Draft | Beginner 1 | Trace statements from first to last. |
| 5 | Branching | Draft | Beginner 2 | Select the path taken by an `if`/`else`. |
| 6 | Loops | Draft | Beginner 2 | Count bounded loop repetitions. |
| 7 | Methods | Draft | Beginner 2 | Trace parameters and a return value. |
| 8 | Algorithms | Draft | Beginner 3 | Trace a small largest-value algorithm. |
| 9 | Debugging | Draft | Beginner 3 | Identify an incorrect arithmetic operator. |

Every lesson has an objective, sample code, a quiz criterion, a difficulty, safe concepts, and at least one section. Complete pilots have two reviewed content sections. Draft sections are outlines and must not be presented as finished lessons.

## Complete Pilot 1: Variables and Types

Objective:

> Declare variables with clear names and explain the difference between a variable's type and value.

Content sections:

1. Names, types, and values.
2. Declare, then update.

Quiz criterion:

- response kind: multiple choice;
- assessment goal: identify the type that stores a whole number;
- expected answer: `int`;
- normalization: exact;
- feedback is human-authored in the manifest.

## Complete Pilot 2: Operators

Objective:

> Use arithmetic, comparison, and Boolean operators to calculate values and answer true-or-false questions.

Content sections:

1. Calculate with arithmetic.
2. Compare and combine conditions.

Quiz criterion:

- response kind: value input;
- assessment goal: predict the Boolean result of the sample expression;
- expected answer: `true`;
- normalization: trim and ignore case;
- feedback is human-authored in the manifest.

The quiz criteria are specifications only. Prompt 07 does not evaluate answers or show an exercise interaction.

## Safe Snippet Subset

Global limits:

- maximum source length: 1,200 characters;
- maximum nesting depth: 3;
- execution policy: `static-analysis-only`.

Allowed constructs:

- local variable declarations and literals;
- assignment and arithmetic expressions;
- comparison and Boolean expressions;
- `Console.ReadLine` and `Console.WriteLine` concepts;
- sequential statements;
- `if`/`else`;
- bounded `for` and `while` loops;
- method declarations, calls, and return statements;
- basic array declarations and indexing;
- comments.

Forbidden capabilities:

- file-system access;
- network access;
- process launching;
- reflection;
- unsafe code;
- dynamic code generation;
- package loading;
- threading.

Allowed constructs describe curriculum intent, not an implemented parser. Roslyn-based enforcement belongs to a later analysis task. CodeLogic still must not execute learner code in the web process.

## Validation Rules

`CurriculumManifestValidator` verifies:

- schema version is `1`;
- all nine required topic slugs exist;
- exactly nine lessons and exactly two complete pilots exist;
- topic, lesson, exercise, and order values are unique where required;
- every topic has a lesson and every lesson references a known topic;
- every exercise references its owning lesson ID;
- complete lessons have at least two sections;
- sample code exists and remains under the safe character limit;
- lesson concepts are declared by the global allowed set;
- safe allowed and forbidden values are non-blank, unique, and non-overlapping;
- the policy remains `static-analysis-only`;
- quiz kinds, normalization, goals, answers, and explanations are valid.

The seeded catalog validates itself during creation and refuses to expose an invalid manifest.

## Display Behavior

The `/lessons` route displays:

- curriculum counts and safe nesting limit;
- both complete pilot cards with objective, section count, practice type, sample code, and assessment goal;
- all seven draft titles, difficulty labels, and objectives;
- a visible static-analysis-only safety notice.

Each complete pilot links to its lesson reading route. A lesson detail page displays the objective, ordered explanations, sample code, practice preview, safe-concept list, honest completion-state stub, and previous/next links across complete lessons. Draft and unknown slugs render a recoverable unavailable state with a link back to the lesson catalog.

## Explicitly Deferred

- executable exercise controls and answer feedback;
- deterministic quiz evaluation;
- curriculum persistence or authoring UI;
- learner completion and progress state;
- Roslyn parsing and supported-subset enforcement;
- arbitrary code compilation or execution.
