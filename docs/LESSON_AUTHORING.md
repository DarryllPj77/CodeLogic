# Lesson Authoring and Content Management

## Document Status

- Task: Prompt 20 — Lesson authoring and content management
- Last updated: 2026-11-01
- Workflow: Administrator-owned draft, preview, version, and publish

## Editorial Workflow

CodeLogic keeps authored lessons separate from the original typed curriculum manifest. An administrator creates a valid private draft at `/admin/lessons`, reviews the same objective, prose, and code that learners will see, saves additional versions, and explicitly publishes it. Published lessons appear on `/lessons` and use `/authored-lessons/{id}`.

Publication is one-way in this first workflow. A published record is read-only; unpublishing, revising published content, approvals by a second editor, and scheduled publication need separate product decisions and migrations.

## Authorization and Ownership

- ASP.NET Core Identity supplies authentication and the `Administrator` role.
- The authoring page requires that role through route authorization.
- The authenticated `NameIdentifier` claim becomes the owner; the browser does not submit an owner ID.
- Infrastructure verifies ownership again for every update and publish operation.
- Another administrator cannot edit or publish a lesson they do not own.
- The public queries return only `Published` records, so a draft ID is unavailable on the learner route.

The migration seeds the role definition but deliberately does not promote an account. This prevents the first registrant, or any public registrant, from silently receiving administrative power.

## Local Administrator Setup

First apply migrations, register the intended owner through the normal account page, then stop the app. In a local development database only, use a SQLite browser or `sqlite3` to inspect the exact user and add the role association:

```sql
SELECT Id, Email FROM AspNetUsers;

INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES ('PASTE-THE-REVIEWED-USER-ID', '00000000-0000-0000-0000-000000000001');
```

Do not guess the user ID, run this against production, or assign the role to an unverified account. A hosted environment needs a reviewed, auditable administrator-provisioning process rather than direct database editing.

## Validation, Versioning, and Audit Basics

Every draft and publication requires non-blank title, objective, lesson content, and sample code. Limits are 200, 1,000, 10,000, and 10,000 characters respectively. Sample code uses the shared analysis limit; Application validation is authoritative even though the UI also exposes matching limits.

Version starts at 1 and increments on each successful update and publication. Updates carry the expected version; stale writes receive a conflict instead of silently overwriting newer content. Each record stores owner, created UTC, updated UTC, and optional published UTC timestamps. The public page shows the published version but does not expose the owner's account identifier.

## Safety Boundary

Authored sample code is trusted editorial text, not a public runnable-code submission. The workflow displays it with normal encoded Blazor output. It does not compile, dynamically load, or execute it, and it does not add a public authoring endpoint.

## Manual Visual Studio Check

1. Apply the migration described in `docs/PERSISTENCE.md`, register a local owner account, and assign the fixed Administrator role using the reviewed user ID above.
2. Start `CodeLogic.Web` with **Ctrl+F5** and sign in as a learner without the role. Confirm `/admin/lessons` shows the access-denied experience and the authoring navigation item is absent.
3. Sign in as the administrator and open **Author lessons**.
4. Submit blank fields. Confirm validation text appears and no lesson is added.
5. Create a valid draft. Confirm it shows Draft, version 1, owner-scoped audit time, and a private preview.
6. In a signed-out or InPrivate window, confirm the draft is absent from `/lessons` and its `/authored-lessons/{id}` route is unavailable.
7. Edit and save the draft. Confirm the version increments and the preview changes.
8. Publish it. Confirm it becomes read-only, appears on `/lessons`, and its learner page displays the objective, content, and sample code.
9. At 200% zoom and 320 CSS pixels, confirm the authoring fields, errors, list, preview, and public lesson remain readable and keyboard reachable.

## Known Limitations

- Drafts require all four fields; partial/incomplete draft records are not supported yet.
- Published lessons cannot be revised or unpublished.
- There is no approval queue, scheduled publication, revision history table, rich-text editor, media upload, or deletion workflow.
- Version and timestamps provide basic audit fields, not a complete immutable audit log.
- Local administrator assignment is manual; public deployment requires a secure provisioning process.
