# ADR 0002: Blazor Web App on ASP.NET Core

- Status: Proposed
- Date: 2026-10-31

## Context

CodeLogic needs an accessible browser experience for lessons, exercises, progress, and interactive explanations. The owner is learning C# and .NET on Windows with Visual Studio, so a C#-centered full-stack path reduces the number of languages and toolchains required initially.

## Decision

Use the .NET 10 **Blazor Web App** template hosted by ASP.NET Core. Begin with server-rendered pages and add interactive rendering only where the user experience requires it.

ASP.NET Core provides hosting, dependency injection, configuration, logging, security middleware, HTTP endpoints, and later ASP.NET Core Identity integration. Blazor components remain presentation code and call Application use cases rather than EF Core directly.

## Alternatives Considered

### ASP.NET Core MVC or Razor Pages

Both are mature and suitable for content-heavy applications. They require more explicit JavaScript or another client approach for highly interactive learning and visualization experiences. Razor Pages remains a reasonable fallback if interactivity proves limited.

### Separate SPA and ASP.NET Core API

React, Angular, or Vue would provide a rich client ecosystem, but would add a second language, build system, dependency graph, and duplicated client/server contracts before CodeLogic needs them.

### .NET Aspire starter architecture

Aspire is useful for orchestrating distributed applications and their dependencies. CodeLogic begins as one application, so Aspire would introduce concepts without an immediate operational benefit.

## Consequences and Tradeoffs

### Benefits

- The primary stack remains C#/.NET from UI through application logic.
- Visual Studio debugging and local startup stay approachable.
- Server rendering supports quick first load and progressive interactivity.
- ASP.NET Core supplies a mature security and hosting foundation.

### Costs

- Render modes and component lifecycle require deliberate teaching and testing.
- Long-lived interactive circuits, if used, have scaling and reconnect considerations.
- The JavaScript ecosystem may still be needed for editor or visualization integrations.

## Future Extraction Seams

Application use cases and explicit DTOs separate UI components from business behavior. A future SPA, mobile client, or public API can reuse the same application boundary without moving domain rules into the client.
