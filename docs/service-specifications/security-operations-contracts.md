# Vision — Security Operations Contracts Reference

**Status:** Phase 2 implementation reference  
**Service:** `SecurityOperationsService`  
**Target repository location:** `docs/service-specifications/security-operations-contracts.md`  
**Primary detailed specification:** `docs/service-specifications/security-operations-service.md`  
**Domain authority:** `docs/business-domain-specification.md`

---

# 1. Purpose

This document is the compact implementation-reference companion to the detailed `SecurityOperationsService` specification.

Kiro should use it as the fast lookup for enums, persistence shape, API routes, query parameters, request/response DTOs, dashboard contracts, incident transition rules, validation, authorization, seed landmarks, and Phase 2 acceptance checks.

If this reference conflicts with the Business & Domain Specification, the Business & Domain Specification wins. If it omits implementation guidance, use the detailed service specification.

---

# 2. Service Ownership

`SecurityOperationsService` owns exactly:

```text
Hospital
Building
Location
SecurityAsset
SecurityIncident
```

It does not own:

```text
WorkOrder
Technician
Person
Credential
Authentication identity
```

Cross-service references are IDs/snapshots only. No cross-schema EF navigations or direct mutation of another service's tables.

---

# 3. PostgreSQL Contract

Schema:

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

Later infrastructure table:

```text
security_operations.outbox_messages
```

Standards:

```text
IDs        Guid / uuid
Timestamps DateTimeOffset / timestamptz / UTC
Enums      persisted as readable strings
```

---

# 4. Entity Contract — Hospital

```text
Id          Guid            required
Name        string          required, max 200
Code        string?         optional, max 50
CreatedAt   DateTimeOffset  required
```

```text
Hospital 1 -> many Building
UNIQUE business name not required globally
Delete behavior: Restrict
```

---

# 5. Entity Contract — Building

```text
Id          Guid            required
HospitalId  Guid            required
Name        string          required, max 150
CreatedAt   DateTimeOffset  required
```

```text
Building many -> 1 Hospital
Building 1 -> many Location
UNIQUE (hospital_id, name)
Delete behavior: Restrict
```

---

# 6. Entity Contract — Location

```text
Id           Guid            required
BuildingId   Guid            required
Name         string          required, max 150
Floor        string?         optional, max 20
Department   string?         optional, max 100
Description  string?         optional, max 500
CreatedAt    DateTimeOffset  required
```

```text
UNIQUE (building_id, name)
```

Do not create separate `Floor`, `Department`, or `AccessZone` entities.

---

# 7. Entity Contract — SecurityAsset

```text
Id               Guid                 required
LocationId       Guid                 required
Name             string               required, max 150
AssetTag         string?              optional, max 50
AssetType        SecurityAssetType    required
Status           SecurityAssetStatus  required
Manufacturer     string?              optional, max 100
Model            string?              optional, max 100
Description      string?              optional, max 500
LastServiceAt    DateTimeOffset?      optional
StatusChangedAt  DateTimeOffset?      optional
CreatedAt        DateTimeOffset       required
UpdatedAt        DateTimeOffset       required
```

Important rule:

```text
StatusChangedAt may be null when historical timing is unknown.

Whenever Vision changes Status:
StatusChangedAt = now UTC
UpdatedAt       = now UTC
```

Recommended:

```text
UNIQUE asset_tag WHERE asset_tag IS NOT NULL
```

---

# 8. Enums

`SecurityAssetType`:

```text
Camera
AccessControlledDoor
BadgeReader
SecurityGate
```

`SecurityAssetStatus`:

```text
Operational
Degraded
Offline
```

`IncidentSeverity`:

```text
Low
Medium
High
Critical
```

`IncidentStatus`:

```text
Open
Investigating
Resolved
```

---

# 9. Entity Contract — SecurityIncident

```text
Id                 Guid              required
LocationId         Guid              required
SecurityAssetId    Guid?             optional
WorkOrderId        Guid?             optional external reference
Title              string            required, max 150
Description        string            required, max 2000
Severity           IncidentSeverity  required
Status             IncidentStatus    required
ResolutionSummary  string?           optional until resolution, max 2000
ResolvedAt         DateTimeOffset?   optional
CreatedAt          DateTimeOffset    required
UpdatedAt          DateTimeOffset    required
```

Rules:

```text
New -> Open
SecurityAssetId optional
If supplied, asset must exist and belong to LocationId
ResolvedAt null while active
Resolution requires nonblank ResolutionSummary
Resolved is terminal
At most one WorkOrder in MVP
WorkOrderId is external only; no cross-schema FK
```

---

# 10. Incident Transition Matrix

Valid:

```text
Open -> Investigating
Open -> Resolved
Investigating -> Resolved
```

Invalid:

```text
Investigating -> Open
Resolved -> Open
Resolved -> Investigating
```

Same-state requests should be idempotent.

Repeated `Resolved` must preserve original `ResolvedAt`.

---

# 11. Required Indexes

```text
buildings:
  (hospital_id)
  UNIQUE (hospital_id, name)

locations:
  (building_id)
  UNIQUE (building_id, name)

security_assets:
  (location_id)
  (status)
  (asset_type)
  (status, asset_type)

security_incidents:
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

# 12. API Base Contract

```text
/api/v1
```

List envelope:

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

---

# 13. Endpoint Summary

```text
GET   /api/v1/assets
GET   /api/v1/assets/{id}

GET   /api/v1/incidents
GET   /api/v1/incidents/{id}
POST  /api/v1/incidents
PATCH /api/v1/incidents/{id}

GET   /api/v1/dashboard
```

Potential later narrow endpoint only if required by repair demo:

```text
PATCH /api/v1/assets/{id}/status
```

Do not implement general asset CRUD.

---

# 14. GET /api/v1/assets

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

Default sort:

```text
Name ASC
```

Search fields:

```text
asset name
asset tag
location name
building name
```

Use PostgreSQL `ILIKE`.

Asset list item:

```text
Id
Name
AssetTag
AssetType
Status
Building { Id, Name }
Location { Id, Name, Floor, Department }
LastServiceAt
StatusChangedAt
```

Both timestamps may be null where unknown.

---

# 15. GET /api/v1/assets/{id}

Return:

```text
Id
Name
AssetTag
AssetType
Status
Manufacturer
Model
Description
Building { Id, Name }
Location { Id, Name, Floor, Department }
LastServiceAt
StatusChangedAt
RecentIncidents[]
```

Recent incident:

```text
Id
Title
Severity
Status
CreatedAt
WorkOrderId
```

Limit recent incidents to 5–10.

Unknown asset -> `404`.

---

# 16. GET /api/v1/incidents

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

Default:

```text
CreatedAt DESC
```

Search fields:

```text
title
description
asset name
location name
```

List item:

```text
Id
Title
Severity
Status
Asset? { Id, Name, AssetType }
Location { Id, Name }
CreatedAt
ResolvedAt
WorkOrderId
```

---

# 17. GET /api/v1/incidents/{id}

Return:

```text
Id
Title
Description
Severity
Status
Building { Id, Name }
Location { Id, Name, Floor, Department }
Asset? { Id, Name, AssetTag, AssetType, Status }
WorkOrderId
ResolutionSummary
ResolvedAt
CreatedAt
UpdatedAt
```

Unknown incident -> `404`.

---

# 18. POST /api/v1/incidents

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

Validation:

```text
location required and exists
asset optional
if asset supplied, it exists and belongs to location
severity valid
title 1..150
description 1..2000
```

Created state:

```text
Id                server-generated UUID
Status            Open
CreatedAt         now UTC
UpdatedAt         now UTC
ResolvedAt        null
ResolutionSummary null
WorkOrderId       null
```

Success:

```text
201 Created
Location: /api/v1/incidents/{id}
```

---

# 19. PATCH /api/v1/incidents/{id}

Authorized:

```text
SecurityManager
```

Request:

```json
{
  "status": "Investigating",
  "resolutionSummary": null
}
```

Resolve:

```json
{
  "status": "Resolved",
  "resolutionSummary": "Camera restored after power and network connection were reset."
}
```

Behavior:

```text
Open -> Investigating
  UpdatedAt = now

Open/Investigating -> Resolved
  ResolutionSummary required
  ResolvedAt = now
  UpdatedAt = now

Backward transition
  409 Conflict
```

Same-state is idempotent.

---

# 20. GET /api/v1/dashboard

This endpoint returns only SecurityOperations-owned data.

It must not query:

```text
work_orders.*
credentials.*
```

Response shape:

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
  "criticalAlerts": [],
  "recentActivity": []
}
```

Metric rules:

```text
OperationalPercentage = OperationalAssets / TotalAssets * 100
round whole
zero assets -> 0

ActiveCritical =
  Severity == Critical
  AND Status != Resolved

ActiveTotal =
  Status != Resolved

CriticalAlerts =
  active Critical incidents
  CreatedAt DESC
  limit 5
```

Do not add `AuditEvent` solely for recent activity.

---

# 21. Full Dashboard Composition

Next.js composes:

```text
SecurityOperationsService
  security health
  incident summary
  critical alerts
  recent security activity

WorkOrderService
  open work-order count

CredentialService
  expiring-credential count
```

No DashboardService in MVP.

No cross-schema SQL joins.

---

# 22. Authorization Contract

Primary demo role:

```text
SecurityManager
```

SecurityManager must be able to complete the entire MVP demo, including Credential Administrator activities in CredentialService.

Separate role remains:

```text
CredentialAdministrator
```

SecurityOperations matrix:

```text
Capability                      SecurityManager   Technician   CredentialAdministrator
GET dashboard                   Yes               Optional     Optional
GET assets                      Yes               Yes*         No
GET asset detail                Yes               Yes*         No
GET incidents                   Yes               No           No
GET incident detail             Yes               No           No
POST incident                   Yes               No           No
PATCH incident                  Yes               No           No
PATCH asset status if added     Yes               No           No
```

`*` Enable Technician reads only if useful to assigned-work context.

---

# 23. IncidentCreated Qualification

Automatic work-order creation:

```text
Severity == Critical
AND SecurityAssetId != null
```

Therefore:

```text
Critical + asset    -> qualifying event
Critical + no asset -> no automatic work order
High + asset        -> no automatic work order
Medium/Low          -> no automatic work order
```

Other incidents may get manual work orders later.

---

# 24. IncidentCreated Contract

Version:

```text
vision.security-operations.incident-created.v1
```

Semantics:

```text
EventId
EventType
OccurredAt
CorrelationId

Incident
  Id
  Title
  Description
  Severity

Asset
  Id
  Name
  AssetTag
  AssetType

Location
  Id
  Name
  BuildingId
  BuildingName
```

Do not serialize EF/domain entities directly.

---

# 25. Transactional Outbox Contract

When SQS is added:

```text
same DB transaction:
  insert security incident
  insert outbox message
```

Outbox table:

```text
security_operations.outbox_messages
```

Minimum fields:

```text
id
event_type
payload
occurred_at
published_at
attempt_count
last_error
correlation_id
```

Behavior:

```text
publish success -> set published_at
publish failure -> keep unpublished, increment attempt, log/record error
```

Do not block the synchronous Security Operations slice on AWS setup.

---

# 26. Seed Contract

Hospital:

```text
Northstar Medical Center
```

Buildings:

```text
Main Hospital
Administrative Building
Data Center
```

Locations:

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

Assets:

```text
target ~55
acceptable range 40–60
3–5 combined Degraded/Offline
most Operational
```

Suggested distribution:

```text
22 Camera
14 AccessControlledDoor
13 BadgeReader
6 SecurityGate
```

---

# 27. Required Demo Asset

```text
Name       Pharmacy Storage Camera 02
AssetTag   CAM-PHARM-002
Type       Camera
Building   Main Hospital
Location   Pharmacy Storage
Status     Offline
```

---

# 28. Required Demo Incident

```text
Title        Pharmacy storage camera offline
Description  Camera stopped responding and is not producing video.
Severity     Critical
Status       Open
Asset        Pharmacy Storage Camera 02
Location     Pharmacy Storage
```

Additional variety:

```text
High / Investigating
Medium / Open
Low / Resolved
```

Seed must be idempotent. Stable deterministic IDs recommended for landmarks.

---

# 29. Validation Contract

At minimum:

```text
page >= 1
1 <= pageSize <= 100

valid asset status/type
valid incident severity/status

title required <= 150
description required <= 2000

resolution summary required when resolving

location exists
asset exists if supplied
asset.LocationId == incident.LocationId
```

---

# 30. HTTP Errors

```text
400 invalid request/enum/pagination
401 unauthenticated
403 unauthorized role
404 resource absent
409 invalid state transition
500 unexpected server failure
```

Use Problem Details where practical.

---

# 31. Query Implementation Rules

Read queries:

```text
AsNoTracking()
```

List flow:

```text
filter
search
count
order
paginate
project
```

Avoid N+1, client-side filtering, giant loaded graphs, and generic dynamic sorting.

Dashboard counts should be database aggregates.

---

# 32. Async Rules

```text
HTTP CancellationToken
  -> MediatR
  -> Handler
  -> EF Core / SaveChangesAsync
```

Do not use:

```text
.Result
.Wait()
Task.Run() around EF/HTTP I/O
```

---

# 33. Required Unit Tests

Incident lifecycle:

```text
new -> Open
Open -> Investigating
Open -> Resolved
Investigating -> Resolved
resolve without summary -> rejected
Resolved -> Investigating -> rejected
Resolved -> Open -> rejected
Resolved -> Resolved -> idempotent, timestamp preserved
```

Asset/location:

```text
matching -> valid
mismatch -> rejected
location-only -> valid
```

Event qualification:

```text
Critical + asset -> qualifies
Critical + no asset -> does not qualify
High + asset -> does not qualify
Medium/Low -> does not qualify
```

Dashboard:

```text
all operational
mixed statuses
zero assets
```

---

# 34. Required Integration Tests

```text
GET assets returns persisted assets
GET assets filters status
GET assets filters type
asset search finds pharmacy camera
asset detail returns location/recent incidents
unknown asset -> 404

POST incident persists
mismatched asset/location rejected
GET incidents returns created incident
PATCH Open -> Investigating persists
PATCH -> Resolved persists summary/timestamp
backward transition -> 409

dashboard derives counts from persistence
authorization verified once Cognito enabled
```

Messaging:

```text
Critical + asset -> one outbox record
High + asset -> none
failed publication remains unpublished
successful publication sets published_at
```

Seed regression must protect:

```text
Northstar Medical Center
Main Hospital
Pharmacy Storage
Pharmacy Storage Camera 02
Critical pharmacy incident
```

---

# 35. Phase 2 Kiro Checklist

Kiro should implement:

```text
[ ] five SecurityOperations domain entities
[ ] SecurityOperationsDbContext
[ ] EF entity mappings/configurations
[ ] security_operations PostgreSQL schema
[ ] initial migration
[ ] indexes/constraints
[ ] idempotent Northstar seed infrastructure
[ ] local PostgreSQL configuration
```

Persistence acceptance:

```text
[ ] no WorkOrder/Technician/Person/Credential DbSets
[ ] UUID IDs
[ ] DateTimeOffset UTC timestamps
[ ] StatusChangedAt nullable
[ ] readable constrained enum storage
[ ] FKs only inside owned schema
[ ] no destructive cascade design
[ ] migration applies to empty DB
[ ] seed reruns without duplication
[ ] required Northstar landmarks exist
[ ] local PostgreSQL starts/connects
```

---

# 36. Do Not Invent

Do not add:

```text
Floor entity
Department entity
AccessZone entity
Alert entity
AuditEvent entity
DeviceTelemetry entity
asset subtype entities
cross-service business DbSets
cross-schema EF relationships
shared all-services domain aggregate model
generic repository framework
second database
cache
Kafka
EventBridge
Lambda
Dashboard microservice
```

---

# 37. Contract Precedence

```text
1. Approved Business & Domain Specification
2. Approved detailed SecurityOperationsService specification
3. This quick-reference contract
4. Technology specification
5. README
```

---

# 38. Phase 2 ChatGPT Completion

Covered by the detailed service spec plus this reference:

```text
✓ detailed SecurityOperationsService specification
✓ dashboard response models
✓ asset contracts
✓ incident rules
✓ realistic SecurityOperations seed data
✓ initial API contracts
```

Next ChatGPT work belongs to Phase 3:

```text
review Kiro backend architecture
review frontend/API consistency
review implemented API contracts
define WorkOrderService
define SQS integration contract in WorkOrder context
define work-order acceptance tests
```
