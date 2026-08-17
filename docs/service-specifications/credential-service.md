# Vision — CredentialService Detailed Specification

**Status:** MVP implementation specification  
**Service:** `CredentialService`  
**Target implementation agent:** Amazon Kiro  
**Target repository location:** `docs/service-specifications/credential-service.md`  
**Depends on:** `README.md`, `docs/technology-specification.md`, `docs/business-domain-specification.md`  
**Scope:** Vision MVP credential-management vertical slice and Phase 5 authorization integration

---

# 1. Purpose

This document defines the detailed implementation contract for Vision's `CredentialService`.

The service supports the credential-management portion of the primary Vision demo:

```text
Open Credential Management
        ↓
Find employee with reported lost badge
        ↓
Open employee / credential detail
        ↓
Revoke credential
        ↓
Credential immediately displays Revoked
```

It also supports the broader MVP capabilities already approved for Credential Management:

```text
browse personnel
view credentials
issue credential
view expiration
view access level
view derived credential status
revoke credential
show expiring-soon count on dashboard
```

The implementation must remain intentionally small.

Do not turn CredentialService into:

```text
an HR system
an identity-provider replacement
a physical badge-controller integration
a generalized access-control policy engine
```

---

# 2. Service Mission

`CredentialService` answers:

> **Who has a physical-access credential, what access level does it carry, when does it expire, and has it been revoked?**

It is authoritative for:

```text
Person records relevant to credential administration
Credential records
credential issuance
credential expiration state
credential revocation state
credential access level
expiring-soon credential read models
```

It is not authoritative for:

```text
authentication identities
Amazon Cognito users
security assets
security incidents
work orders
technicians
hospital HR workflows
door/controller hardware state
fine-grained physical-access rules
```

---

# 3. Source-of-Truth Precedence

If requirements differ, use this order:

```text
1. Approved Business & Domain Specification
2. This CredentialService specification
3. Approved revised service specifications / demo-role decisions
4. Technology Specification
5. README
```

Important approved demo-role refinement:

```text
SecurityManager has CredentialAdministrator capabilities in CredentialService.
```

Keep the separate:

```text
CredentialAdministrator
```

role.

Do not interpret this as permission for Technician credential management.

---

# 4. Service Ownership

CredentialService owns exactly:

```text
Person
Credential
```

Relationship:

```text
Person 1 -------- * Credential
```

`Person` represents someone whose physical-access credentials are administered.

`Credential` represents one issued physical-access badge/credential.

---

# 5. Important Technician Boundary

Do not merge:

```text
CredentialService.Person
```

with:

```text
WorkOrderService.Technician
```

A real employee may conceptually be both.

The MVP does not require synchronization or a shared employee aggregate.

Do not introduce:

```text
Employee
StaffMember
SharedPerson
User
Worker
```

as a shared cross-service business entity.

---

# 6. PostgreSQL Ownership

Use schema:

```text
credentials
```

Business tables:

```text
credentials.people
credentials.credentials
```

CredentialService may share the same physical PostgreSQL/Neon instance as the other services.

It must not directly read or mutate:

```text
security_operations.*
work_orders.*
```

---

# 7. Existing Phase 2 Model

The current repository already contains the approved foundation:

```text
Person
Credential
PersonType
CredentialAccessLevel
CredentialStatus
CredentialDbContext
EF mappings
migration
seed data
```

Kiro should build on that implementation rather than replacing it with a new model.

The current important decisions are correct:

```text
Credential.Status is derived
CredentialStatus is not persisted
Person -> Credential uses Restrict delete behavior
CredentialNumber is unique
EmployeeNumber is unique when supplied
```

---

# 8. Identifier and Time Standards

Use:

```text
Guid
```

for IDs.

PostgreSQL:

```text
uuid
```

Use:

```text
DateTimeOffset
```

for time values.

PostgreSQL:

```text
timestamp with time zone
```

All application-generated timestamps are UTC.

API timestamps serialize as ISO 8601.

---

# 9. Person Entity

Current properties:

| Property | CLR Type | Required |
|---|---|---:|
| `Id` | `Guid` | Yes |
| `FirstName` | `string` | Yes |
| `LastName` | `string` | Yes |
| `PersonType` | `PersonType` | Yes |
| `IsActive` | `bool` | Yes |
| `EmployeeNumber` | `string?` | No |
| `Email` | `string?` | No |
| `Department` | `string?` | No |
| `JobTitle` | `string?` | No |
| `CreatedAt` | `DateTimeOffset` | Yes |
| `UpdatedAt` | `DateTimeOffset?` | No |
| `Credentials` | collection | Yes |

Existing lengths should remain:

```text
FirstName       100
LastName        100
EmployeeNumber   50
Email            254
Department       100
JobTitle         100
PersonType        20 as string
```

---

# 10. PersonType

Exactly:

```text
Employee
Contractor
```

Do not add:

```text
Patient
Visitor
Vendor
Volunteer
Student
Physician
Temporary
```

as new enum values for the MVP.

A contractor may already model an external worker sufficiently for this portfolio story.

---

# 11. Person Validation

Required:

```text
FirstName nonblank
LastName nonblank
PersonType defined enum
```

If `EmployeeNumber` is supplied:

```text
nonblank after trimming
max 50
unique within CredentialService
```

If `Email` is supplied:

```text
max 254
valid email format
```

Optional display fields remain bounded by existing EF lengths.

---

# 12. Person Business Rules

Approved rules:

```text
employees may have credentials
contractors may have credentials
a person may have historical credentials
Person.IsActive models current relationship to the hospital
deactivating Person does not automatically revoke every credential
credential actions remain explicit
```

Do not implement a cascade revocation workflow from `Person.IsActive`.

---

# 13. Person Mutation Scope

The approved MVP capabilities require:

```text
browse people
view person credentials
```

but do not require HR-style person creation or editing.

Therefore the base MVP CredentialService should treat People as read-oriented reference/business records.

Required public mutation APIs do **not** include:

```text
POST /people
PUT /people/{id}
PATCH /people/{id}
DELETE /people/{id}
```

If later requirements explicitly need person administration, add it deliberately.

Do not invent it in this phase.

---

# 14. Credential Entity

Current properties:

| Property | CLR Type | Required |
|---|---|---:|
| `Id` | `Guid` | Yes |
| `PersonId` | `Guid` | Yes |
| `CredentialNumber` | `string` | Yes |
| `AccessLevel` | `CredentialAccessLevel` | Yes |
| `IssuedAt` | `DateTimeOffset` | Yes |
| `ExpiresAt` | `DateTimeOffset` | Yes |
| `RevokedAt` | `DateTimeOffset?` | No |
| `RevocationReason` | `string?` | No |
| `CreatedAt` | `DateTimeOffset` | Yes |
| `UpdatedAt` | `DateTimeOffset?` | No |
| `Status` | derived | Yes |

Current lengths:

```text
CredentialNumber   50
RevocationReason  500
AccessLevel         20 as string
```

Preserve them unless migration evidence justifies change.

---

# 15. CredentialAccessLevel

Exactly:

```text
General
Clinical
Restricted
Security
```

This is intentionally coarse-grained.

The enum demonstrates physical-access classification.

It is not a permission-rule engine.

Do not model:

```text
individual doors
access zones
time schedules
floor permissions
per-building policies
clearance matrices
RBAC for doors
```

inside this enum.

---

# 16. CredentialStatus

Expose:

```text
Active
Expired
Revoked
```

Status is derived.

Do not persist a redundant `Status` column.

Approved derivation:

```text
if RevokedAt != null
    => Revoked

else if ExpiresAt <= now
    => Expired

else
    => Active
```

Revocation has precedence over expiration.

A credential that is both past expiration and revoked displays:

```text
Revoked
```

---

# 17. Derived Status and Querying

Do not query the ignored `Status` CLR property through EF.

Translate status filters into persisted fields.

At a single request-scoped `now`:

## Active

```text
RevokedAt == null
AND ExpiresAt > now
```

## Expired

```text
RevokedAt == null
AND ExpiresAt <= now
```

## Revoked

```text
RevokedAt != null
```

Use one captured UTC time per query so rows are evaluated consistently.

---

# 18. Expiring Soon Definition

“Expiring soon” is a read-model concept.

Use:

```text
30 days
```

as the approved MVP threshold.

A credential is expiring soon when:

```text
RevokedAt == null
AND ExpiresAt > now
AND ExpiresAt <= now + 30 days
```

Expired credentials are not “expiring soon.”

Revoked credentials are not “expiring soon.”

Define the threshold in one named location, for example:

```text
CredentialPolicy.ExpiringSoonDays = 30
```

or configuration with a default of 30.

Do not scatter `30` throughout handlers/frontend.

---

# 19. Credential Validation

On issuance:

```text
Person exists
CredentialNumber required
CredentialNumber max 50
CredentialNumber unique
AccessLevel is defined
ExpiresAt > IssuedAt
```

Server-generated issuance should ensure:

```text
IssuedAt = now UTC
CreatedAt = now UTC
UpdatedAt = null initially
RevokedAt = null
RevocationReason = null
```

---

# 20. Issuing To Inactive Person

The source material does not explicitly prohibit credentials on an inactive Person.

For a coherent MVP business rule, do not issue a **new** credential to:

```text
Person.IsActive == false
```

Return:

```text
409 Conflict
```

Existing historical credentials remain visible.

This rule aligns issuance with the meaning of `IsActive` while preserving explicit revocation behavior.

Do not automatically revoke historical credentials merely because the Person is inactive.

---

# 21. Credential Number Uniqueness

`CredentialNumber` must be unique within CredentialService.

Database constraint remains authoritative.

Application validation should provide a friendly conflict response when practical.

Concurrent duplicate issuance must still be protected by the unique database index.

Duplicate number:

```text
409 Conflict
```

Do not silently generate a different number when the caller supplied one.

---

# 22. Issuance Timestamps

For the MVP issuance endpoint:

```text
IssuedAt = server current UTC time
```

The client supplies:

```text
ExpiresAt
```

and:

```text
ExpiresAt > IssuedAt
```

The API does not need arbitrary historical issuance/backdating.

This keeps the mutation small and audit-friendly.

---

# 23. Credential Revocation

Current domain operation:

```text
Credential.Revoke(reason)
```

is the right aggregate behavior.

Rules:

```text
reason required
reason max 500
RevokedAt set to now UTC
RevocationReason stored
UpdatedAt set
```

Revocation is terminal for the MVP.

---

# 24. Idempotent Revocation

Revoking an already revoked credential is idempotent.

Required behavior:

```text
status remains Revoked
original RevokedAt preserved
original RevocationReason preserved
no duplicate side effect
```

A second request with a different reason must **not** overwrite historical revocation information.

Return success with the current revoked representation.

Recommended HTTP result:

```text
200 OK
```

This makes retries safe.

---

# 25. Expired Credential Revocation

An expired credential may still be explicitly revoked for administrative/history purposes.

Before revocation:

```text
Expired
```

After revocation:

```text
Revoked
```

because revocation takes status precedence.

This is compatible with the approved derived-state rules.

---

# 26. Physical Badge Boundary

Revocation means:

```text
CredentialService business/application state is revoked
```

It does not mean Vision directly communicates with:

```text
door controllers
badge encoders
physical PACS hardware
reader firmware
```

Do not claim hardware deactivation.

The UI may state:

```text
Revoked in Vision
```

or simply:

```text
Revoked
```

without suggesting direct hardware integration.

---

# 27. Deletion

Do not expose deletion APIs for:

```text
Person
Credential
```

Historical credential records are valuable.

Use status/revocation rather than deletion.

Do not cascade-delete credentials.

Existing:

```text
DeleteBehavior.Restrict
```

is appropriate.

---

# 28. EF Core Indexes

Preserve current indexes:

## Person

```text
UNIQUE employee_number WHERE employee_number IS NOT NULL
is_active
(last_name, first_name)
```

## Credential

```text
UNIQUE credential_number
person_id
expires_at
revoked_at
```

Recommended combined/index refinement only if query measurement requires it:

```text
(revoked_at, expires_at)
```

Do not over-index for demo-scale data.

---

# 29. API Base

Use:

```text
/api/v1
```

Content types:

```text
application/json
application/problem+json
```

Use the same error-handling conventions established in SecurityOperationsService.

---

# 30. Pagination

For list endpoints:

```text
page default = 1
pageSize default = 25
pageSize min = 1
pageSize max = 100
```

Response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

Reuse the project `PagedList<T>` pattern where sensible without creating cross-service project references that violate boundaries.

A small duplicated common shape is acceptable.

---

# 31. Required Endpoints

Implement:

```text
GET  /api/v1/people
GET  /api/v1/people/{id}

GET  /api/v1/credentials
GET  /api/v1/credentials/{id}

POST /api/v1/people/{personId}/credentials
POST /api/v1/credentials/{id}/revoke

GET  /api/v1/credentials/summary
```

No generic Credential PATCH/PUT is required.

No Person mutation endpoint is required.

---

# 32. GET /api/v1/people

Purpose:

```text
browse credential-relevant personnel
find lost-badge employee
```

Query parameters:

```text
personType
isActive
department
search
page
pageSize
```

Recommended defaults:

```text
no PersonType filter
no IsActive filter
LastName ASC, FirstName ASC
```

Do not hide inactive people by default because historical credentials remain useful.

---

# 33. Person Search

Search case-insensitively across:

```text
FirstName
LastName
EmployeeNumber
Email
Department
JobTitle
```

Use PostgreSQL `ILIKE`.

Search runs in PostgreSQL, not in memory.

---

# 34. Person List DTO

Return enough data to find a person quickly:

```text
Id
FirstName
LastName
DisplayName
PersonType
IsActive
EmployeeNumber
Email
Department
JobTitle

CredentialSummary:
    activeCount
    expiringSoonCount
    revokedCount
```

A lighter representation that includes one primary/current credential may also be acceptable, but do not load every historical credential record into every person list row.

Prefer projection/aggregate counts.

---

# 35. Person DisplayName

DTOs may expose:

```text
DisplayName = FirstName + " " + LastName
```

Do not persist `DisplayName` redundantly.

---

# 36. GET /api/v1/people/{id}

Return:

```text
Id
FirstName
LastName
DisplayName
PersonType
IsActive
EmployeeNumber
Email
Department
JobTitle
CreatedAt
UpdatedAt

Credentials[]
```

Credentials should include:

```text
Id
CredentialNumber
AccessLevel
Status
IssuedAt
ExpiresAt
IsExpiringSoon
RevokedAt
RevocationReason
```

Recommended ordering:

```text
active/revoked recent relevance is less important than chronology;
use IssuedAt DESC
```

A revoked lost badge should remain visible.

Unknown person:

```text
404
```

---

# 37. GET /api/v1/credentials

Purpose:

```text
credential inventory
status/expiration management
dashboard/drilldown support
```

Query parameters:

```text
status
accessLevel
personId
expiringSoon
search
page
pageSize
```

Recommended default sort:

```text
ExpiresAt ASC
```

with revoked/historical rows still represented correctly.

Alternatively:

```text
CreatedAt DESC
```

is acceptable if frontend views prioritize recent issuance.

Pick one and keep API/UI consistent.

For Credential Management operations, `ExpiresAt ASC` is preferred because approaching expiration is operationally useful.

---

# 38. Credential Search

Case-insensitive search across:

```text
CredentialNumber
Person.FirstName
Person.LastName
Person.EmployeeNumber
Person.Department
```

Use database-side `ILIKE`.

---

# 39. Credential Status Filter

Accept only:

```text
Active
Expired
Revoked
```

Reject invalid values:

```text
400
```

Validation must use:

```text
Enum.TryParse
AND
Enum.IsDefined
```

to reject undefined numeric enum inputs.

---

# 40. Access Level Filter

Accept only:

```text
General
Clinical
Restricted
Security
```

Reject invalid/undefined values:

```text
400
```

---

# 41. expiringSoon Filter

Support:

```text
expiringSoon=true
```

which applies the approved 30-day rule.

When `expiringSoon=false` is supplied, either:

```text
do not apply the expiring filter
```

or explicitly exclude expiring rows.

For simplicity, recommended semantics:

```text
true -> only expiring soon
false/omitted -> no expiring-soon filter
```

Document it in OpenAPI.

---

# 42. Credential List DTO

Return:

```text
Id
CredentialNumber
AccessLevel
Status
IssuedAt
ExpiresAt
IsExpiringSoon
RevokedAt

Person:
    Id
    DisplayName
    PersonType
    IsActive
    EmployeeNumber
    Department
    JobTitle
```

Do not return `RevocationReason` in every list row unless the UI displays it.

Detail should contain the full reason.

---

# 43. GET /api/v1/credentials/{id}

Return:

```text
Id
CredentialNumber
AccessLevel
Status
IssuedAt
ExpiresAt
IsExpiringSoon
RevokedAt
RevocationReason
CreatedAt
UpdatedAt

Person:
    Id
    FirstName
    LastName
    DisplayName
    PersonType
    IsActive
    EmployeeNumber
    Email
    Department
    JobTitle
```

Unknown credential:

```text
404
```

---

# 44. POST /api/v1/people/{personId}/credentials

Purpose:

```text
issue a new credential to an existing Person
```

Authorized later:

```text
SecurityManager
CredentialAdministrator
```

Request:

```json
{
  "credentialNumber": "NMC-00020",
  "accessLevel": "Clinical",
  "expiresAt": "2027-08-10T21:00:00Z"
}
```

Server controls:

```text
Id
PersonId from route
IssuedAt
CreatedAt
UpdatedAt
RevokedAt
RevocationReason
Status
```

---

# 45. Issuance Success

Valid request:

```text
201 Created
```

Persist:

```text
new Guid
PersonId
CredentialNumber
AccessLevel
IssuedAt = now
ExpiresAt
CreatedAt = now
```

Derived status should immediately be:

```text
Active
```

because issuance validation requires future expiration.

Return the created credential representation.

Use a Location header where practical.

---

# 46. Issuance Errors

Unknown Person:

```text
404
```

Inactive Person:

```text
409
```

Duplicate credential number:

```text
409
```

Blank/invalid credential number:

```text
400
```

Invalid access level:

```text
400
```

Expiration not after issuance:

```text
400
```

---

# 47. POST /api/v1/credentials/{id}/revoke

Purpose:

```text
explicit credential revocation
```

Authorized later:

```text
SecurityManager
CredentialAdministrator
```

Request:

```json
{
  "reason": "Badge reported lost"
}
```

Recommended property name:

```text
reason
```

Domain property remains:

```text
RevocationReason
```

---

# 48. Revoke Success

For active or expired credential:

```text
200 OK
```

Then:

```text
Status = Revoked
RevokedAt populated
RevocationReason stored
UpdatedAt populated
```

Return updated Credential detail.

---

# 49. Revoke Idempotency

For already revoked credential:

```text
POST /credentials/{id}/revoke
```

returns:

```text
200 OK
```

with the existing revoked state.

Verify:

```text
original RevokedAt unchanged
original RevocationReason unchanged
```

Do not return 409 simply because it is already revoked.

Safe retries are intentional.

---

# 50. Revoke Errors

Unknown Credential:

```text
404
```

Blank/whitespace reason:

```text
400
```

Reason > 500:

```text
400
```

Unauthorized role once Cognito exists:

```text
403
```

Unauthenticated:

```text
401
```

---

# 51. GET /api/v1/credentials/summary

Purpose:

```text
dashboard composition
```

CredentialService owns this value.

Recommended response:

```json
{
  "activeCount": 15,
  "expiringSoonCount": 2,
  "expiredCount": 1,
  "revokedCount": 1
}
```

The main dashboard needs at least:

```text
expiringSoonCount
```

Additional small counts are useful for the Credential Management page and cost little.

---

# 52. Summary Rules

At captured UTC `now`:

## activeCount

```text
RevokedAt == null
AND ExpiresAt > now
```

This includes credentials that are expiring soon.

## expiringSoonCount

```text
RevokedAt == null
AND ExpiresAt > now
AND ExpiresAt <= now + 30 days
```

## expiredCount

```text
RevokedAt == null
AND ExpiresAt <= now
```

## revokedCount

```text
RevokedAt != null
```

Do not query another service.

---

# 53. Dashboard Composition

The Next.js dashboard composes:

```text
SecurityOperationsService
    security health / incidents / alerts

WorkOrderService
    open work-order count

CredentialService
    expiring-soon credential count
```

Do not introduce:

```text
DashboardService
cross-schema SQL joins
```

CredentialService returns only Credential-owned summary data.

---

# 54. Application Layer

Suggested organization:

```text
Application/
├── People/
│   └── Queries/
│       ├── GetPeople
│       └── GetPersonById
│
├── Credentials/
│   ├── Commands/
│   │   ├── IssueCredential
│   │   └── RevokeCredential
│   └── Queries/
│       ├── GetCredentials
│       ├── GetCredentialById
│       └── GetCredentialSummary
│
└── Common/
```

Equivalent repository-consistent organization is acceptable.

---

# 55. MediatR

Recommended requests:

```text
GetPeopleQuery
GetPersonByIdQuery

GetCredentialsQuery
GetCredentialByIdQuery
GetCredentialSummaryQuery

IssueCredentialCommand
RevokeCredentialCommand
```

MediatR organizes application behavior.

Do not create a custom command/query bus around it.

---

# 56. FluentValidation

Recommended validators:

```text
GetPeopleQueryValidator
GetCredentialsQueryValidator
IssueCredentialCommandValidator
RevokeCredentialCommandValidator
```

Validate:

```text
pagination
enum values
credential number
expiration
reason length
```

Person existence, active state, and uniqueness require handler/database checks rather than pure request-shape validation.

---

# 57. Enum Validation

Follow the corrected Phase 3 pattern.

Use:

```text
TryParse
AND
IsDefined
```

Do not rely on `Enum.TryParse` alone.

Apply to:

```text
PersonType
CredentialAccessLevel
CredentialStatus
```

---

# 58. Domain/Application Responsibility

Domain owns:

```text
credential status derivation
revocation behavior
idempotent revoke
```

Application handler owns:

```text
load Person/Credential
check Person active for issuance
check duplicate credential number
create credential
invoke Revoke
save
map DTO
```

Endpoint owns:

```text
HTTP binding
HTTP status
authorization policy attachment
```

Keep endpoints thin.

---

# 59. Clock Considerations

The current `Credential.Status` uses:

```text
DateTimeOffset.UtcNow
```

This is acceptable for the MVP.

However, query handlers should capture:

```text
var now = DateTimeOffset.UtcNow;
```

once per query.

If tests become brittle around time, an injectable `TimeProvider` is preferred over creating a custom clock framework.

Modern .NET `TimeProvider` may be used if already compatible with the target runtime.

Do not force a large abstraction.

---

# 60. Query Efficiency

Use:

```text
AsNoTracking()
server-side filtering
server-side pagination
projection
```

Do not:

```text
Include all Credentials for every Person list row
load entire tables then derive status in memory
```

For credential status filtering, translate derived status into SQL predicates.

---

# 61. CancellationToken

Propagate:

```text
HTTP
 ↓
MediatR
 ↓
handler
 ↓
EF Core
```

Use cancellation-aware async calls.

Do not use:

```text
.Result
.Wait()
GetAwaiter().GetResult()
```

---

# 62. Error Handling

Use ASP.NET Core Problem Details conventions consistent with SecurityOperationsService.

Expected:

```text
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
500 Internal Server Error
```

Do not expose:

```text
stack traces
database connection strings
SQL internals
JWTs
AWS credentials
```

---

# 63. CORS

CredentialService must use the same configuration-driven frontend CORS pattern established in SecurityOperationsService.

Development:

```text
http://localhost:3000
```

Production:

```text
configured deployed frontend origin
```

Do not use unrestricted production CORS.

---

# 64. OpenAPI

OpenAPI should accurately document:

```text
People endpoints
Credential endpoints
filters
pagination
enum values
request DTOs
response DTOs
status codes
```

Use concrete response types rather than generic `object`.

This is part of the portfolio story.

---

# 65. Authentication

Phase 5 uses:

```text
Amazon Cognito
OAuth/OIDC
JWT bearer authentication
ASP.NET Core authorization policies
```

Do not add CredentialService-specific user/password storage.

Cognito identities are authentication identities.

`Person` is a business record.

They do not need to be the same entity.

---

# 66. Authorization Matrix

Once Cognito is enabled:

| Capability | SecurityManager | CredentialAdministrator | Technician |
|---|:---:|:---:|:---:|
| Browse People | Yes | Yes | No |
| View Person detail | Yes | Yes | No |
| Browse Credentials | Yes | Yes | No |
| View Credential detail | Yes | Yes | No |
| View Credential summary | Yes | Yes | No |
| Issue Credential | Yes | Yes | No |
| Revoke Credential | Yes | Yes | No |

This reflects the approved demo requirement:

```text
SecurityManager can perform all credential-administration activities needed for the MVP.
```

The separate CredentialAdministrator role remains meaningful and receives the same CredentialService capability set.

---

# 67. Security Manager Demo Role

The primary employer-facing demo user logs in as:

```text
SecurityManager
```

That user must be able to:

```text
open Credential Management
find Michael Brown
view the lost-badge credential
revoke it
see Revoked immediately
```

Do not require switching user accounts in the main five-minute demo.

---

# 68. Technician Restriction

Technician has no CredentialService business capability in the MVP.

Once authorization is enabled:

```text
Technician -> 403
```

for CredentialService protected reads and mutations.

Do not expose personnel/credential inventory merely because the user is authenticated.

---

# 69. Pre-Cognito Phase

Before Cognito implementation:

```text
do not fake roles
do not build local user tables
do not hard-code "SecurityManager" inside handlers
```

Keep endpoint boundaries ready for authorization policies later.

---

# 70. Frontend Routes

Recommended:

```text
/credentials
/credentials/[id]
/people/[id]
```

The main navigation label can be:

```text
Credential Management
```

A separate `/people` route is optional.

For MVP simplicity, `/credentials` may lead with people/credential search and drill into Person detail.

---

# 71. Credential Management Landing Page

The page should make these operational questions easy:

```text
Who has credentials?
Which expire soon?
Which are expired?
Which are revoked?
Can I find a lost badge quickly?
```

Recommended components:

```text
summary cards
search
status filter
access-level filter
credential/person result table
```

Do not overbuild analytics.

---

# 72. Credential Summary UI

Useful cards:

```text
Active
Expiring Soon
Expired
Revoked
```

The global dashboard only needs:

```text
Expiring Soon
```

The Credential Management page may show all four.

---

# 73. People Browsing UX

People results should show:

```text
name
employee/contractor
employee number
department
job title
active/inactive relationship state
credential status summary
```

The reviewer should not need to interpret UUIDs.

---

# 74. Person Detail UX

Show:

```text
name
person type
employee number
department
job title
email
relationship status

Credentials
    credential number
    access level
    status
    issued
    expires
    revoked details when relevant
```

Include:

```text
Issue Credential
```

for authorized users.

---

# 75. Credential Status Presentation

Status must be obvious and not rely solely on color.

Use text:

```text
Active
Expiring Soon
Expired
Revoked
```

Important distinction:

`Expiring Soon` is a presentation/read-model badge, not a fourth `CredentialStatus`.

A credential that is expiring soon still has:

```text
Status = Active
IsExpiringSoon = true
```

---

# 76. Lost Badge Demo

Seed landmark:

```text
Person: Michael Brown
Department: Surgery
Job Title: Surgical Technician
Credential: CredentialLostBadge
Initial status: Active
```

The frontend should make this credential easy to locate.

The exact reason used in the demo may be:

```text
Badge reported lost
```

After revocation:

```text
Status = Revoked
RevokedAt visible
RevocationReason visible
Revoke action disabled/removed
```

---

# 77. Revoke UX

Before mutation, the UI should make the destructive/terminal nature clear.

A simple confirmation dialog is appropriate:

```text
Revoke NMC-00006 for Michael Brown?
Reason: [Badge reported lost]
```

Do not require elaborate approval workflow.

After success:

```text
refresh/update detail
show Revoked immediately
```

---

# 78. Issue Credential UX

Form:

```text
Credential Number
Access Level
Expiration Date
```

Person comes from context.

Server determines issuance time.

Show validation errors clearly.

No hardware encoding step.

---

# 79. Loading/Error/Empty States

Credential screens must intentionally handle:

```text
loading
success
empty
error
```

Examples:

```text
no credentials matching filter
no expiring credentials
person not found
duplicate credential number
revocation failed
```

Do not show indefinite spinners.

---

# 80. Frontend Types

Use explicit TypeScript contracts.

Do not use:

```text
any
```

for Credential API data.

Wire values exactly:

```text
Employee
Contractor

General
Clinical
Restricted
Security

Active
Expired
Revoked
```

`Expiring Soon` is UI copy, not a wire status.

---

# 81. Seed Data

The existing Phase 2 seed is strong and should be preserved.

It currently includes approximately:

```text
18 People
employees + contractors
active + inactive people
multiple departments/job titles
active credentials
expiring-soon credentials
expired credential
historically revoked credential
lost-badge demo credential
```

Do not replace it with generic Person 1 / Badge 1 seed data.

---

# 82. Required Seed Landmark

Preserve deterministic IDs for:

```text
PersonMichaelBrown
CredentialLostBadge
```

The primary lost-badge demo must not start revoked.

Initial state:

```text
Michael Brown credential
RevokedAt == null
ExpiresAt in future
Status == Active
```

---

# 83. Expiring Seed Landmarks

Existing:

```text
Sophia Adams credential ~20 days
Jason Clark credential ~10 days
```

provide expiring-soon examples.

These should count in:

```text
expiringSoonCount
```

provided they remain unrevoked and future-dated.

---

# 84. Expired and Revoked Seeds

Keep at least:

```text
one Expired credential
one Revoked credential
```

so UI/status filtering is immediately demonstrable.

---

# 85. Seed Idempotency

Seed reruns must not duplicate:

```text
People
Credentials
```

Existing early-return seeding is acceptable for the stable demo database if it remains deterministic enough for current project use.

For tests, verify repeated seed execution does not add duplicates.

---

# 86. Current Seed GUID Note

Some credentials created through `Guid.NewGuid()` are not deterministic while the landmark lost-badge credential is deterministic.

This is acceptable because the current seeder exits when People exist.

Do not redesign the entire seed merely to make every historical credential ID deterministic unless tests require it.

Protect the important demo IDs.

---

# 87. Logging

Use structured logging.

Examples:

```text
Issuing credential {CredentialNumber} to person {PersonId}

Credential {CredentialId} issued to person {PersonId}

Revoking credential {CredentialId} for person {PersonId}

Credential {CredentialId} revoked

Credential {CredentialId} already revoked; idempotent request accepted
```

Do not log sensitive auth tokens.

Credential numbers are business identifiers and may appear where operationally useful, but avoid unnecessary bulk logging.

---

# 88. No Credential Messaging Requirement

The MVP does not require CredentialService to publish integration events.

Do not add:

```text
CredentialRevoked event
CredentialIssued event
SNS
SQS
EventBridge
```

just to make the service look distributed.

The one meaningful asynchronous workflow already exists between SecurityOperations and WorkOrder.

Credential revocation is synchronous.

---

# 89. No Hardware Integration

Do not add:

```text
badge printer API
door controller API
PACS vendor API
reader provisioning
physical card encoding
```

The portfolio value comes from clean business/API/security behavior, not simulated hardware complexity.

---

# 90. No Cross-Service API Dependency

CredentialService does not need to call:

```text
SecurityOperationsService
WorkOrderService
```

for its core behavior.

Its data is self-contained.

The frontend composes summary data from service APIs.

---

# 91. Domain Unit Tests — Status

Required:

## Active

```text
RevokedAt null
ExpiresAt > now
=> Active
```

## Expired

```text
RevokedAt null
ExpiresAt <= now
=> Expired
```

## Revoked

```text
RevokedAt nonnull
=> Revoked
```

Revoked wins even when expiration is past.

---

# 92. Domain Unit Tests — Revocation

Valid reason:

```text
RevokedAt populated
RevocationReason stored
UpdatedAt populated
Status Revoked
```

Blank reason:

```text
rejected
```

---

# 93. Domain Unit Tests — Idempotent Revoke

Given already revoked Credential:

```text
original RevokedAt = T1
original reason = R1
```

When revoked again with R2:

```text
RevokedAt remains T1
RevocationReason remains R1
Status remains Revoked
```

---

# 94. Query Tests — People

Test:

```text
list returns People
pagination
PersonType filter
IsActive filter
department filter
search by name
search by employee number
search by department
default name sort
unknown detail -> 404
person detail returns credentials
```

---

# 95. Query Tests — Credentials

Test:

```text
list works
status Active filter
status Expired filter
status Revoked filter
accessLevel filter
personId filter
expiringSoon filter
search credential number
search person name
search employee number
pagination
detail
unknown -> 404
```

---

# 96. Query Tests — Expiring Soon

Use a fixed/captured `now`.

Verify:

```text
ExpiresAt = now + 10 days -> expiring soon
ExpiresAt = now + 30 days -> expiring soon
ExpiresAt > now + 30 days -> not expiring soon
ExpiresAt <= now -> not expiring soon
RevokedAt != null -> not expiring soon
```

Avoid test flakiness around milliseconds.

---

# 97. API Tests — Issue Credential

Valid issuance:

```text
201
PersonId correct
CredentialNumber correct
AccessLevel correct
IssuedAt server-generated
ExpiresAt correct
Status Active
```

Reject:

```text
unknown Person -> 404
inactive Person -> 409
blank number -> 400
duplicate number -> 409
invalid access level -> 400
expiration <= issuance -> 400
```

---

# 98. API Tests — Revoke

Valid active Credential:

```text
200
Status Revoked
RevokedAt populated
reason visible
```

Expired Credential:

```text
200
becomes Revoked
```

Unknown:

```text
404
```

Blank reason:

```text
400
```

---

# 99. API Tests — Idempotent Revoke

First revoke:

```text
200
RevokedAt = T1
Reason = R1
```

Second revoke:

```text
200
RevokedAt still T1
Reason still R1
```

No duplicate side effect.

---

# 100. Persistence Tests

Verify:

```text
credentials schema
People table
Credentials table
CredentialNumber unique
EmployeeNumber unique when nonnull
multiple null EmployeeNumber values allowed
Person deletion restricted while Credentials exist
Credential.Status not persisted
enum strings readable
```

---

# 101. Summary Tests

Seed/query known combinations and verify:

```text
activeCount
expiringSoonCount
expiredCount
revokedCount
```

Important:

```text
expiringSoon is subset of active
```

Do not subtract expiring soon from active count unless UI explicitly requests mutually exclusive counts.

---

# 102. Authorization Tests — Phase 5

SecurityManager:

```text
can browse People
can browse Credentials
can view detail
can issue
can revoke
can view summary
```

CredentialAdministrator:

```text
same CredentialService capabilities
```

Technician:

```text
cannot browse/manage credentials
```

Unauthenticated protected routes:

```text
401
```

Wrong authenticated role:

```text
403
```

Backend enforcement required.

---

# 103. Security Tests

Verify:

```text
invalid enum numeric values rejected
overlength reason rejected
duplicate credential number conflict
no secrets in errors
no token logging
no unrestricted production CORS
```

---

# 104. Demo Acceptance

Primary scenario:

```text
1. Navigate to Credential Management.
2. Search Michael Brown.
3. Open Michael Brown.
4. See active lost-badge credential.
5. Click Revoke.
6. Enter "Badge reported lost".
7. Confirm.
8. API succeeds.
9. Credential now reads Revoked.
10. RevokedAt and reason are visible.
11. Refresh page.
12. Credential remains Revoked.
```

A second revoke attempt, if invoked directly/API, must remain safe and preserve original revocation data.

---

# 105. Performance

Expected dataset is small, but implementation should still demonstrate good habits:

```text
AsNoTracking
projection
bounded pagination
database-side filters
indexes already aligned with major queries
```

No cache is required.

Do not add Redis.

---

# 106. Health Endpoint

Preserve:

```text
GET /health
```

Return:

```text
200
CredentialService identity
```

Later readiness may include database dependency.

Liveness should not become overcomplicated.

---

# 107. Migration Strategy

Build on the existing Credential migration.

If implementation adds no persistence fields, avoid unnecessary migration churn.

If a real schema change becomes necessary:

```text
create a new migration
```

Do not edit an already-applied migration casually once shared environments rely on it.

---

# 108. Explicit Non-Goals — Person / HR

Do not implement:

```text
employee onboarding
employee termination workflow
payroll
manager hierarchy
organizational chart
HR synchronization
department administration
job-title administration
person deletion
person profile editing
```

unless later explicitly approved.

---

# 109. Explicit Non-Goals — Credentials

Do not implement:

```text
PIN codes
biometrics
mobile credentials
credential replacement workflow
credential suspension
credential reactivation
multi-step approvals
badge printing
badge inventory
credential templates
access schedules
door groups
access zones
clearance hierarchy
credential transfer
bulk revocation
```

---

# 110. Explicit Non-Goals — Architecture

Do not introduce:

```text
another microservice
another database
Kafka
SQS for credentials
EventBridge
Lambda
GraphQL
Redis
event sourcing
workflow engine
generic repository framework
shared Person domain assembly
```

---

# 111. Suggested Implementation Sequence

## Slice 1 — API/Application Foundation

1. add MediatR
2. add FluentValidation
3. add Problem Details/global exception handling consistent with SecurityOperations
4. configure CORS
5. organize endpoint mappings under `/api/v1`

## Slice 2 — People Reads

6. People DTOs
7. `GetPeopleQuery`
8. filters/search/pagination
9. `GetPersonByIdQuery`
10. person detail credentials
11. tests

## Slice 3 — Credential Reads

12. Credential DTOs
13. derived SQL status predicates
14. `GetCredentialsQuery`
15. filters/search/pagination
16. `GetCredentialByIdQuery`
17. tests

## Slice 4 — Summary

18. `GetCredentialSummaryQuery`
19. 30-day expiring-soon constant
20. summary endpoint
21. dashboard frontend composition

## Slice 5 — Issuance

22. `IssueCredentialCommand`
23. validator
24. person active/existence check
25. uniqueness handling
26. endpoint
27. tests

## Slice 6 — Revocation

28. `RevokeCredentialCommand`
29. validator
30. domain `Revoke` invocation
31. idempotent response
32. endpoint
33. tests

## Slice 7 — Frontend

34. typed API client
35. Credential Management landing
36. people/credential search
37. Person detail
38. Issue form
39. Revoke confirmation
40. lost-badge demo flow
41. loading/error/empty states

## Slice 8 — Phase 5 Authorization

42. Cognito bearer authentication
43. CredentialAdmin policy
44. SecurityManager OR CredentialAdministrator access
45. Technician denial
46. authorization tests

---

# 112. Things Kiro Must Not Invent

Do not add:

```text
AccessZone
DoorPermission
CredentialPolicy entity
CredentialHistory entity
BadgeDevice entity
BadgePrinter entity
Approval entity
Employee aggregate
Department entity
RoleAssignment entity
AccessSchedule entity
```

The MVP domain remains:

```text
Person
    |
    +-- Credentials
```

with coarse access levels and derived status.

---

# 113. Current Code Adjustments Expected

The current Credential skeleton should be evolved, not rewritten.

Expected changes include:

```text
Program.cs
    add MediatR
    validators
    Problem Details
    CORS
    /api/v1 endpoints

Application/
    replace .gitkeep placeholders with feature code

Credential.cs
    retain derived Status
    retain idempotent Revoke
    potentially refine timestamp injection only if tests justify

Persistence
    retain current schema/mappings
    no unnecessary migration unless model changes

Seeder
    retain realistic Northstar people/credentials
    protect lost-badge landmark
```

---

# 114. CredentialStatus Implementation Note

The current `Status` property computes against:

```text
DateTimeOffset.UtcNow
```

Keep it non-persisted.

DTO projection queries should not call that property inside EF SQL translation.

Instead project persisted fields and compute status:

```text
with SQL-translatable conditional expressions
```

or after projecting only the bounded page.

For list filters, always filter server-side using persisted expressions.

Avoid loading all rows to call `Status`.

---

# 115. Revocation Timestamp Consistency

Current `Revoke` calls `UtcNow` separately for:

```text
RevokedAt
UpdatedAt
```

Those may differ by tiny amounts.

That is not a business problem.

If desired, refine to one captured timestamp:

```text
var now = DateTimeOffset.UtcNow;
RevokedAt = now;
UpdatedAt = now;
```

This improves testability/readability without changing behavior.

---

# 116. Credential Issuance Domain Method

A static factory or constructor may improve invariants, but is not mandatory.

Do not refactor only for pattern purity.

If introduced, it should ensure:

```text
nonempty PersonId
nonblank CredentialNumber
future ExpiresAt relative to IssuedAt
valid access level
```

Application validation still provides friendly errors.

---

# 117. Credential Number Case

Treat CredentialNumber as a human-readable identifier.

Existing uniqueness is database default case-sensitive behavior unless configured otherwise.

The source requirements do not specify case-insensitive uniqueness.

Do not add CITEXT or normalization solely for this MVP.

Frontend should use uppercase seeded convention:

```text
NMC-00001
```

---

# 118. EmployeeNumber Interpretation

The name `EmployeeNumber` is retained even for seeded contractors using values such as:

```text
CTR-001
```

Do not rename the database/domain property during this phase unless a real requirement justifies migration churn.

UI may label it more generically:

```text
Personnel ID
```

if desired.

---

# 119. Credential Expiration Boundary

Status rule is exact:

```text
ExpiresAt <= now => Expired
```

Therefore at the instant of `ExpiresAt`, the credential is expired.

Use this exact rule consistently in:

```text
detail DTO
list DTO
status filters
summary counts
expiring-soon calculations
frontend
tests
```

---

# 120. Dashboard Count Timing

Because expiration is time-derived, dashboard counts can change without a database write.

That is expected.

Do not persist status just to trigger a change.

Each request computes against current UTC time.

---

# 121. No Background Expiration Job

Do not create:

```text
hosted service
cron job
scheduled Lambda
database update job
```

to mark credentials Expired.

Expiration is derived from:

```text
ExpiresAt
```

No persistence mutation is required.

---

# 122. Revocation Audit Sufficiency

For the MVP, these fields are the credential revocation record:

```text
RevokedAt
RevocationReason
UpdatedAt
```

Do not add a separate CredentialAuditEvent entity.

Once Cognito exists, if actor information is easy to record without broad redesign, it may be considered later.

It is not required by the current approved domain model.

---

# 123. Future Actor Attribution

If Phase 5 security review decides that revocation actor attribution is necessary, prefer a small explicit field such as:

```text
RevokedBySubject
```

only after approval.

Do not preemptively add it now because it is absent from the approved domain model.

---

# 124. OpenAPI Examples

Useful credential issuance example:

```json
{
  "credentialNumber": "NMC-00020",
  "accessLevel": "Restricted",
  "expiresAt": "2027-02-10T21:00:00Z"
}
```

Useful revoke example:

```json
{
  "reason": "Badge reported lost"
}
```

Useful filter example:

```text
GET /api/v1/credentials?status=Active&expiringSoon=true&page=1&pageSize=25
```

---

# 125. Problem Details Examples

Duplicate credential:

```json
{
  "title": "Credential number already exists",
  "status": 409,
  "detail": "Credential number NMC-00020 is already in use."
}
```

Inactive person:

```json
{
  "title": "Person is inactive",
  "status": 409,
  "detail": "A new credential cannot be issued to an inactive person."
}
```

Do not leak persistence exception text.

---

# 126. Accessibility / UX

Status indicators should include text, not just color.

Forms should have labels.

Confirmation dialog should identify:

```text
person
credential number
terminal revoke action
```

Keyboard navigation and focus behavior should remain reasonable using the existing frontend approach.

Do not introduce a UI framework solely for the credential slice unless already selected.

---

# 127. Portfolio Narrative

The CredentialService should support a concise explanation:

> Vision separates business credential state from authentication identity. CredentialService owns personnel records relevant to physical access and derives credential status from expiration and revocation data. Revocation is an explicit, terminal, idempotent business operation, while expiration is derived dynamically rather than maintained by background jobs.

The implementation should visibly support that explanation.

---

# 128. ChatGPT Review Checklist

After Kiro implements CredentialService, review:

## Domain

```text
Status derived correctly
Revoked precedence
idempotent revoke
original revoke timestamp/reason preserved
no reactivation
```

## Persistence

```text
credentials schema only
unique credential number
unique optional employee number
Status not persisted
Restrict Person/Credential delete
```

## API

```text
/api/v1
People read endpoints
Credential read endpoints
issuance
revocation
summary
pagination
filters
Problem Details
```

## Query behavior

```text
server-side status predicates
30-day expiring-soon rule
AsNoTracking
no all-table memory filtering
```

## Security

```text
SecurityManager credential admin
CredentialAdministrator credential admin
Technician denied
no fake auth before Cognito
```

## Frontend

```text
lost badge easy to find
status clear
revoke clear
idempotent result stable
typed contracts
loading/error states
```

## Scope

```text
no HR system
no hardware integration
no messaging added
no access-policy engine
```

---

# 129. Definition of Done

CredentialService is MVP-ready when:

```text
✓ service builds and runs
✓ credentials schema works
✓ existing seed remains usable
✓ Person/Credential ownership remains clean

✓ People list works
✓ People search/filter/pagination works
✓ Person detail shows credentials

✓ Credential list works
✓ status filters work
✓ access-level filter works
✓ expiring-soon filter works
✓ Credential detail works

✓ derived Active/Expired/Revoked status correct
✓ 30-day expiring-soon rule correct

✓ active Person can receive new Credential
✓ inactive Person issuance rejected
✓ duplicate number rejected
✓ expiration validation enforced

✓ revoke requires reason
✓ revoke sets RevokedAt/reason
✓ revoke is terminal
✓ repeated revoke is idempotent
✓ original revoke data preserved

✓ summary returns expiring-soon count
✓ dashboard can compose Credential summary

✓ frontend Credential Management works
✓ Michael Brown lost badge is easy to find
✓ lost badge can be revoked
✓ UI immediately shows Revoked

✓ Phase 5 SecurityManager can manage credentials
✓ Phase 5 CredentialAdministrator can manage credentials
✓ Phase 5 Technician cannot manage credentials

✓ CancellationTokens propagate
✓ errors use Problem Details
✓ OpenAPI is useful
✓ no cross-service DB access
✓ no unnecessary domain expansion
```

---

# 130. Final Service Boundary

```text
                    Next.js Frontend
                           |
                           v
                  +-------------------+
                  | CredentialService |
                  +---------+---------+
                            |
               +------------+------------+
               |                         |
               v                         v
            People                  Credentials
               |                         |
               +-----------1:*-----------+
                            |
                            v
                    credentials schema


Credential status:

RevokedAt != null
        |
       yes
        v
     Revoked

        no
        |
        v
ExpiresAt <= now?
   |          |
  yes         no
   |          |
   v          v
Expired     Active


Expiring Soon:

Active
AND
ExpiresAt <= now + 30 days


No direct dependency on:

SecurityOperationsService
WorkOrderService
physical badge hardware
Cognito business records
```

---

# 131. Governing Principle

The Credential slice exists to make one security-administration outcome unmistakable:

> **An authorized hospital security user can find a person's physical-access badge, understand its current state and expiration, and immediately revoke a lost credential through a clear, safe, idempotent workflow.**

Build the smallest implementation that demonstrates:

```text
clear domain ownership
derived state
explicit lifecycle behavior
secure authorization boundaries
good API design
good UX
historical integrity
senior-level scope discipline
```
