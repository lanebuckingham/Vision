---
inclusion: fileMatch
fileMatchPattern:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
---

# Vision .NET Engineering Standards

## General

Write clear, modern, idiomatic C#.

Prefer simple code over clever code.

Use nullable reference types.

Use async APIs for I/O-bound work.

## Async and Cancellation

- Use async/await for database, HTTP, messaging, and other I/O.
- Do not use `.Result` or `.Wait()` in normal application code.
- Accept and propagate `CancellationToken` through asynchronous request paths.
- Pass cancellation tokens to EF Core, HTTP, and other supported async APIs.
- Do not create unnecessary `Task.Run` wrappers around asynchronous I/O.

## ASP.NET Core

Keep controllers/endpoints thin.

Controllers should primarily:

1. Accept HTTP input.
2. Bind/validate request information.
3. Enforce authorization through framework policies.
4. Delegate application behavior.
5. Translate application results into HTTP responses.

Business rules belong in application/domain logic.

## Dependency Injection

Use constructor injection.

Choose lifetimes intentionally.

Do not use service locator patterns.

Do not resolve dependencies manually from `IServiceProvider` unless framework integration genuinely requires it.

## Logging

Use `ILogger<T>` through dependency injection.

Prefer structured logging:

```csharp
logger.LogInformation(
    "Work order {WorkOrderId} assigned to technician {TechnicianId}",
    workOrderId,
    technicianId);
```

Do not construct important log messages through string concatenation.

Never log:

- Passwords
- Access tokens
- Refresh tokens
- Secrets
- Sensitive credential values

## Validation

Use FluentValidation for request/application validation where appropriate.

Validation failures should return predictable client-facing errors.

Do not duplicate the same validation rules across controllers and handlers.

## CQRS / MediatR

Use CQRS/MediatR selectively.

Good candidates:

- Business-changing commands
- Non-trivial queries
- Cross-cutting pipeline behavior
- Operations with meaningful validation/business rules

Do not create commands/handlers for trivial behavior merely to satisfy a pattern.

## Entity Framework Core

- Use async query APIs.
- Avoid N+1 query patterns.
- Project only data needed for read endpoints when practical.
- Use `AsNoTracking()` for read-only queries where appropriate.
- Add indexes to support known list/filter/dashboard queries.
- Keep migrations in source control.
- Do not expose EF entities directly as public API contracts when separate DTOs provide useful boundary protection.

## Error Handling

Use centralized exception/problem handling.

Return consistent Problem Details-style errors where practical.

Do not expose stack traces or internal exception details to API clients.

## Security

Treat client input as untrusted.

Enforce authorization server-side even if the frontend hides controls.

Use policy-based authorization where behavior is permission-sensitive.

## Maintainability

Avoid:

- God classes
- Giant controllers
- Generic repository layers that add no value
- Unnecessary abstraction over EF Core
- Static mutable state
- Hidden side effects

Favor domain-specific names and explicit behavior.
