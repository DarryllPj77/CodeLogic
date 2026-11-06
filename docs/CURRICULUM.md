# CodeLogic Curriculum Specification

## Document Status

- Status: Updated through Prompt 20
- Last updated: 2026-11-01
- Manifest type: EF-backed runtime catalog with typed migration seed
- Schema version: 1
- Complete pilots: 2
- Draft lessons: 7

## Purpose

The curriculum manifest is the single application contract for the initial learning sequence. It describes what each lesson intends to teach without placing curriculum content inside Blazor components.

The manifest contract lives in `CodeLogic.Application/Curriculum`. The reviewed typed seed lives in `CodeLogic.Infrastructure/Curriculum/SeededCurriculumCatalog.cs` and supplies deterministic rows to the initial migration. At runtime, `EfCurriculumCatalog` reads SQLite through `CodeLogicDbContext`, reconstructs the manifest, and validates it before returning it through `ICurriculumCatalog`.

This separation keeps EF Core and SQLite out of Domain, Application, and Blazor components. Prompt 20 adds a separate authored-lesson application contract and table rather than weakening the fixed manifest's validation rules.

## Persistence Behavior

- Topics and lesson specifications are stored in SQLite and survive application restarts.
- Sections, allowed concepts, and answer options are small JSON values inside lesson rows.
- The initial migration inserts two complete pilots and seven drafts with deterministic IDs.
- The application never calls `EnsureCreated`; tests and development use the real migration.
- Curriculum migrations are explicit and are not applied automatically at web startup.
- Learner attempts and completion remain separate from curriculum content and are saved only for the authenticated owner.

See [PERSISTENCE.md](PERSISTENCE.md) for apply, migration, reset, and test instructions.

## Manifest Shape

| Element | Purpose |
| --- | --- |
| `SchemaVersion` | Identifies the supported manifest contract. The initial version is `1`. |
| `SafeSnippetPolicy` | Defines size, nesting, allowed constructs, forbidden capabilities, and the no-execution policy. |
| `Topics` | Supplies stable topic IDs, URL-safe slugs, titles, and descriptions. |
| `Lessons` | Supplies ordering, status, difficulty, Domain lesson and exercise models, sample code, allowed concepts, and quiz criteria. |
| `QuizCriteria` | Defines the response kind, assessment goal, expected answer, normalization rule, human-authored explanation, and multiple-choice options when applicable. |

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
- choices: `int`, `double`, and `string`;
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

## Deterministic Evaluation Rules

The Application-layer evaluator compares submitted text with the expected answer from `QuizCriteria`:

| Rule | Whitespace | Letter casing | Example |
| --- | --- | --- | --- |
| `Exact` | Preserved | Preserved | Only `int` matches `int`; `Int` and ` int ` do not. |
| `Trim` | Leading and trailing whitespace removed | Preserved | ` Ready ` matches `Ready`; `ready` does not. |
| `TrimAndIgnoreCase` | Leading and trailing whitespace removed | Ignored | ` TRUE ` matches `true`. |

Blank answers receive an answer-required result without revealing the explanation. Valid correct and incorrect submissions receive concise feedback and the human-authored explanation. Multiple-choice specifications must define at least two unique non-blank choices containing the expected answer. Value-input specifications cannot define choices.

Lesson practice uses authorized, antiforgery-protected HTML POST forms so refreshes do not create duplicate attempts. Responses are evaluated as text and are never compiled or executed. Correct and incorrect outcomes are saved for the authenticated owner; raw answers are not stored in progress.

## Safe Snippet Subset

Global limits:

- maximum source length: 10,000 characters through the shared analysis policy;
- maximum nesting depth: 3;
- execution policy: `static-analysis-only`.

Allowed constructs:

- local variable declarations and literals;
- assignment and arithmetic expressions;
- comparison and Boolean expressions;
- `Console.ReadLine` and `Console.WriteLine` concepts;
- sequential statements;
- `if`/`else`;
- bounded `for`, `while`, and `do-while` loops;
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

Allowed constructs describe curriculum intent. The Roslyn validator enforces the implemented subset, while CodeLogic still must not execute learner code in the web process.

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
- published administrator-authored lesson cards when they exist;
- a visible static-analysis-only safety notice.

Each complete pilot links to its lesson reading route. A lesson detail page displays the objective, ordered explanations, sample code, deterministic practice form and feedback, safe-concept list, current-session completion status, and previous/next links across complete lessons. Manifest drafts are no longer advertised publicly. Authored lessons appear only after publication; draft and unknown identifiers render a recoverable unavailable state.

## Explicitly Deferred

- editing or promoting the seeded manifest through the authoring UI;
- multi-editor approval, unpublishing, and scheduled publication;
- arbitrary code compilation or execution.
