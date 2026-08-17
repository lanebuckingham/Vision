# Vision — Phase 5 Authorization Matrix

**Status:** Phase 5 architecture specification  
**Audience:** Amazon Kiro implementation agent / Vision project owner  
**Scope:** SecurityOperationsService, WorkOrderService, CredentialService  
**Authentication:** Amazon Cognito, OAuth 2.0 / OIDC, JWT bearer access tokens  
**Authorization:** ASP.NET Core authorization policies plus resource/ownership checks

---

# 1. Purpose

This document defines the Phase 5 authorization model for Vision before Amazon Cognito and API authorization policies are implemented.

It resolves authorization decisions across all three services and all three MVP roles:

```text
SecurityManager
Technician
CredentialAdministrator
```

The goals are to:

- preserve service boundaries,
- enforce least privilege,
- keep the primary SecurityManager credential-management capability,
- prevent SecurityManager from inheriting Technician-only repair actions,
- enforce Technician ownership server-side,
- define consistent `401 Unauthorized` versus `403 Forbidden` behavior,
- give Kiro a concrete policy map to implement.

Backend authorization is authoritative.

Frontend route hiding and disabled controls are usability features only and must never be treated as the security boundary.

---

# 2. Approved Role Model

## SecurityManager

SecurityManager is the primary employer-facing demo role.

SecurityManager may:

```text
administer Security Operations
view/manage WorkOrders at the supervisory level
create WorkOrders
assign Technicians
perform all Credential Administrator activities required by the MVP
```

SecurityManager may **not**:

```text
start Technician work
add Technician repair notes
complete Technician work
```

The approved expansion of SecurityManager into credential administration does not make SecurityManager a Technician.

---

## Technician

Technician is a repair-work actor.

Technician may:

```text
view own assigned WorkOrders
view own assigned WorkOrder detail
start own assigned work
add notes to own assigned work
complete own assigned work
```

Technician may **not**:

```text
view everybody's WorkOrders
create WorkOrders
assign Technicians
administer Security Operations
administer Credentials
```

All Technician WorkOrder permissions are ownership-scoped.

---

## CredentialAdministrator

CredentialAdministrator is a credential-management role.

CredentialAdministrator may:

```text
browse People
view Person detail
browse Credentials
view Credential detail
view Credential summary
issue Credentials
revoke Credentials
```

CredentialAdministrator has no WorkOrder or Security Operations business permissions in the MVP.

---

# 3. Authentication Requirements

All business API endpoints are protected once Cognito is enabled.

The authentication architecture is:

```text
Amazon Cognito
    ↓
OAuth 2.0 / OIDC
    ↓
JWT access token
    ↓
ASP.NET Core JWT bearer authentication
    ↓
authorization policy
    ↓
resource/ownership authorization where required
```

Do not implement:

```text
local user/password tables
custom token issuance
home-grown role middleware
fake role headers
hard-coded demo bypasses
```

Cognito identities are authentication identities.

`CredentialService.Person` is a business record and must not be used as Vision's login identity.

`WorkOrderService.Technician` remains the business identity used for Technician ownership checks.

---

# 4. Cognito Role Mapping

Use Cognito groups as the MVP role source.

Expected Cognito group names:

```text
SecurityManager
Technician
CredentialAdministrator
```

The API authentication layer should normalize the configured Cognito group claim into role/policy evaluation.

Do not trust a role supplied by:

```text
request JSON
query string
route value
custom client header
frontend state
local storage
```

Roles come only from the validated token.

A user may technically possess more than one Cognito group, but Vision must not use group combinations to circumvent the approved capability model.

In particular:

```text
SecurityManager != Technician
```

Do not place the primary SecurityManager demo identity into the Technician group merely to make Technician-only endpoints convenient.

---

# 5. Suggested ASP.NET Core Policies

Keep the policy set small and capability-oriented.

Recommended policies:

```text
SecurityOperationsManager
WorkOrderManager
TechnicianWork
CredentialAdmin
```

## SecurityOperationsManager

Allowed group:

```text
SecurityManager
```

Used for all SecurityOperationsService business endpoints.

---

## WorkOrderManager

Allowed group:

```text
SecurityManager
```

Used for:

```text
view all WorkOrders
view WorkOrder summary
view Technician directory
create WorkOrder
assign Technician
```

---

## TechnicianWork

Allowed group:

```text
Technician
```

This policy is necessary but **not sufficient**.

Every Technician WorkOrder operation must also perform resource-level ownership authorization by mapping the authenticated Cognito subject to the service-owned `Technician` record and confirming the WorkOrder is assigned to that Technician.

Used for:

```text
view own WorkOrders
view own WorkOrder detail
start own WorkOrder
add note to own WorkOrder
complete own WorkOrder
```

---

## CredentialAdmin

Allowed groups:

```text
SecurityManager
CredentialAdministrator
```

Used for every CredentialService business endpoint.

This policy implements the approved rule that SecurityManager can perform all Credential Administrator activities needed by the MVP.

---

# 6. SecurityOperationsService Matrix

Phase 5 resolves the earlier optional Technician read access in favor of least privilege.

The current WorkOrder model carries sufficient assignment/work context, so Technician does not require direct SecurityOperationsService business access for the MVP.

| Endpoint / Capability | SecurityManager | Technician | CredentialAdministrator | Required Authorization |
|---|:---:|:---:|:---:|---|
| `GET /api/v1/dashboard` | Yes | No | No | `SecurityOperationsManager` |
| `GET /api/v1/assets` | Yes | No | No | `SecurityOperationsManager` |
| `GET /api/v1/assets/{id}` | Yes | No | No | `SecurityOperationsManager` |
| `PATCH /api/v1/assets/{id}/status` | Yes | No | No | `SecurityOperationsManager` |
| `GET /api/v1/incidents` | Yes | No | No | `SecurityOperationsManager` |
| `GET /api/v1/incidents/{id}` | Yes | No | No | `SecurityOperationsManager` |
| `POST /api/v1/incidents` | Yes | No | No | `SecurityOperationsManager` |
| `PATCH /api/v1/incidents/{id}` | Yes | No | No | `SecurityOperationsManager` |

### Notes

SecurityManager owns the operational-security workflow.

Technician does not receive incident or asset mutation rights simply because a WorkOrder originated from an incident.

CredentialAdministrator receives no SecurityOperationsService business access.

---

# 7. WorkOrderService Matrix

| Endpoint / Capability | SecurityManager | Technician | CredentialAdministrator | Ownership Constraint | Required Authorization |
|---|:---:|:---:|:---:|---|---|
| `GET /api/v1/work-orders` | Yes | Yes | No | Technician receives own assignments only | Manager or Technician + ownership query constraint |
| `GET /api/v1/work-orders/{id}` | Yes | Assigned only | No | Technician must be assigned Technician | Manager or Technician + resource check |
| `GET /api/v1/work-orders/summary` | Yes | No | No | None | `WorkOrderManager` |
| `POST /api/v1/work-orders` | Yes | No | No | None | `WorkOrderManager` |
| `POST /api/v1/work-orders/{id}/assignment` | Yes | No | No | None | `WorkOrderManager` |
| `POST /api/v1/work-orders/{id}/start` | No | Assigned only | No | Must be assigned Technician | `TechnicianWork` + resource check |
| `POST /api/v1/work-orders/{id}/notes` | No | Assigned only | No | Must be assigned Technician | `TechnicianWork` + resource check |
| `POST /api/v1/work-orders/{id}/complete` | No | Assigned only | No | Must be assigned Technician | `TechnicianWork` + resource check |
| `GET /api/v1/technicians` | Yes | No | No | None | `WorkOrderManager` |
| `GET /api/v1/technicians/{id}` | Yes | No | No | None | `WorkOrderManager` |

## Important SecurityManager Rule

SecurityManager may supervise WorkOrders but must not perform Technician repair actions.

The following must return `403 Forbidden` for an authenticated SecurityManager:

```text
POST /api/v1/work-orders/{id}/start
POST /api/v1/work-orders/{id}/notes
POST /api/v1/work-orders/{id}/complete
```

This remains true even if SecurityManager created or assigned the WorkOrder.

---

# 8. Technician Ownership Enforcement

Technician authorization requires both:

```text
role/group authorization
AND
business-resource ownership authorization
```

Role membership alone is not sufficient.

## Subject Mapping

`WorkOrderService.Technician.CognitoSubject` should map a Cognito identity to its WorkOrderService Technician record.

For a Technician request:

```text
validated JWT sub
    ↓
find Technician where CognitoSubject == sub
    ↓
obtain Technician.Id
    ↓
apply ownership restriction
```

Do not accept `technicianId` from the client as proof of identity.

---

## Technician WorkOrder List

For:

```text
GET /api/v1/work-orders
```

SecurityManager may use the normal filters.

Technician must be constrained server-side to:

```text
AssignedTechnicianId == authenticatedTechnician.Id
```

If the Technician supplies a different `technicianId` query parameter, the API must not use that value to expand access.

Recommended behavior:

```text
ignore/override technicianId with authenticated Technician.Id
```

The resulting query must never return another Technician's WorkOrders.

---

## Technician WorkOrder Detail

For:

```text
GET /api/v1/work-orders/{id}
```

Technician access requires:

```text
WorkOrder.AssignedTechnicianId == authenticatedTechnician.Id
```

If the WorkOrder exists but is assigned to someone else:

```text
403 Forbidden
```

This follows the existing WorkOrder specification's wrong-assigned-Technician behavior.

---

## Technician Mutations

For:

```text
/start
/notes
/complete
```

the API must verify the WorkOrder is assigned to the authenticated Technician before executing the command.

The client must not be permitted to choose the acting Technician identity.

For Technician notes, the stored `TechnicianId` must come from the authenticated business identity, not from request JSON.

---

## Missing Technician Business Mapping

If an authenticated Cognito user has the Technician group but no matching service-owned Technician record for the JWT subject:

```text
403 Forbidden
```

Log a safe diagnostic event without logging the bearer token.

Do not automatically create a Technician record from claims during request processing.

---

# 9. CredentialService Matrix

Every CredentialService business endpoint uses `CredentialAdmin`.

| Endpoint / Capability | SecurityManager | Technician | CredentialAdministrator | Required Authorization |
|---|:---:|:---:|:---:|---|
| `GET /api/v1/people` | Yes | No | Yes | `CredentialAdmin` |
| `GET /api/v1/people/{id}` | Yes | No | Yes | `CredentialAdmin` |
| `GET /api/v1/credentials` | Yes | No | Yes | `CredentialAdmin` |
| `GET /api/v1/credentials/{id}` | Yes | No | Yes | `CredentialAdmin` |
| `GET /api/v1/credentials/summary` | Yes | No | Yes | `CredentialAdmin` |
| `POST /api/v1/people/{personId}/credentials` | Yes | No | Yes | `CredentialAdmin` |
| `POST /api/v1/credentials/{id}/revoke` | Yes | No | Yes | `CredentialAdmin` |

Technician receives no CredentialService business access.

For an authenticated Technician, every protected CredentialService business endpoint returns:

```text
403 Forbidden
```

The primary SecurityManager demo identity can therefore:

```text
open Credential Management
find Michael Brown
view the lost-badge credential
revoke it
see Revoked immediately
```

without switching to a CredentialAdministrator account.

---

# 10. 401 vs 403 Contract

Use the following rule consistently across all services.

## 401 Unauthorized

Return `401` when authentication has not succeeded.

Examples:

```text
no bearer token
invalid bearer token
expired bearer token
bad signature
untrusted issuer
token rejected by configured JWT validation
```

The request has no accepted authenticated principal.

---

## 403 Forbidden

Return `403` when authentication succeeded but the principal is not authorized.

Examples:

```text
Technician calls CredentialService
CredentialAdministrator calls WorkOrderService
SecurityManager calls Technician-only /start
Technician accesses another Technician's WorkOrder
Technician group has no valid Technician business mapping
```

Do not convert authenticated authorization failures into `401`.

---

## 404 Not Found

Continue using `404` for normal resource absence after authorization requirements are satisfied.

Examples:

```text
authorized SecurityManager requests unknown WorkOrder
authorized Credential Administrator requests unknown Credential
```

For the WorkOrderService Technician ownership case, the existing specification explicitly defines wrong assigned Technician as `403`; preserve that behavior.

---

# 11. Health Endpoints

Service health probes are infrastructure endpoints rather than business capabilities.

The existing:

```text
GET /health
```

endpoint may remain anonymous so container/platform health checks do not require Cognito tokens.

Do not expose sensitive configuration, credentials, connection details, token information, or internal exception data through health responses.

---

# 12. Authorization Placement

Prefer endpoint/route authorization for coarse capability checks and application/resource checks for ownership-sensitive behavior.

Example conceptual structure:

```text
endpoint
    ↓
RequireAuthorization("TechnicianWork")
    ↓
resolve authenticated Technician from JWT subject
    ↓
load WorkOrder
    ↓
verify AssignedTechnicianId
    ↓
execute command
```

Do not rely only on controller/minimal-API endpoint metadata for Technician operations because role membership cannot prove assignment ownership.

Do not push all authorization into the frontend.

---

# 13. Authorization Must Precede Mutation

For protected mutations:

```text
authenticate
    ↓
authorize role/capability
    ↓
authorize resource ownership when applicable
    ↓
validate/execute business operation
    ↓
persist
```

An unauthorized caller must never reach a mutation merely because the request DTO is valid.

Authorization failure must not partially modify persistence.

---

# 14. Repair-to-Security-Resolution Boundary

Phase 4 currently contains a frontend orchestration concept:

```text
complete WorkOrder
    ↓
PATCH SecurityAsset -> Operational
    ↓
resolve SecurityIncident
```

Phase 5 authorization creates an intentional role boundary:

```text
Technician
    may complete assigned WorkOrder

SecurityManager
    may mutate SecurityAsset / SecurityIncident
```

Therefore, once Cognito authorization is enabled, a Technician completing work must **not** automatically gain permission to execute the two SecurityOperationsService mutations.

Do not solve this by:

```text
granting Technician incident mutation
granting Technician asset-status mutation
granting SecurityManager Technician-only completion
putting the SecurityManager in the Technician group
bypassing backend authorization for the frontend
```

## MVP Phase 5 behavior

After Technician completion:

```text
WorkOrder == Completed
```

The security-resolution follow-up remains a SecurityManager responsibility.

The existing `Finish Security Resolution` concept is the correct place to preserve this separation.

SecurityManager can subsequently:

```text
set asset -> Operational
resolve incident -> Resolved
```

This may require a small frontend adjustment so a Technician completion does not treat expected `403` responses from SecurityOperationsService as a failed WorkOrder completion.

No new workflow engine or service-to-service impersonation mechanism is required for the MVP.

---

# 15. Frontend Behavior

The frontend should use authenticated role information to improve UX.

Examples:

## SecurityManager

Show:

```text
Security Operations
Work Orders supervisory actions
Credential Management
Create WorkOrder
Assign Technician
Issue Credential
Revoke Credential
Finish Security Resolution
```

Hide/disable:

```text
Start Work
Add Technician Note
Complete Work
```

---

## Technician

Show:

```text
own WorkOrders
Start Work
Add Technician Note
Complete Work
```

Hide:

```text
Security Operations administration
Create WorkOrder
Assign Technician
Credential Management
```

---

## CredentialAdministrator

Show:

```text
Credential Management
```

Hide:

```text
Security Operations
WorkOrder administration
Technician repair actions
```

Frontend behavior is not authorization enforcement.

A manually crafted HTTP request must still be rejected by the API.

---

# 16. Token Safety

Authorization implementation must not log:

```text
Authorization header
raw access token
refresh token
ID token
Cognito secret
```

Logging may include safe identifiers when useful, such as:

```text
correlation ID
Cognito subject
resolved role/group names
endpoint
authorization result
business resource ID
```

Avoid logging excessive personal information from CredentialService.

---

# 17. Minimum Authorization Integration Tests

Kiro should implement integration tests covering at least the following.

## Authentication

```text
protected endpoint + no token -> 401
protected endpoint + invalid token -> 401
protected endpoint + expired token -> 401
```

---

## SecurityOperationsService

```text
SecurityManager GET dashboard -> success
SecurityManager POST incident -> success
Technician GET dashboard -> 403
Technician PATCH asset status -> 403
Technician PATCH incident -> 403
CredentialAdministrator security-operations request -> 403
```

---

## WorkOrderService — SecurityManager

```text
SecurityManager GET all WorkOrders -> success
SecurityManager GET summary -> success
SecurityManager GET technicians -> success
SecurityManager POST WorkOrder -> success
SecurityManager assign Technician -> success

SecurityManager start WorkOrder -> 403
SecurityManager add Technician note -> 403
SecurityManager complete WorkOrder -> 403
```

---

## WorkOrderService — Technician

```text
Technician GET work-orders -> only own assigned WorkOrders
Technician GET own WorkOrder detail -> success
Technician GET another Technician's WorkOrder -> 403
Technician start own assigned WorkOrder -> success
Technician start another Technician's WorkOrder -> 403
Technician add note to own WorkOrder -> success
Technician add note to another Technician's WorkOrder -> 403
Technician complete own WorkOrder -> success
Technician complete another Technician's WorkOrder -> 403
Technician manually create WorkOrder -> 403
Technician assign Technician -> 403
Technician GET technicians -> 403
Technician GET WorkOrder summary -> 403
```

Also verify:

```text
client-supplied technicianId cannot expand Technician list access
note TechnicianId is derived from authenticated identity
```

---

## WorkOrderService — CredentialAdministrator

```text
CredentialAdministrator WorkOrder business request -> 403
```

---

## CredentialService

```text
SecurityManager browse People -> success
SecurityManager browse Credentials -> success
SecurityManager issue Credential -> success
SecurityManager revoke Credential -> success
SecurityManager view summary -> success

CredentialAdministrator same capability set -> success

Technician GET People -> 403
Technician GET Credentials -> 403
Technician issue Credential -> 403
Technician revoke Credential -> 403
Technician view credential summary -> 403
```

---

# 18. Policy Acceptance Criteria

Phase 5 authorization is acceptable when:

- every business endpoint requires authenticated access,
- `/health` remains usable by infrastructure without Cognito,
- SecurityManager has all Security Operations management capabilities,
- SecurityManager has supervisory WorkOrder capabilities,
- SecurityManager cannot perform Technician-only repair actions,
- SecurityManager has the complete Credential Administrator capability set,
- Technician sees only assigned WorkOrders,
- Technician cannot manipulate another Technician's WorkOrder,
- Technician actor identity is derived from the authenticated Cognito subject,
- CredentialAdministrator has CredentialService access only,
- wrong authenticated roles return `403`,
- unauthenticated/invalid-token requests return `401`,
- authorization is enforced by backend APIs,
- bearer tokens and secrets are not logged,
- frontend visibility is consistent with, but not relied upon for, backend enforcement.

---

# 19. Explicit Phase 5 Decisions

The following previously optional/ambiguous permissions are now resolved for the MVP:

```text
Technician -> SecurityOperations dashboard: No
Technician -> SecurityOperations asset reads: No
Technician -> WorkOrder summary: No
Technician -> Technician directory: No
CredentialAdministrator -> SecurityOperations reads: No
CredentialAdministrator -> WorkOrder reads: No
```

Reason:

```text
least privilege
clear role boundaries
smaller attack surface
simpler authorization model
no demonstrated MVP requirement for these cross-role reads
```

If a later UX requirement genuinely requires one of these reads, change the matrix deliberately rather than granting broad access preemptively.

---

# 20. Kiro Implementation Guidance

Recommended implementation order:

```text
1. Configure Cognito JWT bearer authentication.
2. Configure role/group claim mapping.
3. Register the four capability policies.
4. Protect all business endpoint groups.
5. Implement WorkOrder Technician subject resolution.
6. Enforce own-work query filtering.
7. Enforce resource ownership for Technician detail/mutations.
8. Apply CredentialAdmin to all CredentialService endpoints.
9. Leave /health anonymous.
10. Update frontend visibility/navigation by role.
11. Adjust repair-to-security-resolution UI for the Phase 5 role boundary.
12. Add authorization integration tests.
```

Keep the implementation straightforward.

Do not introduce a separate authorization microservice, custom permissions database, generalized RBAC engine, or policy framework for the MVP.

---

# 21. Final Capability Summary

```text
SecurityManager
    SecurityOperationsService: full MVP business access
    WorkOrderService: supervisory access
    CredentialService: full MVP credential-admin access
    Technician repair actions: NO

Technician
    SecurityOperationsService: no MVP business access
    WorkOrderService: own assigned repair work only
    CredentialService: no access

CredentialAdministrator
    SecurityOperationsService: no access
    WorkOrderService: no access
    CredentialService: full MVP credential-admin access
```

This is the Phase 5 authorization contract Kiro should implement unless the project owner explicitly approves a later change.
