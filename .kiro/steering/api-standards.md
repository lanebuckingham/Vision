---
inclusion: auto
name: vision-api-standards
description: REST API design, ASP.NET Core endpoints, DTOs, HTTP semantics, validation, pagination, authorization, and error responses for Vision.
---

# Vision REST API Standards

## Style

Use resource-oriented REST APIs.

Prefer nouns in endpoint paths.

Examples:

```text
GET    /api/v1/assets
GET    /api/v1/assets/{id}

GET    /api/v1/incidents
GET    /api/v1/incidents/{id}
POST   /api/v1/incidents
PATCH  /api/v1/incidents/{id}

GET    /api/v1/work-orders
POST   /api/v1/work-orders

POST   /api/v1/credentials/{id}/revoke
```

Action-style subresources are acceptable when they represent a meaningful domain transition, such as credential revocation.

## Versioning

Use `/api/v1/` for the MVP unless an approved API specification chooses another consistent versioning mechanism.

## Status Codes

Use HTTP semantics consistently.

- `200 OK` — successful read/update returning content
- `201 Created` — successful resource creation
- `204 No Content` — successful mutation with no response body
- `400 Bad Request` — malformed/validation failure
- `401 Unauthorized` — missing/invalid authentication
- `403 Forbidden` — authenticated but not permitted
- `404 Not Found` — requested resource does not exist
- `409 Conflict` — state/concurrency/business conflict
- `500 Internal Server Error` — unexpected server failure

Do not return `200 OK` for known errors.

## Error Shape

Prefer RFC Problem Details-compatible responses.

Validation responses should make field-level failures understandable.

Do not leak implementation details.

## DTO Boundaries

Use explicit request/response contracts.

Do not bind persistence entities directly to public API input.

Avoid exposing internal fields merely because they exist in the database.

## Lists

List APIs should support paging when result sets can grow.

Typical query parameters:

```text
page
pageSize
search
status
type
buildingId
```

Provide stable defaults and enforce a reasonable maximum page size.

## Updates

Use explicit commands/request DTOs for meaningful state transitions.

Protect state transitions from invalid changes.

Example:

```text
New -> Assigned -> In Progress -> Completed
```

Do not allow arbitrary state mutation that bypasses domain rules.

## Authentication and Authorization

All non-public APIs require Cognito authentication unless explicitly documented otherwise.

Authorization must be enforced by the API.

Frontend visibility is not a security boundary.

## Idempotency

Design operations that may be retried so duplicate execution does not create harmful duplicate effects.

This is especially important for:

- SQS consumers
- Credential revocation
- Work-order creation from integration events

## Cancellation

Pass request cancellation tokens through controller/endpoint, application, persistence, and supported infrastructure calls.

## OpenAPI

Keep OpenAPI/Swagger accurate enough to serve as a useful developer contract.

Document:

- Request models
- Response models
- Important status codes
- Authentication requirements
