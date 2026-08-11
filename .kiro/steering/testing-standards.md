---
inclusion: auto
name: vision-testing-standards
description: Unit tests, integration tests, xUnit conventions, acceptance criteria, authorization tests, persistence tests, and failure-case testing for Vision.
---

# Vision Testing Standards

## Philosophy

Test important behavior, not arbitrary implementation details.

The one-week MVP does not require 100% coverage.

Prioritize confidence in:

- Business rules
- Authorization
- State transitions
- Persistence
- API contracts
- Asynchronous idempotency
- Critical demo workflows

## Unit Tests

Use xUnit.

Good unit-test candidates include:

- Incident severity/status rules
- Work-order transitions
- Credential revocation
- Validators
- Domain calculations
- Idempotency decisions that can be tested without infrastructure

Tests should be deterministic and independent.

## Integration Tests

Prioritize integration tests for:

- Creating and retrieving incidents
- Dashboard data
- Work-order create/update flow
- Credential revocation
- EF Core/PostgreSQL persistence behavior
- Authorization policies
- API validation
- Important SQS consumer behavior where practical

## Test Naming

Prefer behavior-oriented names.

Example:

```text
RevokeCredential_WhenCredentialIsActive_MarksCredentialRevoked
```

or another consistent readable convention.

## Arrange / Act / Assert

Tests should make setup, action, and expected behavior easy to identify.

Avoid excessive shared setup that obscures the scenario.

## Authorization Tests

Test both allowed and forbidden behavior.

Examples:

- Credential Administrator can revoke a credential.
- Technician cannot revoke a credential.
- Technician can update an assigned work order when permitted.
- Unauthenticated requests to protected APIs are rejected.

## SQS / Messaging Tests

Where feasible, test:

- Duplicate message processing
- Idempotency
- Invalid message handling
- Retry-safe behavior
- Correlation identifier propagation

Do not write brittle tests that depend unnecessarily on live AWS resources.

## Database Testing

Use realistic relational behavior.

Do not replace every persistence test with mocked repositories if doing so hides EF Core/PostgreSQL behavior that matters.

## Acceptance Criteria

For every meaningful feature, define expected success and failure scenarios before considering it complete.

Example: credential revocation

1. Active credential revocation succeeds.
2. `RevokedAt` is recorded.
3. Repeated revocation does not create harmful duplicate effects.
4. Unauthorized role receives 403.
5. Missing credential receives 404.
