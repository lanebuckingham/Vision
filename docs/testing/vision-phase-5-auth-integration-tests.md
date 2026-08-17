# Vision — Phase 5 Authentication and Authorization Integration Test Specification

**Status:** Phase 5 test-design specification  
**Audience:** Amazon Kiro implementation agent / Vision project owner  
**Depends on:** Phase 5 authorization matrix and Phase 5 threat model  
**Goal:** Verify the real authentication/authorization path rather than controller logic in isolation

---

# 1. Purpose

This specification defines the minimum integration-test coverage required for Vision Phase 5 authentication and authorization.

The tests must prove:

```text
valid identity + allowed capability -> success
missing/invalid identity -> 401
valid identity + wrong capability -> 403
Technician identity + wrong WorkOrder owner -> 403
```

Frontend hiding is not sufficient.

Where practical, tests should exercise the ASP.NET Core authentication/authorization middleware and endpoint policy configuration through HTTP.

---

# 2. Test Roles

Create test identities representing:

```text
SecurityManager
Technician A
Technician B
CredentialAdministrator
Authenticated user with no approved Vision role
```

For Technician tests, create service-owned records such that:

```text
Technician A.CognitoSubject == subject for Technician A token
Technician B.CognitoSubject == subject for Technician B token
```

and create WorkOrders assigned separately to A and B.

---

# 3. Token Test Dimensions

The test harness must be able to exercise at least these token conditions:

```text
valid access token
no token
malformed token
invalid signature
expired token
wrong issuer / user pool
wrong app client context
wrong token type (ID token where access token is required)
valid token with wrong Cognito group
valid token with no approved group
```

Tests do not have to call the live Cognito service on every run.

A deterministic local/test signing setup is acceptable if it faithfully exercises the production JWT bearer validation configuration or an equivalent test configuration.

At least one higher-level environment test should validate the actual Cognito configuration before Phase 5 approval/deployment.

---

# 4. Global Authentication Tests

Run against at least one protected endpoint in each service.

| Scenario | Expected |
|---|---|
| No bearer token | `401` |
| Malformed bearer token | `401` |
| Invalid signature | `401` |
| Expired access token | `401` |
| Wrong issuer | `401` |
| Unapproved app client | `401` |
| ID token used instead of access token | `401` |
| Valid access token, allowed role | request proceeds to authorization/business handling |

Verify no authentication failure response contains:

```text
raw JWT
refresh token
client secret
AWS credential
connection string
stack trace
```

---

# 5. 401 vs 403 Contract Tests

These are explicit acceptance tests.

## Authentication failure

```text
Given no accepted authenticated principal
When a protected endpoint is called
Then response == 401
```

## Authorization failure

```text
Given a valid authenticated principal
And the principal lacks the required capability
When the protected endpoint is called
Then response == 403
```

Do not accept an implementation that returns 401 for normal wrong-role cases.

---

# 6. SecurityOperationsService — SecurityManager

Verify SecurityManager can:

```text
GET /api/v1/dashboard
GET /api/v1/assets
GET /api/v1/assets/{id}
PATCH /api/v1/assets/{id}/status
GET /api/v1/incidents
GET /api/v1/incidents/{id}
POST /api/v1/incidents
PATCH /api/v1/incidents/{id}
```

Use existing endpoint-specific expected success codes.

Also verify existing domain validation still works after authorization is added.

Authentication must not cause valid business validation errors to turn into authorization errors.

---

# 7. SecurityOperationsService — Technician Denials

For Technician A, verify:

```text
GET /api/v1/dashboard -> 403
GET /api/v1/assets -> 403
GET /api/v1/assets/{id} -> 403
PATCH /api/v1/assets/{id}/status -> 403
GET /api/v1/incidents -> 403
GET /api/v1/incidents/{id} -> 403
POST /api/v1/incidents -> 403
PATCH /api/v1/incidents/{id} -> 403
```

Verify no mutation occurs on denied PATCH/POST requests.

---

# 8. SecurityOperationsService — CredentialAdministrator Denials

Verify CredentialAdministrator receives `403` from every SecurityOperationsService business endpoint.

`/health` is excluded from this business-access rule if intentionally anonymous.

---

# 9. WorkOrderService — SecurityManager Allowed Capabilities

Verify SecurityManager can:

```text
GET /api/v1/work-orders
GET /api/v1/work-orders/{id}
GET /api/v1/work-orders/summary
POST /api/v1/work-orders
POST /api/v1/work-orders/{id}/assignment
GET /api/v1/technicians
GET /api/v1/technicians/{id}
```

Use valid data and assert normal successful behavior.

---

# 10. WorkOrderService — SecurityManager Technician-Action Denials

SecurityManager must receive `403` for:

```text
POST /api/v1/work-orders/{id}/start
POST /api/v1/work-orders/{id}/notes
POST /api/v1/work-orders/{id}/complete
```

Test even when:

```text
SecurityManager created the WorkOrder
SecurityManager assigned the Technician
WorkOrder is otherwise in a valid state for the requested transition
```

Assert no WorkOrder state or notes change.

---

# 11. WorkOrderService — Technician List Ownership

Given:

```text
WorkOrder A -> Technician A
WorkOrder B -> Technician B
```

When Technician A calls:

```text
GET /api/v1/work-orders
```

Then response contains A's assigned WorkOrders and not B's.

Also test:

```text
GET /api/v1/work-orders?technicianId=<Technician-B>
```

Expected:

```text
Technician A still cannot receive Technician B's WorkOrders.
```

Implementation may ignore/override the supplied filter, but it must never widen access.

---

# 12. WorkOrderService — Technician Detail Ownership

## Own assignment

```text
Technician A
GET WorkOrder A
-> success
```

## Another Technician's assignment

```text
Technician A
GET WorkOrder B
-> 403
```

Preserve the approved wrong-assigned-Technician behavior rather than converting it to broad read access.

---

# 13. WorkOrderService — Technician Start Ownership

Test:

```text
Technician A starts WorkOrder A -> success
Technician A starts WorkOrder B -> 403
```

For denied access:

```text
status unchanged
StartedAt unchanged
no other mutation
```

---

# 14. WorkOrderService — Technician Notes Ownership

Test:

```text
Technician A adds note to WorkOrder A -> success
Technician A adds note to WorkOrder B -> 403
```

Critically verify the persisted note actor identity comes from the authenticated Technician mapping.

Do not trust any client-supplied Technician ID.

If request DTO currently contains TechnicianId, the server must ignore/remove it for actor identity.

---

# 15. WorkOrderService — Technician Completion Ownership

Test:

```text
Technician A completes WorkOrder A -> success
Technician A completes WorkOrder B -> 403
```

Denied request must not:

```text
change status
set CompletedAt
change completion summary
modify SecurityOperations state
```

---

# 16. WorkOrderService — Technician Supervisory Denials

Technician must receive `403` for:

```text
GET /api/v1/work-orders/summary
POST /api/v1/work-orders
POST /api/v1/work-orders/{id}/assignment
GET /api/v1/technicians
GET /api/v1/technicians/{id}
```

---

# 17. WorkOrderService — Missing Technician Mapping

Given:

```text
valid access token
cognito:groups contains Technician
JWT subject has no matching Technician.CognitoSubject
```

Verify Technician-owned WorkOrder request returns:

```text
403
```

Verify application does not:

```text
auto-create a Technician
accept technicianId from client
fall back to unfiltered WorkOrders
```

---

# 18. WorkOrderService — CredentialAdministrator Denials

Verify CredentialAdministrator receives `403` for all WorkOrderService business endpoints.

---

# 19. CredentialService — SecurityManager

Verify SecurityManager can:

```text
GET /api/v1/people
GET /api/v1/people/{id}
GET /api/v1/credentials
GET /api/v1/credentials/{id}
GET /api/v1/credentials/summary
POST /api/v1/people/{personId}/credentials
POST /api/v1/credentials/{id}/revoke
```

This test set is mandatory because SecurityManager's credential-administration capability is a deliberate Vision demo requirement.

---

# 20. CredentialService — CredentialAdministrator

Run the same allowed capability set as SecurityManager.

Do not make SecurityManager a special-case inside CredentialService handlers.

Both roles should satisfy the shared CredentialAdmin policy.

---

# 21. CredentialService — Technician Denials

Technician must receive `403` for:

```text
GET /api/v1/people
GET /api/v1/people/{id}
GET /api/v1/credentials
GET /api/v1/credentials/{id}
GET /api/v1/credentials/summary
POST /api/v1/people/{personId}/credentials
POST /api/v1/credentials/{id}/revoke
```

Denied issue/revoke requests must not alter persistence.

---

# 22. CredentialService — No-Role Denials

A valid authenticated Cognito user with no Vision role/group must receive:

```text
403
```

from all CredentialService business endpoints.

---

# 23. Credential Issuance + Authorization Interaction

For an authorized SecurityManager or CredentialAdministrator, preserve CredentialService rules:

```text
Person must exist
Person must be active
CredentialNumber unique
ExpiresAt > IssuedAt
AccessLevel valid
```

Test ordering at behavior level:

```text
wrong role + otherwise valid request -> 403 and no mutation
allowed role + invalid business request -> existing 400/404/409 behavior
```

Authorization must not erase domain validation for authorized callers.

---

# 24. Credential Revocation + Authorization Interaction

## Allowed first revoke

Authorized role:

```text
POST /api/v1/credentials/{id}/revoke
reason = "Badge reported lost"
```

Assert:

```text
success
status == Revoked
RevokedAt set
RevocationReason preserved
```

## Allowed repeated revoke

Authorized role revokes same credential again with a different reason.

Assert:

```text
success
original RevokedAt unchanged
original RevocationReason unchanged
```

## Denied revoke

Technician attempts same endpoint.

Assert:

```text
403
RevokedAt unchanged
RevocationReason unchanged
```

---

# 25. Credential Summary Authorization

Verify:

```text
SecurityManager -> success
CredentialAdministrator -> success
Technician -> 403
no token -> 401
```

For authorized callers, preserve summary semantics:

```text
activeCount
expiringSoonCount
expiredCount
revokedCount

expiringSoon is a subset of active
```

---

# 26. Health Endpoint Tests

If `/health` remains anonymous:

```text
GET /health without token -> success
```

Assert response does not expose:

```text
connection string
database password
AWS credentials
Cognito secret
JWT
stack trace
```

---

# 27. CORS Tests

Development configuration should allow the configured local frontend origin:

```text
http://localhost:3000
```

Production configuration must not use unrestricted production CORS.

At minimum, configuration tests should prove the allowed origin comes from configuration and is not hard-coded to `AllowAnyOrigin()` for production.

Remember: CORS tests supplement authorization tests; they do not replace them.

---

# 28. Token-Logging Tests / Inspection

Trigger:

```text
invalid token
expired token
403 role denial
```

Inspect captured application logs.

Assert raw bearer token does not appear.

If test logging captures request headers, ensure Authorization is redacted/excluded.

---

# 29. Problem Details Security Tests

For representative:

```text
400
401
403
404
409
500/test exception path
```

assert responses do not expose:

```text
stack trace
SQL text
database connection string
AWS credential
Cognito secret
raw JWT
```

Keep existing Problem Details conventions.

---

# 30. Frontend Authorization UX Tests

These are secondary to API tests but should cover:

## SecurityManager

Visible:

```text
Security Operations navigation
WorkOrder supervisory actions
Credential Management
Issue
Revoke
Finish Security Resolution
```

Not offered as normal UI actions:

```text
Start Work
Add Technician Note
Complete Work
```

## Technician

Visible:

```text
own WorkOrders
Start Work
Add Technician Note
Complete Work
```

Hidden:

```text
Credential Management
Create WorkOrder
Assign Technician
Security Operations admin actions
```

## CredentialAdministrator

Visible:

```text
Credential Management
```

Hidden:

```text
Security Operations business navigation
WorkOrder business navigation
Technician repair actions
```

Then bypass the UI and call denied API endpoints directly to prove backend enforcement.

---

# 31. Repair-to-Security-Resolution Workflow Test

This test protects the role boundary introduced by Phase 5.

## Technician phase

Technician completes own WorkOrder.

Assert:

```text
WorkOrder == Completed
```

Do not require Technician to successfully mutate:

```text
SecurityAsset -> Operational
SecurityIncident -> Resolved
```

If the frontend currently attempts those calls automatically, expected 403 responses must not cause the already successful WorkOrder completion to appear rolled back.

## SecurityManager phase

SecurityManager later invokes the security-resolution follow-up.

Assert:

```text
asset -> Operational
incident -> Resolved
dashboard reflects improvement
```

Do not grant Technician SecurityOperations mutation rights to make this test easier.

---

# 32. Cognito Group Mapping Tests

Given a validated access token with:

```text
cognito:groups = ["SecurityManager"]
```

verify manager policies behave as intended.

Given:

```text
cognito:groups = ["CredentialAdministrator"]
```

verify CredentialAdmin only.

Given:

```text
cognito:groups = ["Technician"]
```

verify TechnicianWork role gate plus ownership checks.

Also test missing group claim:

```text
valid token with no recognized group -> authenticated but business access denied (403)
```

---

# 33. Multiple-Group Defensive Test

Although the primary demo identities should use clean role assignments, test the implementation behavior if a token contains multiple groups.

The presence of SecurityManager must not silently bypass explicit resource checks on Technician endpoints.

Prefer endpoint policies that express their actual allowed role plus ownership requirement rather than a generic "any Vision user" policy.

The project owner should approve any intentionally multi-role demo identity.

---

# 34. Middleware / Endpoint Coverage Test

Verify every business endpoint in all three services has the intended authorization requirement.

A useful automated guard can enumerate endpoint metadata and fail if a business route lacks authorization metadata unexpectedly.

Exclude explicitly anonymous infrastructure routes such as `/health`.

This protects against a newly added endpoint accidentally shipping anonymous.

---

# 35. Minimum CI Gate

Before Phase 5 approval:

```text
dotnet build
dotnet test
frontend lint
frontend build
authorization integration test suite
```

If live-Cognito validation cannot run in ordinary CI, run deterministic JWT integration tests in CI and perform a separate documented Cognito smoke test in the configured development environment.

---

# 36. Phase 5 Authentication/Authorization Test Acceptance

Phase 5 test coverage is acceptable when it proves:

- unauthenticated protected access returns 401,
- bad JWT validation dimensions return 401,
- wrong authenticated role returns 403,
- SecurityManager has CredentialService administration,
- SecurityManager lacks Technician-only mutations,
- Technician can access only assigned WorkOrders,
- Technician identity is derived from Cognito subject mapping,
- client-supplied technicianId cannot widen access,
- CredentialAdministrator is limited to CredentialService business access,
- denied mutations leave persistence unchanged,
- credential issue/revoke domain rules still work for authorized callers,
- revocation idempotency remains intact,
- raw tokens/secrets do not appear in logs/errors,
- `/health` behavior is deliberate,
- frontend visibility matches backend policies,
- direct API calls cannot bypass frontend hiding,
- repair completion and security resolution preserve the approved role boundary.
