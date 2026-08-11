# Vision — Business & Domain Specification

**Status:** MVP specification / implementation baseline  
**Scope:** One-week MVP  
**Primary implementation agent:** Amazon Kiro  
**Architecture/specification/review agent:** ChatGPT  
**Target repository path:** `docs/business-domain-specification.md`

---

## 1. Purpose

This document defines the business scope and minimum domain model for the **Vision** one-week MVP.

Vision is a hospital-focused Physical Security Operations and Credential Management SaaS application. The MVP must behave like a small, believable enterprise product rather than a technology showcase. Its purpose is to give hospital security and facilities personnel a fast operational view of security equipment, incidents, repair work, and access credentials.

This document is intended to remove domain ambiguity before Amazon Kiro commits deeply to persistence, service implementation, API contracts, or frontend behavior.

Unless a later approved specification explicitly supersedes this document, Kiro should treat the business rules, domain boundaries, entities, enums, relationships, state transitions, and MVP exclusions here as implementation constraints.

---

## 2. Source-of-Truth Relationship

This specification is derived from and must remain consistent with the current Vision source documents:

- `README.md`
- `technology-specification.md`

Where those documents deliberately deferred detailed business/domain decisions, this document resolves only the decisions required to implement the one-week MVP.

The following existing constraints remain unchanged:

- The product is initially hospital-focused.
- The MVP uses three backend services:
  - `SecurityOperationsService`
  - `WorkOrderService`
  - `CredentialService`
- The MVP uses three application personas:
  - Security Manager
  - Technician
  - Credential Administrator
- The minimum domain contains these nine entities:
  - Hospital
  - Building
  - Location
  - SecurityAsset
  - SecurityIncident
  - WorkOrder
  - Technician
  - Person
  - Credential
- The principal employer-facing workflow is a camera outage that becomes an incident and repair workflow, followed by revocation of a lost credential.
- Anything not required to make that workflow believable and polished is post-MVP unless explicitly stated otherwise.

---

# 3. Product Definition

## 3.1 Product Statement

**Vision gives hospital security and facilities teams a centralized operational view of physical-security assets, security incidents, maintenance work, and personnel credentials so they can quickly understand whether critical areas are secure and whether important security systems are functioning properly.**

The MVP is intentionally not a full physical-security management platform. It is a focused operational application demonstrating the highest-value portions of that problem.

---

## 3.2 Primary Customer

For the MVP, the modeled customer is:

> **A hospital or medical center operating multiple buildings, departments, and restricted locations with centrally managed physical-security equipment and employee/contractor credentials.**

The seeded demonstration customer should be:

> **Northstar Medical Center**

Multi-hospital tenancy is explicitly outside the MVP. The application may therefore assume one hospital in the demonstration environment while keeping identifiers and relationships clean enough that future expansion would not require a destructive redesign.

---

## 3.3 Business Problems Addressed

Vision answers the following operational questions:

1. Which physical-security assets are functioning normally?
2. Which cameras, controlled doors, badge readers, or gates are degraded or offline?
3. Which active security incidents require attention?
4. Has maintenance work been created for failed equipment?
5. Who is responsible for repairing a failed asset?
6. Has repair work been completed?
7. Which employee or contractor credentials are active, expired, approaching expiration, or revoked?
8. Can a lost credential be revoked immediately by an authorized user?

The MVP should optimize for answering these questions quickly and clearly.

---

# 4. MVP Success Definition

The MVP is complete when a first-time reviewer can understand and perform the following story in approximately five minutes:

1. Open Vision and immediately understand that it is a hospital physical-security operations product.
2. See the hospital-wide security status.
3. Find a critical offline camera at or near **Pharmacy Storage**.
4. Open the camera and inspect its current status.
5. Inspect or create the associated security incident.
6. Create or inspect the maintenance work order.
7. Assign a technician.
8. Move the work order into active repair.
9. Add a technician repair note.
10. Complete the work order.
11. See the operational dashboard reflect the improved state.
12. Open Credential Management.
13. Find an employee whose badge was reported lost.
14. Revoke the credential.
15. See that the credential is now clearly shown as revoked.

The demo must require no training, hidden setup, or explanation of unfinished controls.

---

# 5. MVP Personas

## 5.1 Security Manager

### Business responsibility

Monitors the hospital's physical-security posture and coordinates response to security-equipment incidents.

### MVP capabilities

- View dashboard.
- Browse/search/filter security assets.
- View asset details.
- View incidents.
- Create incidents.
- Update incident status.
- View work orders associated with security incidents/assets.
- Create a work order manually when necessary.
- Assign a technician to a work order.
- Browse people relevant to credential management.
- View a person's credentials.
- Issue a credential.
- View credential state and expiration.
- Revoke a credential.

---

## 5.2 Technician

### Business responsibility

Performs maintenance work on failed or degraded physical-security equipment.

### MVP capabilities

- View assigned work orders.
- View work-order details.
- Move an assigned work order to `InProgress`.
- Add technician notes.
- Complete assigned repair work.

### Explicit non-responsibilities

A Technician does not manage credentials and does not administer hospital security incidents beyond the maintenance workflow.

---

## 5.3 Credential Administrator

### Business responsibility

Administers employee and contractor physical-access credentials.

### MVP capabilities

- Browse people relevant to credential management.
- View a person's credentials.
- Issue a credential.
- View credential state and expiration.
- Revoke a credential.

### Explicit non-responsibilities

The MVP does not require a Credential Administrator to manage security assets, incidents, or repair work.

---

# 6. MVP Capability Boundaries

The MVP contains six product capabilities.

## 6.1 Security Operations Dashboard

The dashboard provides an immediate operational summary.

It should display, at minimum:

- Hospital name.
- Overall security-operational percentage or equivalent health indicator.
- Assets operational versus total.
- Degraded/offline asset count.
- Active critical incident count.
- Open work-order count.
- Credentials approaching expiration count.
- Critical alerts.
- Recent operational activity.

### MVP interpretation

The dashboard is a read model, not a domain entity.

`SecurityOperationsService` owns the core security-health portion of the dashboard. Work-order and credential summary values originate from their owning services. The frontend may compose those read values rather than creating cross-service database ownership.

No service may directly treat another service's schema/tables as its owned persistence model.

---

## 6.2 Security Asset Inventory

Users can:

- Browse assets.
- Search assets.
- Filter by status.
- Filter by asset type.
- Filter by building/location where useful.
- View asset details.
- View current operational state.
- View last-service date when known.
- View related incidents.
- Follow a related work order through the incident/work-order workflow.

Initial asset types:

- Camera
- AccessControlledDoor
- BadgeReader
- SecurityGate

No asset subtype tables or subtype-specific behavior are required for the MVP. Asset type is represented by an enum on `SecurityAsset`.

---

## 6.3 Security Incident Management

Security personnel can:

- Create an incident for a security asset/location.
- View incident details.
- View severity.
- View status.
- Move an incident from `Open` to `Investigating`.
- Resolve an incident when the operational problem has been addressed.
- Associate an incident with a maintenance work order.

Initial statuses:

- Open
- Investigating
- Resolved

The MVP does not include a generalized incident-response workflow engine, SLA engine, escalation hierarchy, or approval process.

---

## 6.4 Work Order Management

Users can:

- Create a maintenance work order.
- Associate it with the source incident.
- Associate it with the affected asset.
- Assign a technician.
- Move it through a simple lifecycle.
- Add technician notes.
- Complete the repair.

Initial statuses:

- New
- Assigned
- InProgress
- Completed

Complex scheduling, dispatch optimization, shifts, technician calendars, inventory/parts management, estimates, billing, SLAs, and preventive maintenance are out of scope.

---

## 6.5 Credential Management

Authorized users can:

- View people.
- View credentials.
- Issue a credential.
- Set a simple access level.
- View expiration.
- Revoke a credential.

Initial access levels:

- General
- Clinical
- Restricted
- Security

The MVP represents access level as a simple classification. It does not model detailed access zones, door-by-door grants, inheritance, approval workflows, or access policy evaluation.

---

## 6.6 Authentication and Authorization

Authentication is provided by Amazon Cognito.

Initial application roles:

- SecurityManager
- Technician
- CredentialAdministrator

Authorization must be enforced by backend APIs. Hiding a control in the frontend does not constitute authorization.

Authentication identity is not itself part of the business-domain model defined here. Cognito is the identity provider; no custom User/Auth microservice should be added.

---

# 7. Minimum Domain Model

## 7.1 Overview

The one-week MVP has exactly nine required business entities:

```text
Hospital
  1
  |
  *
Building
  1
  |
  *
Location
  1
  |
  *
SecurityAsset
  1
  |
  *
SecurityIncident
  1
  |
  0..1
WorkOrder


Person
  1
  |
  *
Credential


Technician
  1
  |
  *
WorkOrder (assignment)
```

Cross-service relationships are represented by stable identifiers and, where useful for display, small immutable snapshots. They are not EF navigation relationships across service DbContexts.

---

# 8. Entity Specifications

## 8.1 Hospital

### Responsibility

Represents the hospital organization whose physical-security posture Vision is displaying.

### Service ownership

`SecurityOperationsService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `Name` | string | Yes | e.g. `Northstar Medical Center` |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `Code` | string | Short display/internal code if useful |

### Relationships

- Hospital `1 -> many` Building.

### Validation

- Name must not be empty.
- Name should be limited to a reasonable display length, recommended maximum 200 characters.
- For the MVP, only one seeded hospital is required.

### Not modeled

- Tenant billing.
- Hospital networks.
- Separate customer/account entity.
- Multi-hospital tenancy.

---

## 8.2 Building

### Responsibility

Represents a physical building belonging to the hospital.

### Service ownership

`SecurityOperationsService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `HospitalId` | UUID | Yes | Owning Hospital |
| `Name` | string | Yes | Human-readable name |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Relationships

- Building `many -> 1` Hospital.
- Building `1 -> many` Location.

### Validation

- `HospitalId` must reference an existing hospital.
- Name must not be empty.
- Recommended maximum name length: 150 characters.

### MVP simplification

A separate `Floor` entity is **not** required. Floor/department/area information belongs on `Location`.

---

## 8.3 Location

### Responsibility

Represents a meaningful physical area where security equipment is installed or an incident occurs.

Examples:

- Emergency Department Entrance
- Pharmacy Storage
- ICU East Corridor
- Main Lobby
- Surgical Wing Staff Entrance
- Data Center Entrance

### Service ownership

`SecurityOperationsService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `BuildingId` | UUID | Yes | Owning Building |
| `Name` | string | Yes | Display name |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `Floor` | string | e.g. `1`, `2`, `B1` |
| `Department` | string | e.g. Pharmacy, ICU |
| `Description` | string | Short contextual description |

### Relationships

- Location `many -> 1` Building.
- Location `1 -> many` SecurityAsset.
- Location may be referenced by SecurityIncident.

### Validation

- `BuildingId` must reference an existing building.
- Name must not be empty.
- Recommended maximum name length: 150 characters.

### MVP simplification

Do not introduce:

- Floor entity.
- Department entity.
- AccessZone entity.
- Coordinate geometry.
- Floor-plan objects.

---

## 8.4 SecurityAsset

### Responsibility

Represents one physical-security device or controlled security point monitored by Vision.

### Service ownership

`SecurityOperationsService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `LocationId` | UUID | Yes | Physical location |
| `Name` | string | Yes | Human-readable identifier |
| `AssetType` | `SecurityAssetType` | Yes | Enum |
| `Status` | `SecurityAssetStatus` | Yes | Current operational state |
| `CreatedAt` | DateTimeOffset | Yes | UTC |
| `UpdatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `AssetTag` | string | Human-readable inventory code |
| `Manufacturer` | string | Seed-data realism only |
| `Model` | string | Seed-data realism only |
| `LastServiceAt` | DateTimeOffset | Last known completed service |
| `StatusChangedAt` | DateTimeOffset | When current operational status began |
| `Description` | string | Short descriptive text |

### Relationships

- SecurityAsset `many -> 1` Location.
- SecurityAsset `1 -> many` SecurityIncident.
- A work order in `WorkOrderService` may reference the asset by `SecurityAssetId`.

### Validation

- `LocationId` must exist.
- Name must not be empty.
- Asset type must be a defined enum value.
- Status must be a defined enum value.

### Business rules

1. An asset is one of the four MVP asset types.
2. Asset status describes operational health, not incident workflow state.
3. An asset may have zero or many incidents over time.
4. Completing a repair may cause its asset to return to `Operational`.
5. The exact synchronization mechanism between completed work and asset status can be implemented synchronously first; asynchronous enhancement must not block the base workflow.
6. The MVP does not ingest live device telemetry. Status is application/demo data.

---

## 8.5 SecurityIncident

### Responsibility

Represents an operational security problem or event involving a hospital location and, usually, a security asset.

### Service ownership

`SecurityOperationsService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `LocationId` | UUID | Yes | Where incident occurred |
| `Severity` | `IncidentSeverity` | Yes | Enum |
| `Status` | `IncidentStatus` | Yes | Enum |
| `Title` | string | Yes | Concise display title |
| `Description` | string | Yes | Operational description |
| `CreatedAt` | DateTimeOffset | Yes | UTC |
| `UpdatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `SecurityAssetId` | UUID | Most MVP incidents should have one |
| `ResolvedAt` | DateTimeOffset | Required once resolved |
| `ResolutionSummary` | string | Required when resolving |
| `WorkOrderId` | UUID | External reference to WorkOrderService once known |

### Relationships

- SecurityIncident `many -> 1` Location.
- SecurityIncident `many -> 0..1` SecurityAsset.
- SecurityIncident `1 -> 0..1` WorkOrder for the one-week MVP.

### Validation

- Title required; recommended maximum 150 characters.
- Description required; recommended maximum 2,000 characters.
- Severity must be valid.
- Status must be valid.
- `ResolvedAt` must be null unless status is `Resolved`.
- Resolving an incident requires a non-empty `ResolutionSummary`.
- If `SecurityAssetId` is present, the asset must belong to the specified location.

### Business rules

1. New incidents start as `Open`.
2. `Open -> Investigating` is valid.
3. `Investigating -> Resolved` is valid.
4. `Open -> Resolved` is allowed for the MVP for simple/rapidly resolved incidents.
5. A resolved incident cannot return to an active state in the MVP.
6. A critical equipment incident may cause a maintenance work order to be created.
7. An incident may exist without a work order when maintenance is not required.
8. For the MVP, an incident has at most one associated work order.
9. Creating the same async work order more than once for the same incident must be prevented by the Work Order consumer.

---

## 8.6 WorkOrder

### Responsibility

Represents maintenance work required to restore a failed/degraded physical-security asset.

### Service ownership

`WorkOrderService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `SecurityAssetId` | UUID | Yes | External identifier from SecurityOperationsService |
| `Title` | string | Yes | Repair task |
| `Description` | string | Yes | Work required |
| `Priority` | `WorkOrderPriority` | Yes | Minimal enum |
| `Status` | `WorkOrderStatus` | Yes | Lifecycle enum |
| `CreatedAt` | DateTimeOffset | Yes | UTC |
| `UpdatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `SecurityIncidentId` | UUID | Source incident |
| `AssignedTechnicianId` | UUID | Null until assigned |
| `AssignedAt` | DateTimeOffset | Set when assigned |
| `StartedAt` | DateTimeOffset | Set on first `InProgress` transition |
| `CompletedAt` | DateTimeOffset | Set when completed |
| `CompletionSummary` | string | Recommended/required on completion |
| `AssetNameSnapshot` | string | Optional display snapshot |
| `LocationNameSnapshot` | string | Optional display snapshot |
| `CorrelationId` | string | For cross-service traceability |
| `SourceEventId` | UUID/string | For idempotent event consumption |

### Relationships

- WorkOrder `many -> 0..1` Technician.
- WorkOrder references exactly one SecurityAsset by external ID.
- WorkOrder may reference one SecurityIncident by external ID.
- In the MVP, one SecurityIncident produces at most one WorkOrder.

### Validation

- Asset ID required.
- Title required; recommended maximum 150 characters.
- Description required; recommended maximum 2,000 characters.
- A WorkOrder cannot enter `Assigned` without an active technician.
- A WorkOrder cannot enter `InProgress` without an assigned technician.
- A WorkOrder cannot enter `Completed` without an assigned technician.
- Completion requires a completion summary or final technician note.
- Completed work orders are terminal in the MVP.

### Business rules

1. New manually or asynchronously created work orders begin as `New`.
2. Assigning a technician changes `New -> Assigned`.
3. Starting repair changes `Assigned -> InProgress`.
4. Completing repair changes `InProgress -> Completed`.
5. The MVP does not support reopening a completed work order.
6. The MVP does not support canceling work orders.
7. The async `IncidentCreated` consumer must be idempotent.
8. A duplicate event for the same qualifying incident must not create a second work order.
9. Technician notes are part of the work-order lifecycle, but do not require a tenth business entity for the MVP; store them as owned/value records within the Work Order service persistence model.

---

## 8.7 Technician

### Responsibility

Represents a maintenance/security technician who can be assigned physical-security repair work.

### Service ownership

`WorkOrderService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `DisplayName` | string | Yes | Human-readable |
| `Email` | string | Yes | Useful for identity/display |
| `IsActive` | bool | Yes | Assignment eligibility |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `CognitoSubject` | string | Links authenticated technician when needed |
| `Specialty` | string | Seed realism only; no scheduling logic |

### Relationships

- Technician `1 -> many` WorkOrder assignments.

### Validation

- Display name required.
- Email required and syntactically valid.
- Only active technicians may receive new assignments.

### Important boundary

`Technician` is intentionally separate from `Person`.

`Person` is owned by CredentialService and represents someone whose physical-access credential is administered. `Technician` is owned by WorkOrderService and represents someone assignable to repair work. A real employee might conceptually be both, but the MVP does not need cross-service person synchronization to demonstrate the business story.

Do not create a shared `Employee` aggregate or shared database entity to unify them for the MVP.

---

## 8.8 Person

### Responsibility

Represents an employee or contractor whose physical-access credential is administered by Vision.

### Service ownership

`CredentialService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `FirstName` | string | Yes | |
| `LastName` | string | Yes | |
| `PersonType` | `PersonType` | Yes | Employee or Contractor |
| `IsActive` | bool | Yes | Current relationship to hospital |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `EmployeeNumber` | string | Demo realism |
| `Email` | string | |
| `Department` | string | e.g. Pharmacy |
| `JobTitle` | string | |
| `UpdatedAt` | DateTimeOffset | |

### Relationships

- Person `1 -> many` Credential.

### Validation

- First and last names required.
- `PersonType` must be valid.
- Employee number, if supplied, should be unique within CredentialService.

### Business rules

1. Both hospital employees and contractors may have credentials.
2. A person may have historical credentials.
3. The MVP does not model HR employment workflows.
4. Deactivating a person does not automatically implement a complex revocation workflow in the one-week MVP; credential actions remain explicit.

---

## 8.9 Credential

### Responsibility

Represents a physical-access badge/credential issued to a Person.

### Service ownership

`CredentialService`

### Required properties

| Property | Type | Required | Notes |
|---|---|---:|---|
| `Id` | UUID | Yes | Stable identifier |
| `PersonId` | UUID | Yes | Credential holder |
| `CredentialNumber` | string | Yes | Human-readable badge/credential identifier |
| `AccessLevel` | `CredentialAccessLevel` | Yes | Enum |
| `IssuedAt` | DateTimeOffset | Yes | |
| `ExpiresAt` | DateTimeOffset | Yes | |
| `CreatedAt` | DateTimeOffset | Yes | UTC |

### Optional properties

| Property | Type | Notes |
|---|---|---|
| `RevokedAt` | DateTimeOffset | Null unless revoked |
| `RevocationReason` | string | Required on revoke |
| `UpdatedAt` | DateTimeOffset | |

### Derived status

Credential status should be derived as:

```text
if RevokedAt != null
    => Revoked
else if ExpiresAt <= now
    => Expired
else
    => Active
```

`CredentialStatus` may be exposed in DTOs and domain behavior, but it should not need to be redundantly persisted if the implementation can derive it consistently.

### Relationships

- Credential `many -> 1` Person.

### Validation

- Person must exist.
- Credential number required and unique.
- `ExpiresAt` must be after `IssuedAt`.
- Access level must be valid.
- Revocation reason required when revoking.
- `RevokedAt` cannot precede `IssuedAt`.

### Business rules

1. New credentials are active if their expiration date is in the future.
2. An expired credential is not active.
3. A revoked credential is not active regardless of expiration date.
4. Revocation is terminal for the MVP.
5. Revoking an already revoked credential is **idempotent**:
   - it remains revoked,
   - the original `RevokedAt` should not be replaced,
   - no duplicate revocation side effect should occur.
6. The MVP does not reactivate revoked credentials.
7. The MVP does not alter physical badge hardware; revocation represents the business/application state.
8. “Expiring soon” is a read-model concept, recommended as credentials expiring within the next 30 days. This threshold should be a named configuration/domain constant rather than duplicated magic numbers.

---

# 9. Enumerations

## 9.1 SecurityAssetType

```text
Camera
AccessControlledDoor
BadgeReader
SecurityGate
```

---

## 9.2 SecurityAssetStatus

```text
Operational
Degraded
Offline
```

Rationale:

- `Operational` — functioning normally.
- `Degraded` — partially functioning or requires attention but not completely unavailable.
- `Offline` — unavailable/nonfunctional.

Do not add a large hardware telemetry state machine for the MVP.

---

## 9.3 IncidentSeverity

```text
Low
Medium
High
Critical
```

Critical is required for the primary demo and SQS workflow.

---

## 9.4 IncidentStatus

```text
Open
Investigating
Resolved
```

Valid transitions:

```text
Open ---------> Investigating ---------> Resolved
  \------------------------------------> Resolved
```

`Resolved` is terminal in the MVP.

---

## 9.5 WorkOrderStatus

```text
New
Assigned
InProgress
Completed
```

Valid transitions:

```text
New -> Assigned -> InProgress -> Completed
```

No skipping states is required for the main repair workflow.

---

## 9.6 WorkOrderPriority

```text
Low
Medium
High
Critical
```

When a work order is automatically created from an incident, its initial priority should normally map from the incident severity.

---

## 9.7 CredentialAccessLevel

```text
General
Clinical
Restricted
Security
```

This is intentionally coarse-grained.

---

## 9.8 CredentialStatus

```text
Active
Expired
Revoked
```

Prefer deriving this status from revocation and expiration data.

---

## 9.9 PersonType

```text
Employee
Contractor
```

No additional person categories are required for the MVP.

---

# 10. Aggregate and Ownership Boundaries

## 10.1 SecurityOperationsService

Owns:

- Hospital
- Building
- Location
- SecurityAsset
- SecurityIncident

Primary aggregates:

- `Hospital` hierarchy for facility/reference data.
- `SecurityAsset` for asset operational state.
- `SecurityIncident` for incident lifecycle.

The service may expose dashboard read models computed from its owned data.

It must not own WorkOrder, Technician, Person, or Credential persistence.

---

## 10.2 WorkOrderService

Owns:

- WorkOrder
- Technician
- Technician notes as owned/value records

Primary aggregate:

- `WorkOrder`

External references:

- `SecurityIncidentId`
- `SecurityAssetId`

The service must not depend on EF relationships into SecurityOperationsService tables.

---

## 10.3 CredentialService

Owns:

- Person
- Credential

Primary aggregate:

- `Person` with credentials, or `Credential` as an independently addressed aggregate depending on implementation style.

The service is authoritative for credential lifecycle and access-level state.

---

# 11. Cross-Service Domain Rules

## 11.1 No Cross-Service Database Ownership

All three services may use the same PostgreSQL/Neon instance for the MVP, but they must have clear logical ownership.

Recommended schemas:

```text
security_operations.*
work_orders.*
credentials.*
```

A service must not directly mutate another service's owned tables.

Cross-service business references use identifiers, API contracts, integration events, or small snapshots.

---

## 11.2 Incident-to-Work-Order Messaging Workflow

The MVP's one meaningful asynchronous workflow is:

```text
Critical equipment incident created
        |
        v
SecurityOperationsService
        |
        v
IncidentCreated integration event
        |
        v
Amazon SQS
        |
        v
WorkOrderService
        |
        v
Maintenance WorkOrder created
```

Minimum event information should be sufficient for the WorkOrderService to create a believable work order without reading another service's database.

Recommended event payload semantics:

- EventId
- OccurredAt
- CorrelationId
- IncidentId
- IncidentSeverity
- IncidentTitle
- IncidentDescription
- SecurityAssetId
- AssetName
- LocationId
- LocationName

### Qualification rule

For the one-week MVP, automatic work-order creation is required only for a **Critical incident associated with a SecurityAsset**.

Other incidents may have a manually created work order.

### Idempotency rule

`EventId` and/or `IncidentId` must be used to ensure duplicate SQS deliveries do not create duplicate work orders.

---

# 12. Status Transition Rules

## 12.1 Asset Status

No complex transition graph is required.

Allowed states:

```text
Operational
Degraded
Offline
```

The main demo starts with the camera `Offline` and ends with it `Operational`.

A completed repair may update the associated asset to `Operational` as part of the demo workflow.

---

## 12.2 Incident Status

```text
Open -> Investigating -> Resolved
Open -----------------> Resolved
```

Rules:

- New incident => `Open`.
- Resolution requires `ResolutionSummary`.
- Resolution sets `ResolvedAt`.
- Resolved is terminal.

---

## 12.3 Work Order Status

```text
New -> Assigned -> InProgress -> Completed
```

Rules:

- Assignment requires an active Technician.
- Entering `Assigned` sets `AssignedTechnicianId` and `AssignedAt`.
- Entering `InProgress` sets `StartedAt` on first transition.
- Entering `Completed` sets `CompletedAt`.
- Completion requires a completion summary or final repair note.
- Completed is terminal.

---

## 12.4 Credential Status

Derived:

```text
RevokedAt != null         -> Revoked
else ExpiresAt <= now     -> Expired
else                      -> Active
```

Rules:

- Revoke sets `RevokedAt` and `RevocationReason`.
- Revoke is idempotent.
- Revoked is terminal in the MVP.

---

# 13. Core Business Workflows

## 13.1 Workflow A — Investigate Offline Camera

**Actor:** Security Manager

1. Dashboard shows a critical offline camera.
2. Security Manager opens asset details.
3. Asset status is `Offline`.
4. Associated open/critical incident is visible.
5. Security Manager opens incident details.
6. Incident context includes location and affected asset.

### Acceptance criteria

- Asset can be found without knowing its internal ID.
- Location and status are obvious.
- Incident is reachable directly from asset context.
- The screen must not require understanding backend architecture.

---

## 13.2 Workflow B — Create Security Incident

**Actor:** Security Manager

1. Select an asset or location.
2. Provide title.
3. Provide description.
4. Select severity.
5. Submit.
6. Incident is created as `Open`.

For a Critical incident associated with an asset, the service publishes the integration event used by the SQS workflow.

### Acceptance criteria

- Invalid/missing required data returns validation errors.
- Incident receives server-generated ID/timestamps.
- New status is `Open`.
- Duplicate API submission should not be intentionally generated by the UI.
- Message publication failure must be observable and must not silently pretend the async workflow succeeded.

---

## 13.3 Workflow C — Create/Assign Repair Work

**Actors:** Security Manager, then Technician

1. Work order is automatically created from the qualifying incident or manually created.
2. Work order begins as `New`.
3. Security Manager assigns an active Technician.
4. Work order becomes `Assigned`.
5. Technician begins work.
6. Work order becomes `InProgress`.
7. Technician adds a repair note.
8. Technician completes work.
9. Work order becomes `Completed`.

### Acceptance criteria

- A work order cannot be `Assigned` without a technician.
- A work order cannot be `InProgress` without assignment.
- A work order cannot be completed before `InProgress`.
- Duplicate incident messages do not create duplicate work orders.
- Completion timestamp is recorded.
- Repair note/completion summary remains visible.

---

## 13.4 Workflow D — Restore Asset / Resolve Incident

**Actor:** Security Manager or system-assisted demo flow

1. Work order completes.
2. Affected asset becomes `Operational`.
3. Incident is resolved with resolution information.
4. Dashboard security status improves.

### MVP implementation guidance

The business outcome matters more than introducing a second integration event.

Kiro may first implement this through the simplest clean synchronous/API-supported mechanism that preserves service ownership. A follow-up event such as `WorkOrderCompleted` may be added only if it can be done without jeopardizing the one-week schedule.

---

## 13.5 Workflow E — Revoke Lost Credential

**Actor:** Security Manager or Credential Administrator

1. Browse/search personnel.
2. Open employee.
3. View active credential.
4. Choose revoke.
5. Enter/select reason such as `Lost badge`.
6. Confirm.
7. Credential becomes `Revoked`.
8. Revocation timestamp and reason are visible.

### Acceptance criteria

- Only authorized role may revoke.
- Credential not found => 404 at API layer.
- Already revoked credential => idempotent success.
- Revocation timestamp is populated once.
- Revocation reason is preserved.
- Credential is no longer reported as active.

---

# 14. Dashboard Business Rules

## 14.1 Security Operational Percentage

For the MVP, the recommended security-operational percentage is:

```text
OperationalAssets / TotalAssets * 100
```

Round to a whole percentage for the headline display.

`Degraded` and `Offline` assets count as non-operational for this headline metric.

This simple rule makes the metric understandable and reproducible. Do not invent a weighted security-risk scoring algorithm for the MVP.

---

## 14.2 Active Critical Incidents

Count incidents where:

```text
Severity == Critical
AND Status != Resolved
```

---

## 14.3 Open Work Orders

Count work orders where:

```text
Status != Completed
```

---

## 14.4 Expiring Credentials

Recommended MVP definition:

```text
Credential is Active
AND ExpiresAt > now
AND ExpiresAt <= now + 30 days
```

Use UTC/current time consistently.

---

## 14.5 Recent Activity

Recent activity may be constructed from existing entity timestamps and actions.

Do **not** add a full `AuditEvent` domain entity solely to populate the MVP dashboard.

A richer audit trail is post-MVP.

---

# 15. Seed Data Specification

Seed data should make Vision look operational immediately.

## 15.1 Hospital

One:

- Northstar Medical Center

---

## 15.2 Buildings and Locations

Use a believable small structure such as:

### Main Hospital

- Main Lobby
- Emergency Department Entrance
- Pharmacy Storage
- ICU East Corridor
- Surgical Wing Staff Entrance

### Administrative Building

- Administration Lobby
- Records Storage Entrance

### Data Center

- Data Center Entrance
- Server Room Corridor

This is sufficient to create geographic/contextual variety without building a facilities-management subsystem.

---

## 15.3 Security Assets

Target approximately 40–60 assets.

Distribution should include:

- Cameras
- Access-controlled doors
- Badge readers
- Security gates

Most should be `Operational`.

Seed approximately:

- 3–5 degraded/offline assets.
- At least one `Critical`-story camera:
  - Camera
  - Location: Pharmacy Storage
  - Status: Offline

The dashboard should look healthy overall while still containing a clear problem to investigate.

---

## 15.4 Incidents

Seed several incidents with varied severities/statuses.

Must include:

- Critical incident associated with the Pharmacy Storage offline camera.
- At least one resolved historical incident to make history believable.
- At least one non-critical active incident.

---

## 15.5 Technicians and Work Orders

Seed approximately 3–5 technicians.

Seed several work orders in different states:

- New
- Assigned
- InProgress
- Completed

The principal camera story should have either:

- a work order created during the demo, or
- an existing work order that the reviewer can continue.

Prefer the option that makes the five-minute demo the most reliable.

---

## 15.6 People and Credentials

Seed approximately 15–25 people across:

- Employees
- Contractors

Include credentials that are:

- Active
- Expiring within 30 days
- Expired
- Revoked

Must include one employee with an active credential that can be described in the demo as a reported lost badge and then revoked.

---

# 16. Minimum Validation Rules Summary

The following validation is mandatory for MVP behavior:

### Facility/security operations

- Required names cannot be blank.
- Referenced Hospital/Building/Location must exist within owning service.
- Asset type/status values must be valid.
- Incident title/description/severity required.
- Resolution requires resolution summary.
- Asset/location consistency must be enforced for incidents.

### Work orders

- Asset ID required.
- Title/description required.
- Assignment requires active technician.
- Lifecycle transitions must follow the defined order.
- Completion requires repair completion information.
- Duplicate incident event must not produce duplicate work order.

### Credentials

- Person must exist.
- Credential number unique.
- Expiration after issue date.
- Access level valid.
- Revocation reason required.
- Repeated revocation is idempotent.

---

# 17. Authorization Matrix

| Capability | Security Manager | Technician | Credential Administrator |
|---|:---:|:---:|:---:|
| View dashboard | Yes | Optional read | Optional read |
| Browse assets | Yes | Read if needed for assigned work | No |
| View asset details | Yes | Read if needed for assigned work | No |
| Create/update incidents | Yes | No | No |
| View work orders | Yes | Assigned work only | No |
| Create work order | Yes | No | No |
| Assign technician | Yes | No | No |
| Start assigned work | No | Yes | No |
| Add technician note | No | Yes | No |
| Complete assigned work | No | Yes | No |
| Browse people | Yes | No | Yes |
| Issue credential | Yes | No | Yes |
| Revoke credential | Yes | No | Yes |

For the MVP, Kiro may implement the smallest policy set that enforces these important distinctions. Do not build a large permission matrix.

---

# 18. Explicit MVP Non-Goals

Do **not** add the following unless the project owner explicitly approves an MVP change:

- Multi-hospital tenancy.
- Customer/account/billing subsystem.
- Visitor management.
- Physical badge printing.
- Real access-control hardware integration.
- Real-time telemetry ingestion.
- Floor-plan mapping.
- Floor entity unless implementation proves it truly necessary.
- Department entity.
- AccessZone entity.
- Door-by-door credential grants.
- Access approval workflows.
- Detailed access inheritance.
- Complex technician scheduling.
- Shift management.
- Dispatch optimization.
- Parts/inventory management.
- Preventive maintenance scheduling.
- SLA engine.
- Escalation engine.
- Advanced analytics.
- AI features.
- Recurring failure intelligence.
- Full audit-reporting suite.
- Native mobile app.
- Additional microservices.
- Shared business-entity database ownership across services.
- Kafka.
- Lambda.
- EventBridge.
- Managed production Kubernetes.

---

# 19. Domain Decisions Intentionally Deferred Until After MVP

The following are legitimate future questions but must not block the one-week MVP:

1. Whether Location should evolve into Building/Floor/Department/Zone submodels.
2. Whether Technician and credentialed Person should share an enterprise person identity.
3. Whether WorkOrder completion should publish a `WorkOrderCompleted` integration event.
4. Whether incident/work-order relationship should eventually become one-to-many.
5. Whether credential access level should evolve into access zones and grants.
6. Whether asset subtype-specific properties should become separate models.
7. Whether a full audit/event history should become a first-class domain capability.
8. Whether multi-hospital tenancy should be introduced.
9. Whether dashboards should be served through a dedicated composition/BFF layer.

Do not pre-solve these future problems in the MVP implementation.

---

# 20. Kiro Implementation Guardrails

Kiro should implement against the following rules:

1. **Do not introduce business entities beyond the nine defined here merely for modeling purity.**
2. Value/owned records such as technician notes are allowed where persistence requires them, but they are not new top-level business capabilities.
3. **Do not merge service-owned aggregates into a shared domain model.**
4. Use UUID identifiers consistently across service boundaries.
5. Use UTC timestamps (`DateTimeOffset` preferred).
6. Keep controllers thin.
7. Put lifecycle and validation behavior in the appropriate application/domain layer.
8. Use FluentValidation for request validation where consistent with the technology specification.
9. Use MediatR/CQRS where it improves the meaningful workflows; do not force it into trivial reads.
10. Propagate `CancellationToken` through async I/O.
11. Treat SQS duplicate delivery as normal.
12. Make the Incident-to-WorkOrder consumer idempotent.
13. Do not let another service directly mutate a service-owned PostgreSQL schema.
14. Prefer simple read composition over shared database coupling.
15. Do not add infrastructure or domain complexity that endangers the one-week schedule.
16. When ambiguity remains, optimize for the five-minute employer demo and preserve service ownership.

---

# 21. Minimum Domain Acceptance Criteria

The domain model is ready for implementation when Kiro can create persistence and APIs without inventing answers to these questions:

- What are the MVP entities? **Defined.**
- Which service owns each entity? **Defined.**
- What are the entity relationships/cardinalities? **Defined.**
- What are the essential properties? **Defined.**
- What status/access/type enums exist? **Defined.**
- What are the lifecycle transitions? **Defined.**
- What validation rules matter? **Defined.**
- Which cross-service references are allowed? **Defined.**
- Which workflow uses SQS? **Defined.**
- How is duplicate message delivery handled? **Defined at the business-rule level.**
- How does credential revocation behave? **Defined.**
- What is explicitly out of scope? **Defined.**
- What constitutes MVP success? **Defined.**

---

# 22. Recommended Next Specification

After this document is approved, the next architecture artifact should be:

> **SecurityOperationsService Detailed Specification**

It should define:

- Entity implementation details for Hospital, Building, Location, SecurityAsset, and SecurityIncident.
- EF Core ownership/mappings.
- PostgreSQL schema/table names and indexes.
- Exact API endpoints and versioning.
- Request and response DTOs.
- Dashboard read models.
- Asset filtering/search behavior.
- Incident commands and queries.
- FluentValidation rules.
- Authorization policies.
- `IncidentCreated` integration-event contract.
- Seed data.
- Unit/integration test scenarios.
- Acceptance criteria.

That specification can then be handed directly to Kiro for implementation while the WorkOrderService specification is prepared in parallel.

---

# 23. Final MVP Domain Map

```text
                            NORTHSTAR MEDICAL CENTER
                                      |
                                   Hospital
                                      |
                                      | 1..*
                                      v
                                   Building
                                      |
                                      | 1..*
                                      v
                                   Location
                                      |
                                      | 1..*
                                      v
                                SecurityAsset
                                      |
                                      | 1..*
                                      v
                              SecurityIncident
                                      |
                                      | 0..1
                   external reference / SQS trigger
                                      |
                                      v
                                  WorkOrder
                                      |
                                      | 0..1 assigned
                                      v
                                  Technician


                                    Person
                                      |
                                      | 1..*
                                      v
                                  Credential
```

Service ownership:

```text
SecurityOperationsService
  Hospital
  Building
  Location
  SecurityAsset
  SecurityIncident

WorkOrderService
  WorkOrder
  Technician
  technician notes (owned/value records)

CredentialService
  Person
  Credential
```

This is the minimum domain model required to build the one-week Vision MVP.
