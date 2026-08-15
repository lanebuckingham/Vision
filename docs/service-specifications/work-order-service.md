# Vision — WorkOrderService Detailed Specification

**Status:** MVP implementation specification  
**Service:** `WorkOrderService`  
**Target implementation agent:** Amazon Kiro  
**Target repository location:** `docs/service-specifications/work-order-service.md`  
**Depends on:** `README.md`, `docs/technology-specification.md`, `docs/business-domain-specification.md`, `docs/service-specifications/security-operations-service-specification-revised.md`  
**Scope:** Vision one-week MVP

---

# 1. Purpose

This document defines the detailed implementation contract for Vision's `WorkOrderService`.

The service owns the maintenance workflow that follows physical-security equipment failures.

It supports the primary Vision repair story:

```text
Security incident
      ↓
Work order created
      ↓
Technician assigned
      ↓
Repair started
      ↓
Repair note added
      ↓
Repair completed
```

The service must support both:

1. manually created maintenance work orders, and
2. work orders created asynchronously from qualifying Security Operations incidents.

The implementation should remain intentionally small and production-shaped.

Kiro should not invent additional maintenance entities, scheduling concepts, workflow states, infrastructure patterns, or cross-service persistence relationships unless an approved specification explicitly requires them.

---

# 2. Service Mission

`WorkOrderService` answers:

> **What maintenance work is required for failed physical-security equipment, who is responsible for it, what state is the repair in, and has the repair been completed?**

It is authoritative for:

- work orders,
- work-order priority,
- work-order lifecycle,
- technician assignment,
- repair timestamps,
- technician notes,
- completion information,
- technician assignment eligibility,
- consumption of the Security Operations incident event,
- idempotent automatic work-order creation.

It is not authoritative for:

- hospitals,
- buildings,
- locations,
- security-asset operational state,
- security incidents,
- people/employee credential records,
- credentials,
- authentication identities.

---

# 3. Source-of-Truth Precedence

If specifications differ, use this order:

```text
1. Business & Domain Specification
2. This WorkOrderService specification
3. SecurityOperationsService specification where cross-service contract applies
4. Technology Specification
5. README
```

This specification adds implementation detail but must not silently change the approved business/domain model.

---

# 4. Service Boundary

`WorkOrderService` owns:

```text
WorkOrder
Technician
TechnicianNote as an owned/value record
```

Primary aggregate:

```text
WorkOrder
```

`Technician` is an independently persisted service-owned reference entity used for assignment.

`TechnicianNote` is not a tenth top-level Vision business entity.

It belongs to the WorkOrder aggregate.

---

# 5. Cross-Service References

A WorkOrder may store:

```text
SecurityAssetId
SecurityIncidentId
AssetNameSnapshot
LocationNameSnapshot
CorrelationId
SourceEventId
```

`SecurityAssetId` and `SecurityIncidentId` are external references.

They do not create:

- EF navigation properties into Security Operations,
- PostgreSQL foreign keys across service schemas,
- direct SQL joins into `security_operations`,
- shared domain aggregates.

WorkOrderService must never directly modify:

```text
security_operations.*
credentials.*
```

---

# 6. PostgreSQL Ownership

Use schema:

```text
work_orders
```

Business tables:

```text
work_orders.work_orders
work_orders.technicians
work_orders.technician_notes
```

`technician_notes` exists for persistence but remains aggregate-owned rather than a standalone Vision business capability.

Do not create a public `DbSet<TechnicianNote>`.

Current repository behavior using:

```text
DbSet<WorkOrder>
DbSet<Technician>
```

should remain.

---

# 7. Identifier and Time Standards

Use:

```text
Guid
```

for identifiers.

PostgreSQL:

```text
uuid
```

Use:

```text
DateTimeOffset
```

for timestamps.

PostgreSQL:

```text
timestamp with time zone
```

All application-generated timestamps must be UTC.

All API timestamps serialize as ISO 8601.

---

# 8. Enum Persistence

Store WorkOrder enums as readable strings.

## WorkOrderStatus

Exactly:

```text
New
Assigned
InProgress
Completed
```

## WorkOrderPriority

Exactly:

```text
Low
Medium
High
Critical
```

Do not add:

```text
Pending
Scheduled
Cancelled
OnHold
Blocked
Closed
Reopened
Escalated
```

to the MVP.

---

# 9. Domain Entity — WorkOrder

Properties:

| Property | CLR Type | Required |
|---|---|---:|
| `Id` | `Guid` | Yes |
| `SecurityAssetId` | `Guid` | Yes |
| `SecurityIncidentId` | `Guid?` | No |
| `Title` | `string` | Yes |
| `Description` | `string` | Yes |
| `Priority` | `WorkOrderPriority` | Yes |
| `Status` | `WorkOrderStatus` | Yes |
| `AssignedTechnicianId` | `Guid?` | No |
| `AssignedAt` | `DateTimeOffset?` | No |
| `StartedAt` | `DateTimeOffset?` | No |
| `CompletedAt` | `DateTimeOffset?` | No |
| `CompletionSummary` | `string?` | No |
| `AssetNameSnapshot` | `string?` | No |
| `LocationNameSnapshot` | `string?` | No |
| `CorrelationId` | `string?` | No |
| `SourceEventId` | `Guid?` | No |
| `CreatedAt` | `DateTimeOffset` | Yes |
| `UpdatedAt` | `DateTimeOffset` | Yes |
| `Notes` | owned collection | Yes, may be empty |

Recommended lengths:

```text
Title                 150
Description          2000
CompletionSummary    2000
AssetNameSnapshot     150
LocationNameSnapshot  150
CorrelationId         100
```

---

# 10. WorkOrder Invariants

A WorkOrder must always reference one SecurityAsset:

```text
SecurityAssetId != Guid.Empty
```

Title:

```text
required
nonblank
max 150
```

Description:

```text
required
nonblank
max 2000
```

Priority must be one of the defined `WorkOrderPriority` values.

Status must be one of the defined `WorkOrderStatus` values.

New work orders always begin:

```text
Status = New
```

Clients must not specify the initial status.

---

# 11. WorkOrder Lifecycle

The lifecycle is exactly:

```text
New
 |
 v
Assigned
 |
 v
InProgress
 |
 v
Completed
```

No skipping states is required.

Valid transitions:

```text
New -> Assigned
Assigned -> InProgress
InProgress -> Completed
```

Invalid examples:

```text
New -> InProgress
New -> Completed

Assigned -> New
Assigned -> Completed

InProgress -> New
InProgress -> Assigned

Completed -> anything
```

Completed is terminal.

The MVP does not support:

- reopening,
- cancellation,
- reassignment after work has started,
- pausing,
- scheduling,
- escalation.

---

# 12. Assignment Behavior

Assignment requires:

```text
Status == New
```

and:

```text
Technician exists
Technician.IsActive == true
```

Successful assignment sets:

```text
AssignedTechnicianId
AssignedAt = now UTC
Status = Assigned
UpdatedAt = now UTC
```

The WorkOrder entity should own this state transition.

Recommended domain operation:

```text
AssignTechnician(...)
```

Do not implement assignment by directly setting entity properties in the controller.

---

# 13. Starting Work

Starting work requires:

```text
Status == Assigned
AssignedTechnicianId != null
```

Successful start sets:

```text
Status = InProgress
StartedAt = now UTC
UpdatedAt = now UTC
```

`StartedAt` represents the first transition into active repair.

Recommended domain operation:

```text
StartWork(...)
```

---

# 14. Completing Work

Completion requires:

```text
Status == InProgress
AssignedTechnicianId != null
```

Completion also requires repair-completion information.

The approved domain permits either:

```text
nonblank CompletionSummary
```

or:

```text
at least one meaningful TechnicianNote
```

Therefore the current Phase 2 implementation that requires a nonblank completion summary in every case should be adjusted during WorkOrder implementation.

Recommended behavior:

```text
if completionSummary is blank
AND no technician notes exist
    reject completion
```

Successful completion sets:

```text
Status = Completed
CompletedAt = now UTC
UpdatedAt = now UTC
```

If a completion summary is supplied:

```text
CompletionSummary = supplied value
```

Do not erase or replace technician notes when completing.

---

# 15. Technician Entity

Properties:

| Property | CLR Type | Required |
|---|---|---:|
| `Id` | `Guid` | Yes |
| `DisplayName` | `string` | Yes |
| `Email` | `string` | Yes |
| `IsActive` | `bool` | Yes |
| `CognitoSubject` | `string?` | No |
| `Specialty` | `string?` | No |
| `CreatedAt` | `DateTimeOffset` | Yes |

Recommended lengths:

```text
DisplayName       150
Email             254
CognitoSubject    128
Specialty         100
```

Rules:

- display name required,
- email required,
- email syntactically valid,
- email unique,
- only active technicians may receive new assignments.

`Specialty` is display/seed realism.

It does not create:

- skill matching,
- scheduling,
- dispatch optimization,
- technician certification rules.

---

# 16. Technician vs Person

Do not merge:

```text
Technician
```

with:

```text
CredentialService.Person
```

Even if the same real employee conceptually exists in both contexts.

For the MVP:

```text
Technician
    = repair-assignment identity

Person
    = credential-management identity
```

Do not create:

```text
Employee
User
StaffMember
SharedPerson
```

to unify them.

---

# 17. TechnicianNote

Persist technician notes as owned records inside the WorkOrder aggregate.

Properties:

| Property | CLR Type | Required |
|---|---|---:|
| `Id` | `Guid` | Yes |
| `WorkOrderId` | `Guid` | Yes |
| `TechnicianId` | `Guid` | Yes |
| `Content` | `string` | Yes |
| `CreatedAt` | `DateTimeOffset` | Yes |

Content:

```text
required
nonblank
max 2000
```

A note must record the technician who created it.

Do not create standalone technician-note administration APIs.

Notes are addressed through their WorkOrder.

---

# 18. TechnicianNote Lifecycle Rules

For the MVP, notes may be added while a work order is:

```text
Assigned
InProgress
```

They may not be added while:

```text
New
Completed
```

The authenticated Technician adding the note must be the assigned technician once Cognito authorization is enabled.

The primary demo should normally add the repair note after entering `InProgress`.

Notes should be returned chronologically:

```text
CreatedAt ASC
```

---

# 19. EF Core Relationships

Relationship:

```text
Technician 1 -------- * WorkOrder
```

A WorkOrder has:

```text
0..1 AssignedTechnician
```

Use:

```text
AssignedTechnicianId
```

as the FK within the WorkOrder schema.

Technician notes:

```text
WorkOrder 1 -------- * TechnicianNote
```

should remain an owned collection.

No EF relationships may cross into SecurityOperationsService.

---

# 20. Delete Behavior

MVP does not expose deletion APIs for:

```text
WorkOrder
Technician
TechnicianNote
```

Technicians should normally be made inactive rather than deleted.

Do not build delete workflows.

The existing assignment FK behavior may preserve historical work orders if a technician row were removed, but service behavior should not depend on technician deletion.

---

# 21. Required Indexes

At minimum:

```text
work_orders:
    (status)
    (priority)
    (assigned_technician_id)
    (security_asset_id)
    (created_at DESC)
```

Required uniqueness:

```text
UNIQUE security_incident_id
WHERE security_incident_id IS NOT NULL
```

and:

```text
UNIQUE source_event_id
WHERE source_event_id IS NOT NULL
```

These constraints are important for the asynchronous idempotency strategy.

Recommended additional combined indexes only if query patterns justify them:

```text
(status, assigned_technician_id)
(status, created_at)
```

Do not add indexes speculatively in large numbers.

---

# 22. SecurityIncident Cardinality

MVP rule:

```text
SecurityIncident 1 -> 0..1 WorkOrder
```

Therefore two WorkOrders may not reference the same non-null `SecurityIncidentId`.

This applies regardless of whether the WorkOrder was created:

- manually, or
- asynchronously from SQS.

The database uniqueness constraint provides concurrency protection in addition to application-level checks.

---

# 23. API Standards

Base route:

```text
/api/v1
```

Use:

```text
application/json
application/problem+json
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

Do not build generic dynamic sorting infrastructure.

---

# 24. Required API Endpoints

Implement:

```text
GET  /api/v1/work-orders
GET  /api/v1/work-orders/{id}
POST /api/v1/work-orders

POST /api/v1/work-orders/{id}/assignment
POST /api/v1/work-orders/{id}/start
POST /api/v1/work-orders/{id}/notes
POST /api/v1/work-orders/{id}/complete

GET  /api/v1/work-orders/summary

GET  /api/v1/technicians
GET  /api/v1/technicians/{id}
```

These explicit action endpoints are intentional.

They communicate the domain operations more clearly than exposing arbitrary WorkOrder PATCH semantics.

Do not implement:

```text
PUT /work-orders/{id}
DELETE /work-orders/{id}
PATCH arbitrary WorkOrder fields
```

---

# 25. GET /api/v1/work-orders

Query parameters:

```text
status
priority
technicianId
assetId
incidentId
search
page
pageSize
```

Recommended default sort:

```text
CreatedAt DESC
```

Search case-insensitively across:

```text
title
description
asset name snapshot
location name snapshot
assigned technician display name
```

Use PostgreSQL `ILIKE` where appropriate.

---

# 26. WorkOrder List DTO

Return:

```text
Id
Title
Priority
Status
SecurityAssetId
SecurityIncidentId
AssetName
LocationName

AssignedTechnician?
    Id
    DisplayName
    Specialty

AssignedAt
StartedAt
CompletedAt
CreatedAt
UpdatedAt
```

Do not include all technician notes in list responses.

Do not include long completion/description fields unless the UI actually needs them.

---

# 27. Incident Lookup

The following query must be supported:

```text
GET /api/v1/work-orders?incidentId={securityIncidentId}
```

This is important to the asynchronous workflow.

After SecurityOperationsService publishes an `IncidentCreated` event and WorkOrderService consumes it, the frontend can discover the resulting work order using the source incident ID.

This avoids requiring WorkOrderService to write directly into SecurityOperations persistence merely to establish frontend navigation.

Because one incident can produce at most one WorkOrder, this filtered list will contain at most one item under valid data.

---

# 28. Asset Lookup

Support:

```text
GET /api/v1/work-orders?assetId={securityAssetId}
```

This enables asset detail screens to show service/repair history without cross-schema joins.

WorkOrderService performs the lookup using its own external `SecurityAssetId` field.

---

# 29. Technician Work Lookup

Support:

```text
GET /api/v1/work-orders?technicianId={technicianId}
```

This provides the basic assigned-work view.

Once Cognito is enabled, Technician-role authorization should constrain a technician to their own work rather than trusting arbitrary client-supplied technician IDs.

Do not make the query parameter itself an authorization mechanism.

---

# 30. GET /api/v1/work-orders/{id}

Return full detail:

```text
Id
SecurityAssetId
SecurityIncidentId

Title
Description
Priority
Status

AssetName
LocationName

AssignedTechnician?
    Id
    DisplayName
    Email
    Specialty
    IsActive

AssignedAt
StartedAt
CompletedAt
CompletionSummary

CreatedAt
UpdatedAt

Notes[]
    Id
    TechnicianId
    TechnicianDisplayName
    Content
    CreatedAt
```

`SourceEventId` and `CorrelationId` are primarily operational/infrastructure metadata.

They do not need to be prominent in normal UI DTOs.

They may be included in an internal/admin-oriented representation if useful, but do not clutter the employer-facing work-order UI with infrastructure details.

Unknown work order:

```text
404
```

---

# 31. POST /api/v1/work-orders

Purpose:

```text
manual work-order creation
```

Authorized role once Cognito is enabled:

```text
SecurityManager
```

Request:

```json
{
  "securityAssetId": "uuid",
  "securityIncidentId": "uuid-or-null",
  "title": "Repair pharmacy storage camera",
  "description": "Investigate and restore the offline camera.",
  "priority": "Critical",
  "assetName": "Pharmacy Storage Camera 02",
  "locationName": "Pharmacy Storage"
}
```

`assetName` and `locationName` are display snapshots.

They are not authoritative replacements for SecurityOperations data.

---

# 32. Manual WorkOrder Creation Rules

Required:

```text
SecurityAssetId
Title
Description
Priority
```

Optional:

```text
SecurityIncidentId
AssetNameSnapshot
LocationNameSnapshot
```

Server controls:

```text
Id
Status
AssignedTechnicianId
AssignedAt
StartedAt
CompletedAt
CompletionSummary
CorrelationId
SourceEventId
CreatedAt
UpdatedAt
```

New manual WorkOrder:

```text
Status = New
CreatedAt = now UTC
UpdatedAt = now UTC
```

`SourceEventId` remains null.

---

# 33. External Reference Validation

WorkOrderService must not directly query Security Operations database tables to validate:

```text
SecurityAssetId
SecurityIncidentId
```

For the MVP, treat them as external identifiers supplied through an already-established Security Operations workflow or integration event.

If manual creation is initiated from a Security Operations asset/incident screen, the frontend already has those identifiers and display context.

Do not introduce cross-schema database reads merely for referential validation.

If stronger verification becomes necessary later, use a SecurityOperationsService API rather than its database.

---

# 34. Duplicate Manual Incident WorkOrder

If:

```text
SecurityIncidentId != null
```

and a WorkOrder already exists for that incident:

```text
409 Conflict
```

Return Problem Details explaining that the incident already has an associated WorkOrder.

The frontend should then be able to navigate to the existing work order.

Do not silently create another.

---

# 35. POST /api/v1/work-orders/{id}/assignment

Authorized:

```text
SecurityManager
```

Request:

```json
{
  "technicianId": "uuid"
}
```

Rules:

- work order must exist,
- status must be `New`,
- technician must exist,
- technician must be active.

Success:

```text
200 OK
```

Return the updated WorkOrder detail or a sufficiently useful updated representation.

Unknown work order:

```text
404
```

Unknown technician:

```text
404
```

Inactive technician:

```text
409
```

Wrong work-order lifecycle state:

```text
409
```

---

# 36. POST /api/v1/work-orders/{id}/start

Authorized:

```text
Technician
```

Rules:

```text
WorkOrder exists
Status == Assigned
AssignedTechnicianId exists
caller is assigned technician once authentication enabled
```

Successful transition:

```text
Assigned -> InProgress
```

sets:

```text
StartedAt
UpdatedAt
```

Return updated detail.

Invalid state:

```text
409
```

Unauthorized technician:

```text
403
```

once authentication is enabled.

---

# 37. POST /api/v1/work-orders/{id}/notes

Authorized:

```text
Technician
```

Request:

```json
{
  "content": "Replaced damaged PoE patch cable and verified stable camera feed."
}
```

Server controls:

```text
Note Id
WorkOrderId
TechnicianId
CreatedAt
```

Rules:

- work order exists,
- assigned technician exists,
- caller is assigned technician,
- work order is `Assigned` or `InProgress`,
- content required,
- content max 2000,
- completed work orders cannot receive new notes.

Success:

```text
201 Created
```

---

# 38. POST /api/v1/work-orders/{id}/complete

Authorized:

```text
Technician
```

Request:

```json
{
  "completionSummary": "Replaced failed network cable and verified camera feed is stable."
}
```

`completionSummary` may be null or blank only when at least one existing technician repair note provides the required repair-completion information.

Rules:

```text
WorkOrder exists
Status == InProgress
AssignedTechnicianId exists
caller is assigned technician
completion information exists
```

Success:

```text
InProgress -> Completed
```

sets:

```text
CompletedAt
UpdatedAt
CompletionSummary when supplied
```

Return updated detail.

Invalid lifecycle:

```text
409
```

Missing completion information:

```text
400
```

or validation-oriented Problem Details.

---

# 39. GET /api/v1/technicians

Purpose:

```text
Security Manager assignment UI
```

Query parameters:

```text
activeOnly
search
page
pageSize
```

Recommended:

```text
activeOnly default = true
```

Search:

```text
DisplayName
Email
Specialty
```

Default sort:

```text
DisplayName ASC
```

List item:

```text
Id
DisplayName
Email
Specialty
IsActive
```

Do not implement technician scheduling/calendar availability.

---

# 40. GET /api/v1/technicians/{id}

Return:

```text
Id
DisplayName
Email
Specialty
IsActive
CreatedAt
```

Do not return a giant WorkOrder collection as part of Technician detail.

Assigned work is queried through `/work-orders`.

Unknown technician:

```text
404
```

---

# 41. WorkOrder Dashboard Summary

Vision's full dashboard is composed in the frontend.

WorkOrderService provides its owned summary data through:

```text
GET /api/v1/work-orders/summary
```

Response:

```json
{
  "openCount": 4,
  "byStatus": {
    "new": 1,
    "assigned": 1,
    "inProgress": 2,
    "completed": 7
  }
}
```

Definition:

```text
Open =
Status != Completed
```

All counts derive from WorkOrderService persistence.

Do not query SecurityOperations or Credential schemas.

Do not create a Dashboard domain entity.

---

# 42. Application-Layer Structure

Recommended organization:

```text
Application/
├── WorkOrders/
│   ├── Commands/
│   │   ├── CreateWorkOrder
│   │   ├── AssignTechnician
│   │   ├── StartWork
│   │   ├── AddTechnicianNote
│   │   └── CompleteWorkOrder
│   └── Queries/
│       ├── GetWorkOrders
│       ├── GetWorkOrderById
│       └── GetWorkOrderSummary
│
├── Technicians/
│   └── Queries/
│       ├── GetTechnicians
│       └── GetTechnicianById
│
└── Common/
```

Equivalent repository-consistent organization is acceptable.

Do not create unnecessary assemblies solely to imitate textbook Clean Architecture.

---

# 43. MediatR

Recommended requests:

```text
GetWorkOrdersQuery
GetWorkOrderByIdQuery
GetWorkOrderSummaryQuery

CreateWorkOrderCommand
AssignTechnicianCommand
StartWorkCommand
AddTechnicianNoteCommand
CompleteWorkOrderCommand

GetTechniciansQuery
GetTechnicianByIdQuery
```

MediatR should organize application behavior.

Do not wrap MediatR in another custom command bus.

---

# 44. Domain/Application Responsibility

Domain entity owns:

```text
assignment transition
start transition
completion transition
terminal-state protection
```

Application handler owns:

```text
loading WorkOrder
loading Technician
checking service-level prerequisites
invoking domain behavior
saving transaction
mapping DTO
```

Validator owns:

```text
request shape
required fields
length limits
enum/request validation
```

Controller/endpoint owns:

```text
HTTP binding
HTTP response
authorization policy attachment
```

Controllers should remain thin.

---

# 45. Query Behavior

Read-only queries should use:

```text
AsNoTracking()
```

Use projections rather than materializing large entity graphs where practical.

Avoid N+1 behavior.

For WorkOrder detail, fetch required technician/note context efficiently.

For lists, do not fetch technician-note collections.

Filter in PostgreSQL rather than in memory.

---

# 46. CancellationToken

Propagate request cancellation:

```text
HTTP
 ↓
MediatR
 ↓
handler
 ↓
EF Core
```

Use:

```text
ToListAsync(cancellationToken)
CountAsync(cancellationToken)
FirstOrDefaultAsync(cancellationToken)
SaveChangesAsync(cancellationToken)
```

Do not use:

```text
.Result
.Wait()
GetAwaiter().GetResult()
```

---

# 47. Problem Details

Use Problem Details-compatible errors.

Expected:

```text
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
500 Internal Server Error
```

Typical mappings:

```text
invalid request                   -> 400
unknown WorkOrder                 -> 404
unknown Technician                -> 404
inactive Technician assignment    -> 409
invalid lifecycle transition      -> 409
incident already has WorkOrder    -> 409
wrong assigned Technician         -> 403
```

Do not expose stack traces or database internals.

---

# 48. Authorization Matrix

Once Cognito is implemented:

| Capability | SecurityManager | Technician | CredentialAdministrator |
|---|:---:|:---:|:---:|
| View all WorkOrders | Yes | No* | No |
| View own assigned WorkOrders | Yes | Yes | No |
| View WorkOrder detail | Yes | Assigned only | No |
| Manually create WorkOrder | Yes | No | No |
| View technicians for assignment | Yes | Limited/not required | No |
| Assign technician | Yes | No | No |
| Start work | No | Assigned only | No |
| Add technician note | No | Assigned only | No |
| Complete work | No | Assigned only | No |
| View WorkOrder summary | Yes | Optional | No |

`*` Technician must not gain unrestricted access to everybody's assigned maintenance work merely because the endpoint supports filtering.

Authorization must be enforced by backend APIs.

Frontend hiding alone is insufficient.

---

# 49. Pre-Cognito Implementation

Cognito arrives in the later authorization phase.

Until then:

- keep operations separated cleanly by endpoint,
- avoid fake production authentication,
- avoid temporary user tables,
- keep `CognitoSubject` available on Technician,
- structure commands so the caller identity can later be supplied cleanly.

Do not build authentication infrastructure twice.

---

# 50. SQS Consumer Responsibility

WorkOrderService is the consumer of:

```text
vision.security-operations.incident-created.v1
```

The detailed event/transport contract is maintained separately, but WorkOrderService must understand the approved event semantics.

The event contains sufficient:

```text
incident
asset
location
correlation
```

context to create a WorkOrder without querying Security Operations persistence.

---

# 51. Automatic WorkOrder Qualification

SecurityOperationsService is responsible for publishing only qualifying incident events.

The qualification rule is:

```text
Incident Severity == Critical
AND
SecurityAssetId != null
```

WorkOrderService should nevertheless reject or safely ignore structurally invalid messages.

Do not create automatic WorkOrders from:

```text
Critical incident without asset
High incident
Medium incident
Low incident
```

The business rule remains narrow for the MVP.

---

# 52. Event-Derived WorkOrder Mapping

For a valid `IncidentCreated.v1` event:

```text
WorkOrder.Id
    = new Guid

SecurityAssetId
    = event.Asset.Id

SecurityIncidentId
    = event.Incident.Id

Priority
    = mapped from incident severity

Status
    = New

AssetNameSnapshot
    = event.Asset.Name

LocationNameSnapshot
    = event.Location.Name

CorrelationId
    = event.CorrelationId

SourceEventId
    = event.EventId

CreatedAt
    = WorkOrder creation time UTC

UpdatedAt
    = same initial creation time
```

Recommended generated title:

```text
Repair: {Incident.Title}
```

Description may use the incident description as the initial repair context.

Do not blindly serialize/store the entire incoming event as the WorkOrder domain entity.

---

# 53. Priority Mapping

For automatic creation:

```text
Incident Low      -> WorkOrder Low
Incident Medium   -> WorkOrder Medium
Incident High     -> WorkOrder High
Incident Critical -> WorkOrder Critical
```

Because automatic creation is currently only triggered for Critical incidents, the primary asynchronous path produces:

```text
Priority = Critical
```

The complete mapping should still be centralized rather than embedded as scattered string comparisons.

---

# 54. SQS Idempotency

Amazon SQS may deliver the same message more than once.

Duplicate delivery is normal.

WorkOrderService must be idempotent.

Use both:

```text
SourceEventId
SecurityIncidentId
```

where practical.

Before creating a WorkOrder, check whether either already identifies processed business work.

Database uniqueness constraints remain the final concurrency safeguard.

---

# 55. Duplicate Event — Same EventId

If a WorkOrder already exists with:

```text
SourceEventId == incoming EventId
```

the event is already processed.

Behavior:

```text
do not create another WorkOrder
log duplicate/idempotent handling
acknowledge message successfully
```

Do not treat this as a poison message.

---

# 56. Duplicate Event — Same IncidentId

If a WorkOrder already exists with:

```text
SecurityIncidentId == incoming IncidentId
```

do not create another WorkOrder even if the incoming `EventId` differs.

This protects the business invariant:

```text
one incident -> at most one WorkOrder
```

A manually created WorkOrder for the incident also satisfies this rule.

The message may be acknowledged after logging that work already exists.

---

# 57. Consumer Transaction

Conceptual consumer flow:

```text
SQS message received
       ↓
deserialize explicit v1 event DTO
       ↓
validate contract/version
       ↓
check EventId / IncidentId
       ↓
DB transaction
       ↓
insert WorkOrder if not already present
       ↓
commit
       ↓
acknowledge/delete SQS message
```

Never delete the SQS message before the database operation is safely committed.

---

# 58. Concurrent Duplicate Protection

Application checks alone are insufficient because two duplicate deliveries can race.

Therefore preserve unique database constraints for:

```text
SourceEventId
SecurityIncidentId
```

If concurrent inserts produce a uniqueness violation:

1. determine whether the already-existing row represents the same incident/event business work;
2. treat legitimate duplicate delivery as successful idempotent processing;
3. do not create a second WorkOrder.

Do not expose raw database exceptions.

---

# 59. Correlation

Preserve:

```text
CorrelationId
```

from the incoming incident event.

Use it in structured logs.

When OpenTelemetry is added, propagate the corresponding trace/correlation context where practical.

Do not invent a second unrelated correlation identifier if one is supplied.

---

# 60. WorkOrder Creation and Incident.WorkOrderId

SecurityOperationsService owns:

```text
SecurityIncident.WorkOrderId
```

WorkOrderService must not write it directly through the database.

The MVP does not require WorkOrderService to perform a cross-schema update after asynchronous creation.

The frontend can discover associated work using:

```text
GET /api/v1/work-orders?incidentId={incidentId}
```

If the project later requires SecurityOperationsService to persist the returned WorkOrder ID, accomplish that through:

- a SecurityOperations API, or
- a later integration event,

not through direct database access.

Do not make that additional synchronization block the base SQS workflow.

---

# 61. WorkOrder Completion and Security Operations

Completing a WorkOrder does not grant WorkOrderService ownership of:

```text
SecurityAsset.Status
SecurityIncident.Status
```

Therefore WorkOrderService must not directly modify Security Operations tables.

The broader business outcome is:

```text
WorkOrder Completed
      ↓
Asset eventually Operational
      ↓
Incident resolved
      ↓
Dashboard improves
```

For the one-week MVP, use the simplest clean orchestration that preserves ownership.

A second `WorkOrderCompleted` integration event is optional, not mandatory.

Do not introduce another event merely for architectural purity if it jeopardizes delivery.

---

# 62. Recommended MVP Repair-Completion Integration

The simplest acceptable first implementation is:

```text
Technician completes WorkOrder
        ↓
WorkOrderService persists Completed
        ↓
UI shows repair completed
        ↓
Security Manager/system-supported demo flow
uses SecurityOperations APIs
to restore asset and resolve incident
```

If a narrow SecurityOperations asset status endpoint is required, use the previously reserved:

```text
PATCH /api/v1/assets/{id}/status
```

rather than allowing WorkOrderService direct database mutation.

A `WorkOrderCompleted` event may later automate this if time permits.

---

# 63. WorkOrder Frontend

Add a Work Orders area to Vision.

Primary screens:

```text
Work Order List
Work Order Detail
Manual Work Order Creation
Technician Assignment
Technician Work View
```

The UI must support the five-minute demo rather than becoming a maintenance-management application.

---

# 64. WorkOrder List UX

Display:

- title,
- asset snapshot,
- location snapshot,
- priority,
- status,
- assigned technician,
- created date.

Make status immediately understandable.

Filters should correspond to backend query support rather than fetching one page and filtering client-side.

Critical work should be visually prominent.

Do not rely solely on color.

---

# 65. WorkOrder Detail UX

Clearly display:

```text
repair title
description
priority
status
asset
location
source incident link/context when available
assigned technician
assignment time
start time
completion time
completion summary
technician notes
```

Infrastructure metadata such as EventId should not dominate the UI.

The screen should look like a maintenance workflow, not a distributed-systems debugger.

---

# 66. Assignment UX

Security Manager:

1. opens `New` WorkOrder,
2. chooses from active technicians,
3. sees technician name/specialty,
4. assigns,
5. WorkOrder immediately displays `Assigned`.

Do not display inactive technicians as normal assignment choices.

---

# 67. Technician UX

Technician should be able to see:

```text
their assigned WorkOrders
```

and on an assigned WorkOrder:

```text
Start Work
```

Once `InProgress`:

```text
Add Repair Note
Complete Work
```

Do not show Technician controls for:

- creating WorkOrders,
- assigning technicians,
- editing Security incidents,
- managing credentials.

---

# 68. Frontend/API Types

Use explicit TypeScript contracts.

Do not use:

```text
any
```

for WorkOrder API data.

Frontend enum values must correspond exactly to backend strings:

```text
New
Assigned
InProgress
Completed
```

Do not independently rename backend `InProgress` to another wire-format value.

Presentation text may render:

```text
In Progress
```

while the API value remains:

```text
InProgress
```

---

# 69. Loading/Error/Empty States

WorkOrder screens must handle:

```text
loading
success
empty
error
```

Examples:

- no assigned work,
- no work matching filters,
- failed assignment,
- invalid lifecycle transition,
- work order not found.

Do not allow failed mutations to appear successful in the UI.

---

# 70. Seed Data

Seed approximately:

```text
3–5 Technicians
```

The current repository's four technicians are appropriate:

```text
Marcus Johnson
Sarah Chen
David Park
Lisa Reeves
```

Keep their specialties believable but simple.

Seed WorkOrders in multiple lifecycle states:

```text
New
Assigned
InProgress
Completed
```

The current persistence seed already provides:

```text
Assigned
InProgress
Completed
```

Add at least one believable `New` WorkOrder so all lifecycle states are represented.

---

# 71. Pharmacy Storage Demo Seed Rule

Do **not** pre-seed an automatically generated WorkOrder for the primary qualifying Pharmacy Storage incident if the intended demo is to show SQS creating it.

The preferred demo path is:

```text
Critical Pharmacy incident
      ↓
no WorkOrder initially
      ↓
IncidentCreated event
      ↓
WorkOrderService consumer
      ↓
new Critical WorkOrder
```

This makes the asynchronous architecture visible and believable.

Other historical work orders may remain seeded.

---

# 72. Seed Consistency

Every seeded external:

```text
SecurityAssetId
SecurityIncidentId
```

must correspond to the deterministic SecurityOperations seed IDs intended by the demo.

Do not create arbitrary cross-service UUIDs that do not reference real seeded Security Operations records.

Seed reruns must not duplicate data.

---

# 73. Logging

Use structured logging.

Examples:

```text
Creating manual work order {WorkOrderId} for asset {AssetId}

Assigning technician {TechnicianId} to work order {WorkOrderId}

Work order {WorkOrderId} moved from Assigned to InProgress

Technician {TechnicianId} added note to work order {WorkOrderId}

Work order {WorkOrderId} completed

Received IncidentCreated event {EventId} for incident {IncidentId}

Work order {WorkOrderId} created from event {EventId}

Duplicate event {EventId} ignored for existing work order {WorkOrderId}
```

Never log:

- bearer tokens,
- AWS credentials,
- connection strings,
- database passwords.

---

# 74. OpenTelemetry Expectations

When observability is added, instrument:

```text
ASP.NET Core requests
EF Core/PostgreSQL
SQS receive/processing
errors
```

The important distributed trace is:

```text
Create Critical Incident
        ↓
SecurityOperations outbox
        ↓
SQS publish
        ↓
WorkOrderService consume
        ↓
WorkOrder INSERT
```

Correlation should survive this workflow.

---

# 75. OpenAPI

OpenAPI must accurately describe:

- WorkOrder endpoints,
- technician endpoints,
- query parameters,
- request DTOs,
- response DTOs,
- enum values,
- important HTTP status codes.

A hiring manager should be able to understand WorkOrderService from Swagger without reading all implementation code.

---

# 76. Test Strategy

Use:

```text
xUnit
```

Focus testing on:

- aggregate lifecycle,
- assignment restrictions,
- notes,
- completion,
- API behavior,
- idempotent consumer behavior,
- database uniqueness,
- authorization once Cognito exists.

Do not create elaborate testing infrastructure that slows MVP delivery.

---

# 77. Domain Unit Tests — Assignment

Required:

```text
New WorkOrder + active technician
    -> Assigned

AssignedTechnicianId populated

AssignedAt populated

UpdatedAt updated
```

Inactive technician:

```text
assignment rejected
```

Attempt assignment outside `New`:

```text
rejected
```

---

# 78. Domain Unit Tests — Start

Required:

```text
Assigned -> InProgress
```

Verify:

```text
StartedAt populated
UpdatedAt updated
```

Starting a:

```text
New
InProgress
Completed
```

WorkOrder should be rejected.

---

# 79. Domain Unit Tests — Completion

Required:

```text
InProgress + completion summary
    -> Completed
```

Verify:

```text
CompletedAt populated
CompletionSummary stored
UpdatedAt updated
```

Also:

```text
InProgress
+ blank summary
+ existing repair note
    -> allowed
```

And:

```text
InProgress
+ blank summary
+ no repair notes
    -> rejected
```

Attempt completion before `InProgress`:

```text
rejected
```

Attempt state change after Completed:

```text
rejected
```

---

# 80. TechnicianNote Tests

Required:

```text
Assigned technician can add valid note

note ID generated
TechnicianId stored
CreatedAt stored
content preserved
```

Reject:

```text
blank note
over-length note
note on New WorkOrder
note on Completed WorkOrder
```

Once authorization exists:

```text
different Technician -> 403
```

---

# 81. API Integration Tests — WorkOrder Reads

Test:

```text
GET list returns seeded work orders
pagination works
status filter works
priority filter works
technician filter works
asset filter works
incident filter works
search works
unknown detail -> 404
detail includes notes
```

---

# 82. API Integration Tests — Manual Creation

Test:

```text
valid manual creation -> 201
status = New
server creates ID
server creates timestamps
SourceEventId null
```

Reject:

```text
missing asset ID
blank title
blank description
invalid priority
```

Duplicate `SecurityIncidentId`:

```text
409
```

---

# 83. API Integration Tests — Assignment

Test:

```text
active technician assignment succeeds
status becomes Assigned
technician returned
AssignedAt populated
```

Reject:

```text
unknown WorkOrder -> 404
unknown Technician -> 404
inactive Technician -> 409
already Assigned -> 409
```

---

# 84. API Integration Tests — Lifecycle

Test:

```text
Assigned -> InProgress succeeds

InProgress -> Completed succeeds

New -> Start rejected

Assigned -> Complete rejected

Completed -> Start rejected

Completed -> Complete rejected
```

Verify timestamps persist.

---

# 85. SQS Acceptance Tests

At minimum:

### Valid qualifying event

```text
IncidentCreated.v1
Critical
asset supplied
```

results in:

```text
exactly one New WorkOrder
Priority = Critical
incident ID copied
asset ID copied
snapshots copied
SourceEventId copied
CorrelationId copied
```

### Duplicate EventId

Deliver same message twice:

```text
one WorkOrder total
```

### Different EventId / same IncidentId

Deliver two messages representing same incident:

```text
one WorkOrder total
```

### Existing manual WorkOrder

Manual WorkOrder already references IncidentId.

Later event arrives.

Result:

```text
no second WorkOrder
message safely handled
```

### Database failure

WorkOrder INSERT fails:

```text
message not acknowledged/deleted as successful
```

---

# 86. Dashboard Summary Tests

Verify:

```text
openCount = New + Assigned + InProgress
```

Completed WorkOrders are excluded.

Summary must derive from persistence.

No Security Operations schema read is allowed.

---

# 87. Authorization Tests — Later Cognito Phase

Security Manager:

```text
can view all WorkOrders
can manually create WorkOrder
can assign technician
cannot perform Technician-only work actions
```

Technician:

```text
can view own assignments
can start own assigned work
can add notes to own assigned work
can complete own work
cannot assign
cannot manually create
cannot manipulate another Technician's work
```

Credential Administrator:

```text
cannot administer WorkOrders
```

Unauthenticated protected mutations:

```text
401
```

Unauthorized authenticated actions:

```text
403
```

---

# 88. Performance

Use:

```text
AsNoTracking
projection
server-side filtering
bounded pagination
appropriate indexes
```

Do not:

```text
load all WorkOrders and filter in memory
Include Notes on every WorkOrder list row
query SecurityOperations database
introduce Redis/cache
```

Normal WorkOrder reads should remain comfortably within the project's normal API performance expectations under demo load.

---

# 89. Explicit Non-Goals

Do not implement:

```text
maintenance schedules
technician shifts
technician calendars
dispatch optimization
route planning
parts inventory
parts ordering
work estimates
billing
labor costing
SLAs
escalations
approvals
preventive maintenance
recurring maintenance
vendor management
attachments
photos
signatures
reopen workflow
cancel workflow
work-order dependencies
multi-technician assignment
subtasks
comments separate from TechnicianNote
audit-event domain entity
maintenance analytics
AI repair recommendations
```

---

# 90. Infrastructure Non-Goals

Do not introduce:

```text
Kafka
EventBridge
Lambda
RabbitMQ
Redis
MongoDB
DynamoDB
another database
another microservice
event sourcing
workflow engine
generic repository framework
```

Amazon SQS is the approved asynchronous mechanism.

PostgreSQL is the persistence technology.

---

# 91. Implementation Sequence

## Slice 1 — Application/API foundation

1. organize WorkOrder features,
2. MediatR setup,
3. FluentValidation,
4. Problem Details/error mapping,
5. API route organization.

## Slice 2 — WorkOrder reads

6. list/detail DTOs,
7. `GetWorkOrdersQuery`,
8. `GetWorkOrderByIdQuery`,
9. endpoints,
10. tests.

## Slice 3 — Technician reads

11. technician DTOs,
12. queries,
13. endpoints,
14. tests.

## Slice 4 — Manual creation and assignment

15. `CreateWorkOrderCommand`,
16. validator,
17. handler,
18. manual-create endpoint,
19. `AssignTechnicianCommand`,
20. assignment endpoint,
21. tests.

## Slice 5 — Repair lifecycle

22. `StartWorkCommand`,
23. `AddTechnicianNoteCommand`,
24. `CompleteWorkOrderCommand`,
25. endpoints,
26. lifecycle tests.

## Slice 6 — Dashboard summary

27. summary query,
28. summary endpoint,
29. tests.

## Slice 7 — Frontend

30. typed WorkOrder API client,
31. WorkOrder list,
32. detail,
33. manual creation,
34. assignment UI,
35. Technician workflow,
36. loading/error/empty states.

## Slice 8 — Messaging

37. explicit `IncidentCreated.v1` consumer DTO,
38. SQS consumer,
39. event-to-WorkOrder mapping,
40. duplicate detection,
41. uniqueness handling,
42. correlation logging,
43. consumer tests,
44. retry/DLQ integration per messaging specification.

---

# 92. Things Kiro Must Not Invent

Do not add:

```text
MaintenanceTask
RepairJob
Assignment entity
Schedule entity
Shift entity
Part entity
Inventory entity
WorkOrderHistory entity
WorkOrderEvent entity
WorkOrderComment entity
Employee entity
shared User entity
shared Person entity
```

`TechnicianNote` remains an owned WorkOrder record.

Do not add cross-service:

```text
SecurityAsset navigation
SecurityIncident navigation
Location navigation
Building navigation
```

---

# 93. ChatGPT Review Checklist

After implementation, review:

## Domain

```text
correct lifecycle
Completed terminal
active Technician requirement
completion-information requirement
notes aggregate-owned
```

## EF Core

```text
work_orders schema only
no cross-service EF relations
unique incident constraint
unique source-event constraint
indexes match queries
owned notes modeled correctly
```

## API

```text
/api/v1
explicit DTOs
pagination
filter behavior
Problem Details
correct status codes
thin HTTP layer
```

## Async

```text
async/await
CancellationToken
no Result/Wait
```

## Messaging

```text
explicit versioned event DTO
no domain entity deserialization
idempotent duplicate handling
DB uniqueness concurrency defense
message ack after persistence
correlation retained
```

## Security

```text
role boundaries ready for Cognito
no fake authentication
no sensitive logs
```

## Frontend

```text
typed contracts
correct enum handling
clear workflow
no client-side business-rule authority
good error states
```

## Portfolio quality

```text
understandable architecture
obvious SQS story
no unnecessary abstractions
five-minute demo remains easy
```

---

# 94. Definition of Done

`WorkOrderService` is MVP-ready when:

```text
✓ service builds and runs
✓ PostgreSQL work_orders schema works
✓ seed is idempotent
✓ WorkOrder remains primary aggregate
✓ TechnicianNote remains aggregate-owned
✓ no cross-service DB relationships exist

✓ WorkOrder list works
✓ filters/search/pagination work
✓ WorkOrder detail works
✓ incident lookup works
✓ asset lookup works

✓ manual WorkOrder creation works
✓ new WorkOrders begin New
✓ duplicate incident WorkOrders prevented

✓ active Technician assignment works
✓ inactive Technician assignment rejected
✓ status becomes Assigned
✓ AssignedAt recorded

✓ assigned work can start
✓ status becomes InProgress
✓ StartedAt recorded

✓ Technician can add repair note
✓ note remains visible

✓ InProgress work can complete
✓ completion information required
✓ CompletedAt recorded
✓ Completed is terminal

✓ dashboard summary returns open count

✓ frontend WorkOrder workflow works
✓ Security Manager can create/assign
✓ Technician workflow is structurally separated

✓ IncidentCreated.v1 creates WorkOrder
✓ event-created WorkOrder uses snapshots
✓ event-created priority mapping correct
✓ duplicate EventId does not duplicate work
✓ duplicate IncidentId does not duplicate work
✓ concurrent duplicate protected by DB
✓ correlation preserved

✓ SQS failure/retry assumptions are safe
✓ CancellationTokens propagate
✓ logging is structured
✓ OpenAPI is useful
✓ important behavior is tested
✓ no unnecessary maintenance-domain expansion
```

---

# 95. Final Service Boundary

```text
                    Next.js Frontend
                           |
                           v
                 +--------------------+
                 | WorkOrderService   |
                 |                    |
                 | WorkOrder Queries  |
                 | Commands           |
                 | Technician Queries |
                 +---------+----------+
                           |
                           v
                  work_orders schema
                           |
              +------------+------------+
              |                         |
              v                         v
          WorkOrder                Technician
              |
              v
       TechnicianNotes
        owned records


SecurityOperationsService
          |
          | IncidentCreated.v1
          v
     Transactional Outbox
          |
          v
      Amazon SQS
          |
          v
   WorkOrderService
          |
          | idempotent consume
          v
      New WorkOrder


Cross-service relationship:

SecurityIncident
      |
      | external IncidentId only
      v
WorkOrder

SecurityAsset
      |
      | external AssetId + snapshots only
      v
WorkOrder
```

---

# 96. Governing Principle

The WorkOrder slice exists to make one business outcome extremely clear:

> **A failed physical-security asset can be handed from a Security Manager to a Technician, repaired, documented, and completed through a believable enterprise workflow.**

The architecture should make the asynchronous incident-to-repair handoff visible and defensible without turning the MVP into a maintenance-management platform.

Build the smallest system that demonstrates:

```text
clear ownership
clean contracts
correct lifecycle behavior
idempotent messaging
useful operational traceability
good user experience
senior-level engineering judgment
```
