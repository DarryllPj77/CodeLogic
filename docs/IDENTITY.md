# Identity and Owned Progress

## Document Status

- Task: Updated through Prompt 23 — security hardening
- Last updated: 2026-11-01
- Account system: ASP.NET Core Identity with EF Core stores

## Anonymous Study Decision

Anonymous visitors may browse the home page, curriculum list, lesson explanations, and code samples. An account is required to submit a quiz or view saved progress.

This keeps the learning material accessible while giving every durable attempt and completion record a trustworthy owner. CodeLogic does not migrate temporary anonymous activity into a new account; a learner signs in before creating saved work.

## Account Security

CodeLogic delegates password validation and hashing to ASP.NET Core Identity. It never stores a readable password or implements a custom password algorithm.

Current settings:

- unique email required;
- minimum 12-character password;
- uppercase, lowercase, number, and symbol required;
- five failed sign-in attempts trigger a 15-minute lockout;
- authentication cookie is HTTP-only, SameSite Lax, essential, and secure whenever the request is HTTPS;
- cookie lifetime is two hours with sliding renewal;
- account forms and quiz submission use antiforgery validation;
- sign-out is a protected POST, not a state-changing GET;
- login errors do not reveal whether an email exists.
- server-side input caps reject email over 254 characters, password input over 128 characters, and local return URLs over 2,048 characters before password hashing or redirect handling;
- request/form limits reject oversized submissions independently of browser `maxlength` attributes.

Email confirmation, password reset email, two-factor authentication, and external providers are not implemented because Prompt 12 does not introduce an email service or recovery channel. `RequireConfirmedAccount` therefore remains false and must be revisited before public deployment.

## Ownership Boundary

Web obtains the owner ID from the authenticated `NameIdentifier` claim. No progress endpoint accepts a user ID supplied by a browser.

`IUserLearningProgressStore` is the Application boundary. `EfUserLearningProgressStore` filters every read and write by that trusted user ID. The database reinforces ownership with foreign keys to `AspNetUsers`, a composite completion key `(UserId, LessonId)`, and a unique attempt-number index `(UserId, ExerciseId, AttemptNumber)`.

Only attempt outcome, attempt number, exercise/lesson identifiers, UTC timestamp, and completion are saved. Learner answers are evaluated deterministically and are not stored.

Prompt 20 seeds an `Administrator` role definition and protects `/admin/lessons` with role authorization. No user is automatically promoted. The authenticated claim identifies the lesson owner, and the persistence adapter rejects another administrator's update or publish attempt. Local role assignment and hosted-provisioning limitations are documented in `docs/LESSON_AUTHORING.md`.

## Apply the Migration

From the repository root in PowerShell:

```powershell
dotnet ef database update --project src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj --startup-project src/CodeLogic.Web/CodeLogic.Web.csproj --context CodeLogicDbContext
```

The `IdentityAndOwnedProgress` migration adds standard ASP.NET Core Identity tables plus `UserExerciseAttempts` and `UserLessonProgress`. The application still does not run migrations automatically.

## Manual Visual Studio Check

1. Apply the migration and start `CodeLogic.Web` with **Ctrl+F5**.
2. Open a lesson while signed out. Confirm the lesson is readable but Practice asks you to sign in.
3. Create an account with a unique email and a password matching the displayed rules.
4. Submit an incorrect answer and then a correct answer.
5. Open **Progress** and confirm both attempts appear and the lesson is complete.
6. Sign out. Confirm `/progress` returns to the sign-in page.
7. Create a second account. Confirm it begins with zero attempts and zero completed lessons.
8. Restart the application, sign in as the first account, and confirm its progress remains.

## Known Limitations

- Account email ownership is not verified yet.
- Password reset, two-factor authentication, account deletion, and profile management are deferred.
- Concurrent submissions for the same exercise are protected by a unique database index but are not retried automatically; a later concurrency task can add a friendly retry if real usage demonstrates the need.
- SQLite is the development provider; hosted-provider behavior still requires provider-specific tests.
- Administrator provisioning is manual for local development and needs a secure audited process before public hosting.
- Per-IP/account rate limiting, email confirmation, MFA, and breached-password screening remain deployment prerequisites rather than implemented controls.
