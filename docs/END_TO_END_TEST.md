# Prompt 12 End-to-End Manual Test

## Purpose

This script proves registration, sign-in, sign-out, authorization, persistent progress, and cross-user isolation while keeping lessons available anonymously.

## Prerequisites

1. Open `CodeLogic.slnx` in Visual Studio.
2. Apply the migration from `docs/IDENTITY.md`.
3. Set `CodeLogic.Web` as the startup project and run with **Ctrl+F5**.
4. Prepare two unique test email addresses and two passwords that meet the displayed rules. Do not use a real password.

## Anonymous Learning

1. In a fresh InPrivate window, open `/lessons/variables-and-types`.
2. Confirm the full lesson and sample code are readable.
3. Confirm Practice offers **Sign in** and **Create account** instead of an answer form.
4. Open `/progress` and confirm the app sends you to Sign in with a return URL.

## First Learner

1. Create the first account. Confirm registration signs you in and returns you to the requested page.
2. Open Variables and Types and submit `double`.
3. Confirm the saved attempt is incorrect and the lesson remains incomplete.
4. Submit `int` and confirm the correct result completes the lesson.
5. Open Progress. Expect two saved attempts, `1 of 2` lessons complete, and `50%`.
6. Sign out. Confirm the home page no longer displays the account and `/progress` requires sign-in.
7. Sign in with an incorrect password five times. Confirm the generic error changes to a temporary lockout message. Use a disposable account for this check because lockout lasts 15 minutes.

## Cross-User Isolation

1. In another InPrivate window, create the second account.
2. Open Progress. Expect zero attempts and `0 of 2` lessons complete.
3. Confirm no first-account email, attempt, or completion appears.
4. Sign out and sign back in as the first account. Confirm its attempts are still present.

## Restart Persistence

1. Stop and restart `CodeLogic.Web`.
2. Sign in as the first account.
3. Open Progress and confirm its two attempts and completed lesson remain.

## Accessibility and Security Smoke Checks

1. Complete registration, sign-in, quiz submission, and sign-out using only the keyboard.
2. Confirm every field has a visible label and focus remains visible.
3. Confirm errors are readable without relying on color.
4. At 200% zoom and 320 CSS pixels wide, confirm account forms do not require page-level horizontal scrolling.
5. Confirm the browser stores an authentication cookie named `CodeLogic.Identity`; do not edit or share its value.
6. Confirm a signed-out direct POST to a quiz or logout endpoint is rejected or challenged.
