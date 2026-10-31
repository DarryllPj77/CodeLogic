# Prompt 10 End-to-End Manual Test

## Purpose

This script proves the first complete CodeLogic learning journey: read a lesson, submit practice attempts, complete the lesson, and see the same browser session update its dashboard.

Progress is intentionally temporary. It is stored in server memory and identified by a session cookie; it is not connected to an account or database.

## Prerequisites

1. Open `CodeLogic.slnx` in Visual Studio.
2. Set `CodeLogic.Web` as the startup project.
3. Run with **Ctrl+F5** or the HTTP launch profile.
4. Use a fresh InPrivate window for a predictable empty session.

## Variables and Types Journey

1. Open **Progress** from the primary navigation.
   - Expect `0 of 2 pilot lessons complete`.
   - Expect `0` attempts and the `No attempts yet` message.
   - Confirm the temporary-session notice says state can disappear.
2. Select **Start learning**.
   - Expect the Variables and Types lesson.
   - Expect its sidebar status to say `Not complete`.
3. In Practice, choose `double` and select **Check answer**.
   - Expect `Try again` and the human-authored explanation.
   - Expect the lesson to remain `Not complete`.
4. Open **Progress**.
   - Expect one incorrect attempt.
   - Expect `0 of 2 pilot lessons complete`.
5. Return to Variables and Types, choose `int`, and select **Check answer**.
   - Expect `Correct answer`.
   - Expect the sidebar to say `Complete this session`.
   - Expect a link to updated session progress.
6. Follow that link.
   - Expect two attempts: attempt 1 incorrect and attempt 2 correct.
   - Expect `1 of 2 pilot lessons complete` and `50% complete this session`.
   - Expect Operators to be recommended next.

## Complete the Second Pilot

1. Open Operators from the recommendation.
2. Enter ` TRUE `, including surrounding spaces, and select **Check answer**.
   - Expect the normalized answer to be correct.
3. Open Progress.
   - Expect `2 of 2 pilot lessons complete` and `100% complete this session`.
   - Expect three total attempts and two correct attempts.
   - Expect the pilot-complete message.

## Session Isolation and Honesty

1. Keep the original browser window open.
2. Open a separate InPrivate window and navigate directly to `/progress`.
   - Expect the new session to start at `0 of 2` with no attempts.
3. Return to the original window and refresh Progress.
   - Expect its completed lessons and attempt history to remain while that session is active.
4. Stop and restart `CodeLogic.Web`.
   - Progress may disappear because the backing store is process memory. This is expected for Prompt 10.

## Accessibility Smoke Check

1. Repeat one quiz using only `Tab`, arrow keys, and `Enter`.
2. Confirm focus outlines remain visible.
3. At 200% zoom and at 320 CSS pixels wide, confirm the progress summary, lesson cards, attempt history, and next action do not overlap or require page-level horizontal scrolling.
4. Confirm correct, incorrect, complete, and incomplete states remain understandable without relying on color.
