# Vision — SecurityOperationsService Detailed Specification

**Status:** MVP implementation specification  
**Service:** `SecurityOperationsService`  
**Target implementation agent:** Amazon Kiro  
**Target repository location:** `docs/service-specifications/security-operations-service.md`  
**Depends on:** `README.md`, `docs/technology-specification.md`, `docs/business-domain-specification.md`  
**Scope:** One-week Vision MVP

---

# 1. Purpose

This document defines the detailed implementation contract for Vision's `SecurityOperationsService`.

The service is the first backend vertical slice because it powers the most important employer-facing experience:

- the hospital security dashboard,
- security asset inventory,
- asset details,
- security incident management,
- the critical camera-outage demo path,
- and publication of the integration event that can create a maintenance work order.

Kiro should implement this specification without inventing additional domain entities, service responsibilities, state machines, or infrastructure patterns unless a clear implementation issue requires an explicit architectural decision.

The goal is a small, production-shaped service that is easy to understand, fast in the public demo, and sufficiently well-designed to demonstrate senior-level engineering judgment.

---

# 2. Service Mission

`SecurityOperationsService` answers:

> **What is the current physical-security operational state of Northstar Medical Center, what assets require attention, and what security incidents are active?**

It is authoritative for:

- hospital/facility reference structure used by security operations,
- physical-security assets,
- asset operational status,
- security incidents,
- security-operations dashboard metrics,
- critical operational alerts derived from its owned data.

It is **not** authoritative for:

- work-order lifecycle,
- technician assignment,
- technician notes,
- people/employee credential records,
- credential issuance,
- credential revocation,
- authentication identities.

---

# 3. Source-of-Truth Precedence

Implementation should follow these documents in this order where specificity differs:

1. `docs/business-domain-specification.md`
2. This `SecurityOperationsService` specification
3. `docs/technology-specification.md`
4. `README.md`

This service specification may add implementation detail but must not silently contradict the approved business/domain model.

---

# 4. MVP Service Boundary

## 4.1 Owned domain entities

`SecurityOperationsService` owns:

```text
Hospital
Building
Location
SecurityAsset
SecurityIncident
```

Relationships:

```text
Hospital 1 ─────────── * Building

Building 1 ─────────── * Location

Location 1 ─────────── * SecurityAsset

Location 1 ─────────── * SecurityIncident

SecurityAsset 1 ────── * SecurityIncident
                        (incident asset is optional)

SecurityIncident 1 ─── 0..1 WorkOrder
                        (external reference only)
```

`WorkOrder` is **not** an EF entity in this service.

## 4.2 Cross-service references

The service may store:

```text
SecurityIncident.WorkOrderId
```

as an external UUID reference after a work order is known.

That does **not** create an EF navigation property, a foreign key constraint to the `work_orders` schema, direct database ownership, or direct table joins across service schemas.

---

# 5. Project Structure Guidance

Kiro owns the working tree and may adapt folder naming to existing repository conventions, but the implementation should preserve clear separation similar to:

```text
src/
└── SecurityOperationsService/
    ├── Api/
    │   ├── Controllers/
    │   ├── Middleware/
    │   └── Contracts/
    ├── Application/
    │   ├── Assets/
    │   │   └── Queries/
    │   ├── Dashboard/
    │   │   └── Queries/
    │   ├── Incidents/
    │   │   ├── Commands/
    │   │   └── Queries/
    │   └── Common/
    ├── Domain/
    │   ├── Entities/
    │   ├── Enums/
    │   └── Exceptions/
    ├── Infrastructure/
    │   ├── Persistence/
    │   │   ├── Configurations/
    │   │   ├── Migrations/
    │   │   └── Seeding/
    │   └── Messaging/
    └── Program.cs
```

A simpler equivalent is acceptable.

Do **not** create excessive projects/assemblies solely to imitate enterprise Clean Architecture.

The service should remain understandable to a hiring manager reviewing the repository.

---

# 6. Technology Requirements

Use the Vision technology baseline:

- C#
- current project-selected modern .NET version
- ASP.NET Core Web API
- REST
- OpenAPI / Swagger
- Entity Framework Core
- PostgreSQL
- Npgsql provider
- FluentValidation
- MediatR where it materially improves command/query organization
- dependency injection
- `ILogger<T>`
- `async` / `await`
- `CancellationToken`
- OpenTelemetry instrumentation when the project reaches its observability phase
- Amazon SQS when the messaging phase is implemented
- xUnit for tests

Do not introduce MongoDB, DynamoDB, Redis, Kafka, RabbitMQ, EventBridge, Lambda, a custom authentication service, another message bus, or another persistence technology.

---

# 7. PostgreSQL Ownership

Use schema:

```text
security_operations
```

Business tables:

```text
security_operations.hospitals
security_operations.buildings
security_operations.locations
security_operations.security_assets
security_operations.security_incidents
```

Infrastructure-support tables may also exist in the same schema where justified, for example:

```text
security_operations.outbox_messages
```

An infrastructure table does not count as a new business-domain entity.

---

# 8. Identifier and Time Standards

Use UUID/`Guid` for all entity identifiers.

Recommended CLR type:

```csharp
Guid
```

Use UTC timestamps with:

```csharp
DateTimeOffset
```

Recommended PostgreSQL type:

```text
timestamp with time zone
```

All API timestamp values should serialize as ISO 8601.

---

# 9. Enum Persistence

Store domain enums as strings in PostgreSQL unless an established repository convention says otherwise.

Examples:

```text
Camera
Operational
Critical
Investigating
```

Apply explicit maximum lengths.

---

# 10. Domain Entity — Hospital

Properties:

| Property | CLR Type | Required | Database |
|---|---|---:|---|
| `Id` | `Guid` | Yes | `uuid` PK |
| `Name` | `string` | Yes | `varchar(200)` |
| `Code` | `string?` | No | `varchar(50)` |
| `CreatedAt` | `DateTimeOffset` | Yes | `timestamptz` |

Rules:

- `Name` cannot be blank.
- `Name` maximum 200 characters.
- `Code`, if supplied, maximum 50 characters.
- MVP seed uses one hospital: `Northstar Medical Center`.
- No tenant/customer/billing properties.

Relationship:

```text
Hospital 1 -> many Building
```

Delete behavior:

```text
Restrict
```

---

# 11. Domain Entity — Building

Properties:

| Property | CLR Type | Required | Database |
|---|---|---:|---|
| `Id` | `Guid` | Yes | `uuid` PK |
| `HospitalId` | `Guid` | Yes | `uuid` FK |
| `Name` | `string` | Yes | `varchar(150)` |
| `CreatedAt` | `DateTimeOffset` | Yes | `timestamptz` |

Rules:

- Hospital must exist.
- Name required.
- Name maximum 150 characters.

Recommended uniqueness:

```text
UNIQUE (hospital_id, name)
```

Delete behavior:

```text
Restrict
```

---

# 12. Domain Entity — Location

Examples:

- Pharmacy Storage
- Emergency Department Entrance
- ICU East Corridor
- Main Lobby
- Data Center Entrance

Properties:

| Property | CLR Type | Required | Database |
|---|---|---:|---|
| `Id` | `Guid` | Yes | `uuid` PK |
| `BuildingId` | `Guid` | Yes | `uuid` FK |
| `Name` | `string` | Yes | `varchar(150)` |
| `Floor` | `string?` | No | `varchar(20)` |
| `Department` | `string?` | No | `varchar(100)` |
| `Description` | `string?` | No | `varchar(500)` |
| `CreatedAt` | `DateTimeOffset` | Yes | `timestamptz` |

Rules:

- Building must exist.
- Name required.
- Name maximum 150.
- Floor maximum 20.
- Department maximum 100.
- Description maximum 500.

Recommended uniqueness:

```text
UNIQUE (building_id, name)
```

Do not create `Floor`, `Department`, or `AccessZone` top-level entities in the MVP.

---

# 13. Domain Entity — SecurityAsset

Properties:

| Property | CLR Type | Required | Database |
|---|---|---:|---|
| `Id` | `Guid` | Yes | `uuid` PK |
| `LocationId` | `Guid` | Yes | `uuid` FK |
| `Name` | `string` | Yes | `varchar(150)` |
| `AssetTag` | `string?` | No | `varchar(50)` |
| `AssetType` | `SecurityAssetType` | Yes | `varchar(50)` |
| `Status` | `SecurityAssetStatus` | Yes | `varchar(30)` |
| `Manufacturer` | `string?` | No | `varchar(100)` |
| `Model` | `string?` | No | `varchar(100)` |
| `Description` | `string?` | No | `varchar(500)` |
| `LastServiceAt` | `DateTimeOffset?` | No | `timestamptz` |
| `StatusChangedAt` | `DateTimeOffset?` | No | `timestamptz` |
| `CreatedAt` | `DateTimeOffset` | Yes | `timestamptz` |
| `UpdatedAt` | `DateTimeOffset` | Yes | `timestamptz` |

`SecurityAssetType` exactly:

```text
Camera
AccessControlledDoor
BadgeReader
SecurityGate
```

`SecurityAssetStatus` exactly:

```text
Operational
Degraded
Offline
```

Rules:

1. Location must exist.
2. Name required.
3. Name maximum 150.
4. Type and status must be valid enum values.
5. `StatusChangedAt`, when known, is when the current status took effect. Seeded records may leave it null when that history is unknown.
6. Any application-driven status change must set `StatusChangedAt = now` and update `UpdatedAt`.
7. `LastServiceAt` may be null.
8. No live hardware telemetry exists in MVP.
9. The service owns the displayed operational state.

Recommended unique constraint:

```text
UNIQUE (asset_tag) WHERE asset_tag IS NOT NULL
```

---

# 14. Domain Entity — SecurityIncident

Properties:

| Property | CLR Type | Required | Database |
|---|---|---:|---|
| `Id` | `Guid` | Yes | `uuid` PK |
| `LocationId` | `Guid` | Yes | `uuid` FK |
| `SecurityAssetId` | `Guid?` | No | `uuid` FK |
| `WorkOrderId` | `Guid?` | No | external UUID; no cross-schema FK |
| `Title` | `string` | Yes | `varchar(150)` |
| `Description` | `string` | Yes | `varchar(2000)` |
| `Severity` | `IncidentSeverity` | Yes | `varchar(20)` |
| `Status` | `IncidentStatus` | Yes | `varchar(30)` |
| `ResolutionSummary` | `string?` | No | `varchar(2000)` |
| `ResolvedAt` | `DateTimeOffset?` | No | `timestamptz` |
| `CreatedAt` | `DateTimeOffset` | Yes | `timestamptz` |
| `UpdatedAt` | `DateTimeOffset` | Yes | `timestamptz` |

`IncidentSeverity` exactly:

```text
Low
Medium
High
Critical
```

`IncidentStatus` exactly:

```text
Open
Investigating
Resolved
```

Valid transitions:

```text
Open ----------> Investigating ----------> Resolved
   \-------------------------------------> Resolved
```

Rules:

1. `LocationId` required.
2. Title required; max 150.
3. Description required; max 2000.
4. New incident always starts `Open`.
5. `SecurityAssetId` optional.
6. If asset supplied, it must exist and belong to the supplied location.
7. `ResolvedAt` must be null while active.
8. Resolving requires nonblank `ResolutionSummary`.
9. Resolving sets `ResolvedAt` and `UpdatedAt`.
10. Resolved is terminal.
11. Incident has at most one associated WorkOrder in MVP.
12. `WorkOrderId` is external only.
13. Critical incident with an asset qualifies for automatic work-order creation once SQS is enabled.

Meaningful behavior should live in entity/application logic, e.g.:

```text
SecurityAsset.ChangeStatus(...)
SecurityIncident.StartInvestigation(...)
SecurityIncident.Resolve(...)
SecurityIncident.AttachWorkOrder(...)
```

---

# 15. EF Core DbContext

Recommended:

```csharp
SecurityOperationsDbContext
```

Owned `DbSet`s:

```text
Hospitals
Buildings
Locations
SecurityAssets
SecurityIncidents
```

Do not include WorkOrders, Technicians, People, or Credentials.

---

# 16. Required Indexes

Buildings:

```text
(hospital_id)
UNIQUE (hospital_id, name)
```

Locations:

```text
(building_id)
UNIQUE (building_id, name)
```

Security assets:

```text
(location_id)
(status)
(asset_type)
(status, asset_type)
```

Optional:

```text
UNIQUE asset_tag WHERE asset_tag IS NOT NULL
```

Security incidents:

```text
(location_id)
(security_asset_id)
(status)
(severity)
(created_at DESC)
(status, severity)
```

Optional:

```text
UNIQUE work_order_id WHERE work_order_id IS NOT NULL
```

---

# 17. API Standards

Base route:

```text
/api/v1
```

Use:

```text
application/json
application/problem+json
```

List response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

Pagination:

```text
page default = 1
pageSize default = 25
pageSize min = 1
pageSize max = 100
```

Assets default sort:

```text
Name ASC
```

Incidents default sort:

```text
CreatedAt DESC
```

Do not build generic dynamic sorting infrastructure.

---

# 18. Asset API

Required:

```text
GET /api/v1/assets
GET /api/v1/assets/{id}
```

No general public asset create/update CRUD is required for the one-week MVP.

## GET /api/v1/assets

Query parameters:

```text
status
type
buildingId
locationId
search
page
pageSize
```

Search case-insensitively across:

- asset name,
- asset tag,
- location name,
- building name.

PostgreSQL `ILIKE` is appropriate.

Example response:

```json
{
  "items": [
    {
      "id": "99750ccc-976b-49ee-a485-f3677b9b91ef",
      "name": "Pharmacy Storage Camera 02",
      "assetTag": "CAM-PHARM-002",
      "assetType": "Camera",
      "status": "Offline",
      "building": {
        "id": "9ca90164-c910-44f6-98f0-142058ffdf1b",
        "name": "Main Hospital"
      },
      "location": {
        "id": "72533c8e-5541-48bd-8821-8ae4c434634f",
        "name": "Pharmacy Storage",
        "floor": "1",
        "department": "Pharmacy"
      },
      "lastServiceAt": "2026-07-22T14:30:00Z",
      "statusChangedAt": "2026-08-10T12:10:00Z"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1
}
```

## GET /api/v1/assets/{id}

Return full asset detail plus recent incidents.

Recommended limit:

```text
5 or 10 recent incidents
```

Unknown asset:

```text
404
```

---

# 19. Incident API

Required:

```text
GET   /api/v1/incidents
GET   /api/v1/incidents/{id}
POST  /api/v1/incidents
PATCH /api/v1/incidents/{id}
```

PATCH is a small state-update contract, not RFC 6902 JSON Patch.

## GET /api/v1/incidents

Query parameters:

```text
status
severity
assetId
buildingId
locationId
search
page
pageSize
```

Search case-insensitively across:

- title,
- description,
- asset name,
- location name.

## POST /api/v1/incidents

Authorized:

```text
SecurityManager
```

Request:

```json
{
  "locationId": "72533c8e-5541-48bd-8821-8ae4c434634f",
  "assetId": "99750ccc-976b-49ee-a485-f3677b9b91ef",
  "severity": "Critical",
  "title": "Pharmacy storage camera offline",
  "description": "Camera stopped responding and is not producing video."
}
```

Behavior:

```text
Id = new UUID
Status = Open
CreatedAt = now UTC
UpdatedAt = now UTC
ResolvedAt = null
ResolutionSummary = null
WorkOrderId = null
```

Success:

```text
201 Created
Location: /api/v1/incidents/{id}
```

## PATCH /api/v1/incidents/{id}

Request:

```json
{
  "status": "Investigating",
  "resolutionSummary": null
}
```

or:

```json
{
  "status": "Resolved",
  "resolutionSummary": "Camera restored after power and network connection were reset."
}
```

Valid:

```text
Open -> Investigating
Open -> Resolved
Investigating -> Resolved
```

Invalid backward transition:

```text
409 Conflict
```

Same-state requests should be idempotent. A second `Resolved` request must preserve the original `ResolvedAt`.

---

# 20. Optional Narrow Asset Status Command

The final repair demo requires the asset to return to `Operational`.

Preferred long-term mechanism:

```text
WorkOrderCompleted integration event
```

But a second async workflow should not block the one-week MVP.

Therefore Kiro may later add a narrowly scoped authenticated endpoint such as:

```text
PATCH /api/v1/assets/{id}/status
```

Request:

```json
{
  "status": "Operational"
}
```

Authorized:

```text
SecurityManager
```

Do not expand this into full asset administration CRUD.

---

# 21. Dashboard API

Required:

```text
GET /api/v1/dashboard
```

Important ownership rule:

This endpoint returns **security-operations data owned by this service**.

It must not directly query:

```text
work_orders.*
credentials.*
```

The Next.js dashboard should compose the additional counts from WorkOrderService and CredentialService.

Recommended SecurityOperations response:

```json
{
  "hospital": {
    "id": "d71c9475-fdb1-4d78-aa12-f9849de39dc2",
    "name": "Northstar Medical Center"
  },
  "securityHealth": {
    "operationalPercentage": 93,
    "operationalAssets": 52,
    "degradedAssets": 2,
    "offlineAssets": 2,
    "totalAssets": 56
  },
  "incidents": {
    "activeCritical": 1,
    "activeTotal": 4
  },
  "criticalAlerts": [
    {
      "incidentId": "2f785125-4630-43c1-ab30-239919cb4a57",
      "title": "Camera Offline — Pharmacy Storage",
      "severity": "Critical",
      "status": "Open",
      "assetId": "99750ccc-976b-49ee-a485-f3677b9b91ef",
      "assetName": "Pharmacy Storage Camera 02",
      "assetType": "Camera",
      "locationName": "Pharmacy Storage",
      "createdAt": "2026-08-10T12:12:00Z"
    }
  ],
  "recentActivity": [
    {
      "type": "IncidentCreated",
      "title": "Pharmacy storage camera offline",
      "occurredAt": "2026-08-10T12:12:00Z",
      "incidentId": "2f785125-4630-43c1-ab30-239919cb4a57",
      "assetId": "99750ccc-976b-49ee-a485-f3677b9b91ef"
    }
  ]
}
```

Metric rules:

```text
OperationalPercentage =
  OperationalAssets / TotalAssets * 100
```

Round to whole percentage.

Active critical incidents:

```text
Severity == Critical
AND Status != Resolved
```

Active total:

```text
Status != Resolved
```

Critical alerts:

```text
active Critical incidents
limit 5
CreatedAt DESC
```

Do not add an AuditEvent entity solely for recent activity.

---

# 22. Cross-Service Dashboard Composition

Final Next.js dashboard ownership:

```text
SecurityOperationsService
  -> security health
  -> assets
  -> critical incidents
  -> security alerts/activity

WorkOrderService
  -> open work-order count

CredentialService
  -> expiring-credential count
```

The frontend may issue these calls in parallel.

Do not create a fourth DashboardService.

Do not use direct cross-schema SQL joins.

---

# 23. Commands and Queries

Recommended MediatR application messages:

Queries:

```text
GetAssetsQuery
GetAssetByIdQuery
GetIncidentsQuery
GetIncidentByIdQuery
GetSecurityDashboardQuery
```

Commands:

```text
CreateIncidentCommand
UpdateIncidentStatusCommand
```

Potential later command:

```text
UpdateAssetStatusCommand
```

Use MediatR selectively, not mechanically.

---

# 24. Validation

Use FluentValidation for meaningful request/command validation.

Recommended validators:

```text
GetAssetsQueryValidator
GetIncidentsQueryValidator
CreateIncidentCommandValidator
UpdateIncidentStatusCommandValidator
```

Required checks include:

- pagination boundaries,
- valid enums,
- title required/max 150,
- description required/max 2000,
- resolution summary required when resolving,
- location exists,
- supplied asset exists and belongs to location.

---

# 25. HTTP Error Semantics

Use:

```text
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
500 Internal Server Error
```

Use ASP.NET Core Problem Details where practical.

Example 409:

```json
{
  "title": "Invalid incident status transition",
  "status": 409,
  "detail": "A resolved incident cannot be moved back to Investigating.",
  "traceId": "..."
}
```

Do not leak stack traces, connection strings, AWS credentials, or secrets.

---

# 26. Authentication and Authorization

Authentication:

```text
Amazon Cognito
OAuth / OIDC
JWT
Bearer
```

The approved demo model treats `SecurityManager` as the full-access demo role across Vision.

For this service:

| Endpoint | SecurityManager | Technician | CredentialAdministrator |
|---|:---:|:---:|:---:|
| GET dashboard | Yes | Optional | Optional |
| GET assets | Yes | Yes* | No |
| GET asset detail | Yes | Yes* | No |
| GET incidents | Yes | No | No |
| GET incident detail | Yes | No | No |
| POST incident | Yes | No | No |
| PATCH incident | Yes | No | No |
| PATCH asset status if added | Yes | No | No |

`*` Technician asset-read permission is useful only if the WorkOrder flow needs it.

Security Manager must also have Credential Administrator capabilities in CredentialService, per the approved demo behavior.

---

# 27. Integration Event — IncidentCreated

The MVP's one required meaningful asynchronous workflow is:

```text
Critical equipment incident created
    ↓
SecurityOperationsService
    ↓
IncidentCreated
    ↓
Amazon SQS
    ↓
WorkOrderService
    ↓
Maintenance WorkOrder created
```

Qualification rule:

```text
Incident.Severity == Critical
AND Incident.SecurityAssetId != null
```

Recommended event type:

```text
vision.security-operations.incident-created.v1
```

Recommended payload:

```json
{
  "eventId": "6ac3b602-51a5-470d-b0e8-686f5f713a04",
  "eventType": "vision.security-operations.incident-created.v1",
  "occurredAt": "2026-08-10T12:12:00Z",
  "correlationId": "1f8fdb31e3b84b25adcc2565ded23321",
  "incident": {
    "id": "2f785125-4630-43c1-ab30-239919cb4a57",
    "title": "Pharmacy storage camera offline",
    "description": "Camera stopped responding and is not producing video.",
    "severity": "Critical"
  },
  "asset": {
    "id": "99750ccc-976b-49ee-a485-f3677b9b91ef",
    "name": "Pharmacy Storage Camera 02",
    "assetTag": "CAM-PHARM-002",
    "assetType": "Camera"
  },
  "location": {
    "id": "72533c8e-5541-48bd-8821-8ae4c434634f",
    "name": "Pharmacy Storage",
    "buildingId": "9ca90164-c910-44f6-98f0-142058ffdf1b",
    "buildingName": "Main Hospital"
  }
}
```

Rules:

- unique `eventId`,
- UTC `occurredAt`,
- propagate correlation ID when available,
- do not serialize EF/domain entities directly,
- include enough display context that WorkOrderService does not query this database,
- explicit event version,
- WorkOrderService owns idempotent consumption.

---

# 28. Reliable Publication — Transactional Outbox

PostgreSQL and SQS cannot be committed atomically.

For the final MVP, use a lightweight transactional outbox when SQS is introduced.

Flow:

```text
POST incident
   ↓
DB transaction
   ├── insert security_incident
   └── insert outbox_message
   ↓ commit
background publisher
   ↓
Amazon SQS
   ↓
mark outbox message published
```

Recommended table:

```text
security_operations.outbox_messages
```

Suggested fields:

```text
id uuid PK
event_type varchar(200)
payload jsonb
occurred_at timestamptz
published_at timestamptz null
attempt_count integer
last_error varchar(2000) null
correlation_id varchar(100) null
```

Recommended partial/indexed lookup for unpublished messages.

Publisher behavior:

- poll unpublished messages,
- send to SQS,
- mark published only after success,
- leave failed messages unpublished,
- increment attempt count,
- log event/correlation IDs,
- respect shutdown cancellation,
- use a scoped DbContext inside the hosted service.

Messaging should be added only after the synchronous incident workflow works.

---

# 29. Logging and Observability

Use structured logs such as:

```text
Creating security incident {IncidentId} for asset {AssetId} at location {LocationId}
Security incident {IncidentId} moved from {OldStatus} to {NewStatus}
Queued integration event {EventId} for incident {IncidentId}
Published integration event {EventId} to SQS
```

Never log:

- bearer tokens,
- secrets,
- connection strings,
- AWS credentials.

When OpenTelemetry is added, instrument:

- ASP.NET Core requests,
- EF/PostgreSQL,
- SQS publication,
- errors,
- correlation across the future WorkOrder consumer.

Target trace:

```text
Browser
  ↓
SecurityOperationsService
  ↓
PostgreSQL
  ↓
SQS
  ↓
WorkOrderService
```

---

# 30. Seed Data

Seed:

```text
1 hospital
3 buildings
9–12 meaningful locations
~55 security assets
3–5 degraded/offline assets
several incidents
```

Required hospital:

```text
Northstar Medical Center
```

Recommended buildings:

```text
Main Hospital
Administrative Building
Data Center
```

Recommended locations:

```text
Main Lobby
Emergency Department Entrance
Pharmacy Storage
ICU East Corridor
Surgical Wing Staff Entrance
Administration Lobby
Records Storage Entrance
Data Center Entrance
Server Room Corridor
```

Suggested asset distribution:

```text
22 Cameras
14 AccessControlledDoors
13 BadgeReaders
6 SecurityGates
```

Most assets are `Operational`.

Required demo asset:

```text
Name: Pharmacy Storage Camera 02
AssetTag: CAM-PHARM-002
Type: Camera
Location: Pharmacy Storage
Building: Main Hospital
Status: Offline
```

Required demo incident:

```text
Title: Pharmacy storage camera offline
Severity: Critical
Status: Open
Asset: Pharmacy Storage Camera 02
Location: Pharmacy Storage
Description: Camera stopped responding and is not producing video.
```

Additional incidents should include:

- High / Investigating
- Medium / Open
- Low / Resolved

Seed data must be idempotent and should use stable deterministic GUIDs where helpful.

Do not hard-code the dashboard percentage separately from actual asset records.

---

# 31. Performance Requirements

This service is likely to be the strategically warm Azure Container App.

Targets:

- normal API calls preferably 300–500 ms or less,
- important initial experience under 2 seconds,
- no visible retry/wake-up failure,
- efficient dashboard query.

Do not add caching before measuring need.

For dashboard queries:

- use aggregate SQL/EF queries,
- use `AsNoTracking()`,
- avoid loading all entities into memory,
- avoid N+1.

For list queries:

1. apply filters,
2. apply search,
3. count,
4. order,
5. paginate,
6. project DTO.

---

# 32. Dependency Injection / Async Requirements

Typical lifetimes:

```text
DbContext -> Scoped
MediatR handlers -> default transient registration
validators -> default
AWS SQS client -> SDK-recommended singleton
outbox publisher -> hosted singleton, create scopes per iteration
```

Do not inject a scoped DbContext directly into a singleton hosted service.

All I/O should use async APIs.

Do not use:

```text
.Result
.Wait()
Task.Run() around EF/HTTP I/O
```

Propagate `CancellationToken`:

```text
Controller
  ↓
MediatR
  ↓
Handler
  ↓
EF Core / SaveChangesAsync
```

Hosted publishers use their stopping token.

---

# 33. Health Endpoints

Recommended:

```text
GET /health/live
GET /health/ready
```

`live` checks process health.

`ready` may include PostgreSQL connectivity.

Do not make liveness depend on SQS availability.

---

# 34. OpenAPI, CORS, Versioning

Swagger/OpenAPI should document:

- routes,
- query parameters,
- request/response shapes,
- auth requirements,
- status codes.

Versioning:

```text
/api/v1
```

No elaborate multi-version framework is required.

CORS should allow configured frontend origins. Production should not use unrestricted origins with credentials.

---

# 35. EF Migrations

Initial SecurityOperationsService migration should create only:

```text
security_operations schema
hospitals
buildings
locations
security_assets
security_incidents
indexes
```

A later messaging migration may add:

```text
outbox_messages
```

Do not create WorkOrder/Credential tables in these migrations.

---

# 36. Testing Strategy

Recommended structure:

```text
tests/
├── SecurityOperationsService.UnitTests/
└── SecurityOperationsService.IntegrationTests/
```

Equivalent simpler structure is acceptable.

Required unit tests:

### Incident lifecycle

- new incident defaults to Open,
- Open -> Investigating,
- Open -> Resolved,
- Investigating -> Resolved,
- resolve without summary rejected,
- Resolved -> Investigating rejected,
- Resolved -> Open rejected,
- repeated Resolved is idempotent and preserves original timestamp.

### Incident asset/location

- asset belongs to location: valid,
- mismatched asset/location: rejected,
- location-only incident: valid.

### Event qualification

- Critical + asset: qualifies,
- Critical + no asset: no maintenance event,
- High + asset: no automatic maintenance event,
- Medium/Low: no automatic event.

### Dashboard metric

Test calculation with:
- all operational,
- mixed statuses,
- zero assets.

Required integration tests:

1. GET assets returns persisted assets.
2. GET assets filters by status.
3. GET assets filters by type.
4. Search finds pharmacy camera.
5. Asset detail returns location and recent incidents.
6. Unknown asset => 404.
7. POST incident persists.
8. Mismatched asset/location fails.
9. GET incidents returns created incident.
10. PATCH Open -> Investigating persists.
11. PATCH -> Resolved persists resolution/timestamp.
12. Invalid backward transition => 409.
13. Dashboard returns database-derived counts.
14. Authorization behavior once Cognito is enabled.

Messaging tests once added:

- Critical asset incident writes exactly one outbox event.
- High incident writes no maintenance-trigger event.
- Failed publication remains unpublished.
- Successful publication sets `published_at`.

Protect demo seed landmarks in a seed integration test.

---

# 37. Controller and Persistence Guidance

Controllers should:

- accept HTTP input,
- enforce framework/auth concerns,
- delegate to application commands/queries,
- translate result to HTTP response.

Controllers should not:

- contain domain transitions,
- build SQS messages,
- contain complex EF queries,
- calculate dashboard rules inline.

Do not introduce generic repositories merely for architectural appearance.

EF Core `DbContext` may be used directly by handlers.

Avoid speculative:

```text
IRepository<T>
GenericRepository<T>
UnitOfWork wrapper around DbContext
```

Do not expose EF/domain entities directly over APIs.

Use DTOs and explicit projections.

A mapping library is not required; manual projection is preferred for this small service.

---

# 38. Security Considerations

- Validate all client input.
- Enforce authorization server-side.
- Do not trust hidden frontend controls.
- Use EF parameterization.
- Keep mutation surface small.
- Validate asset/location consistency.
- Do not expose secrets.
- Do not log tokens.
- Avoid unrestricted production CORS.
- Return generic 500 responses.

All seeded data must be fictional.

Do not seed real patient data, PHI, real employee PII, or real credential identifiers.

Vision is hospital-focused but the MVP is not a clinical data system.

---

# 39. Frontend Contract

Initial routes:

```text
/dashboard
/assets
/assets/[id]
/incidents
/incidents/[id]
```

Dashboard must be able to render:

- hospital name,
- operational percentage,
- operational/total assets,
- degraded/offline indicators,
- active critical incidents,
- critical alerts,
- recent security activity.

Asset list must support:

- browse,
- search,
- status filter,
- type filter,
- location context.

Asset detail must show:

- name,
- tag,
- type,
- status,
- building,
- location,
- floor/department,
- last service,
- manufacturer/model when present,
- recent incidents.

Incident list/detail must make Critical and active incidents obvious and support the state transitions defined above.

---

# 40. Acceptance Criteria

## Service foundation

- builds and starts,
- PostgreSQL connects,
- owned schema/migration correct,
- seed is idempotent,
- Swagger works,
- health endpoints work,
- logging structured,
- cancellation propagation present.

## Asset inventory

- list works,
- pagination works,
- filters work,
- search works,
- detail works,
- recent incident context included,
- unknown asset => 404,
- no N+1 query pattern.

## Incident management

- list/detail work,
- create works,
- new status Open,
- asset/location consistency enforced,
- valid transitions work,
- resolution requires summary,
- backward transitions => 409,
- resolved terminal,
- UTC timestamps correct.

## Dashboard

- hospital displayed,
- counts derived from persistence,
- percentage calculated,
- active-critical count correct,
- critical pharmacy alert visible,
- recent activity populated,
- no direct reads from work_orders or credentials schemas.

## Authorization

Once Cognito is enabled:

- SecurityManager can use all SecurityOperations actions,
- Technician cannot mutate incidents,
- CredentialAdministrator cannot mutate incidents,
- API enforces roles,
- unauthenticated protected mutations => 401.

## Messaging

Once SQS is enabled:

- Critical + asset creates one outbox event,
- v1 contract used,
- payload contains enough incident/asset/location context,
- publication retry safe,
- correlation ID propagated,
- committed incident is not lost when SQS fails.

---

# 41. Implementation Sequence for Kiro

## Slice 1 — Persistence

1. enums,
2. entities,
3. DbContext,
4. EF configurations,
5. migration,
6. seed data.

## Slice 2 — Asset reads

7. asset queries,
8. asset DTOs,
9. endpoints,
10. tests.

## Slice 3 — Incident workflow

11. incident queries,
12. CreateIncident command,
13. UpdateIncidentStatus command,
14. validators,
15. endpoints,
16. lifecycle tests.

## Slice 4 — Dashboard

17. dashboard query,
18. dashboard contract,
19. endpoint,
20. tests.

## Slice 5 — Frontend connection

21. dashboard UI,
22. assets UI,
23. incident UI.

## Slice 6 — Messaging

24. outbox migration/model,
25. event contract,
26. outbox writer,
27. publisher,
28. SQS configuration,
29. messaging tests.

The synchronous product should work before messaging is allowed to block progress.

---

# 42. Things Kiro Must Not Invent

Do not add:

- Floor entity,
- Department entity,
- SecurityZone entity,
- AccessZone entity,
- Alert entity,
- AuditEvent entity,
- DeviceTelemetry entity,
- asset subtype tables,
- WorkOrder/Technician entities in this service,
- Credential/Person entities in this service,
- Dashboard microservice,
- User microservice,
- shared all-services Domain project,
- cross-schema EF relationships,
- direct SQL against other service schemas,
- caching without measurement,
- Kafka/EventBridge/Lambda,
- event sourcing,
- generalized workflow engine,
- GraphQL,
- generic repository framework.

---

# 43. ChatGPT Review Checklist After Kiro Implements

Review:

### Domain

- correct five owned entities,
- no ownership leakage,
- transitions centralized/tested,
- asset/location rule enforced.

### EF Core

- correct schema,
- mappings clean,
- indexes align with queries,
- no cross-service DbSets,
- no accidental cascading deletes,
- no N+1.

### API

- `/api/v1`,
- thin controllers,
- correct status codes,
- ProblemDetails,
- pagination limits,
- explicit DTOs.

### Async

- cancellation propagated,
- no `.Result`,
- no `.Wait()`,
- no fake async.

### Security

- authorization server-side,
- validation,
- safe logs,
- safe CORS/config.

### Messaging

- versioned contract,
- no domain-entity serialization,
- reliable publication,
- correlation ID,
- duplicate-delivery assumptions.

### Performance

- read projections,
- `AsNoTracking`,
- aggregate dashboard queries,
- no premature cache.

### Portfolio quality

- readable code,
- useful Swagger,
- believable seed,
- obvious demo path,
- no overengineering.

---

# 44. Definition of Done

`SecurityOperationsService` is MVP-ready when:

```text
✓ builds and runs
✓ owns only its domain
✓ creates security_operations schema
✓ seeds Northstar Medical Center
✓ dashboard returns calculated security health
✓ asset inventory is searchable/filterable
✓ asset detail shows incident context
✓ incident create/update lifecycle works
✓ SecurityManager authorization is correct
✓ important behavior is tested
✓ cancellation tokens propagate
✓ logging is structured
✓ OpenAPI is useful
✓ primary critical-camera story works
✓ outbox/SQS works when messaging phase is connected
✓ no unnecessary domain/infrastructure expansion
```

---

# 45. Final Service Boundary Diagram

```text
                           Next.js Frontend
                                  |
                             REST /api/v1
                                  |
                                  v
                  +-------------------------------+
                  | SecurityOperationsService     |
                  |                               |
                  | Dashboard Queries             |
                  | Asset Queries                 |
                  | Incident Commands/Queries     |
                  +---------------+---------------+
                                  |
                                  v
                    security_operations schema
                                  |
             +---------+----------+----------+---------+
             |         |          |          |         |
             v         v          v          v         v
          Hospital  Building   Location   Security  Security
                                          Asset     Incident

                                  |
                     Critical incident + asset
                                  |
                                  v
                          Outbox Message
                                  |
                                  v
                             Amazon SQS
                                  |
                                  v
                         WorkOrderService
```

Cross-service dashboard composition:

```text
Next.js Dashboard
   |
   +--> SecurityOperationsService
   |      security health / critical incidents
   |
   +--> WorkOrderService
   |      open work-order count
   |
   +--> CredentialService
          expiring-credential count
```

This is the implementation contract Kiro should use for the Vision one-week MVP `SecurityOperationsService`.
