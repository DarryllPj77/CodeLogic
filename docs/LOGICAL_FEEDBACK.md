# Instructor-Defined Logical Feedback

## Document Status

- Task: Prompt 19 — Logical mistake feedback
- Last updated: 2026-11-01
- First known exercise: Adult age boundary
- Safety model: authored cases executed only through the bounded pedagogical interpreter

## Purpose

Logical feedback answers a narrow question: “For this known exercise, did the learner's supported snippet produce the instructor's expected output for these specific inputs?” It does not inspect arbitrary intent and does not claim to detect every logical error.

Syntax and supported-subset diagnostics remain separate. If the snippet is malformed, cannot be traced, omits the known `age` input, or reaches a trace limit, CodeLogic returns diagnostics and no logical pass/fail verdict.

## Adult Age Boundary Exercise

The authored requirement is:

> Display `Adult` for age 18 or older and `Minor` for younger ages.

The specification keeps one initialized integer variable named `age`. The evaluator uses Roslyn syntax nodes to replace only that initializer for each case; it does not use regex replacement, compile the snippet, or invoke learner code through .NET.

| Case | Input | Expected output | Why it matters |
| --- | ---: | --- | --- |
| Just below | `17` | `Minor` | Confirms the younger path. |
| Exact boundary | `18` | `Adult` | Distinguishes `>= 18` from the common `> 18` mistake. |
| Just above | `19` | `Adult` | Confirms values beyond the threshold stay on the adult path. |

For every case, the UI displays expected output, actual simulated output, Pass/Fail, and a human-authored hint when it fails. The exact boundary is visibly labeled without relying on color alone.

## False Positives and Honest Limits

Passing all three cases is evidence only for ages 17, 18, and 19. For example, this condition passes all authored cases but is still wrong for age 20:

```csharp
if (age == 18 || age == 19)
```

CodeLogic therefore always displays the coverage limitation beside completed results. It does not say “your logic is correct”; it says the known cases passed.

Adding more authored cases can reduce false positives, but no finite example set proves arbitrary program correctness. Broader property-based reasoning, formal verification, AI-generated intent detection, and general semantic analysis are outside this task.

## Result Boundary

`ILogicalFeedbackService` returns one of:

- `Passed`: all instructor-defined cases matched;
- `Failed`: at least one case produced different output;
- `CannotEvaluate`: syntax, supported-subset, operation-limit, or known-exercise setup prevented a fair logical check.

A `CannotEvaluate` result contains diagnostics and no partial logical results. Compiler-style syntax errors are not mislabeled as logical mistakes.

## Manual Visual Studio Check

1. Open `CodeLogic.slnx`, run `CodeLogic.Web`, and open **Playground**.
2. Choose **Load age exercise**, then **Check known cases**. Confirm 17, 18, and 19 pass and 18 is labeled Boundary.
3. Change `>=` to `>`. Check again and confirm only age 18 fails with expected `Adult`, actual `Minor`, and a `>= 18` hint.
4. Remove a parenthesis. Confirm syntax diagnostics appear under “Fix syntax or supported-subset issues first” with no logical verdict.
5. Replace the condition with `age == 18 || age == 19`. Confirm all known cases pass while the limitation explicitly warns that age 20 can still be wrong.
6. Repeat using only the keyboard, at 200% zoom, and at 320 CSS pixels. Confirm the result table scrolls within its container rather than widening the page.
