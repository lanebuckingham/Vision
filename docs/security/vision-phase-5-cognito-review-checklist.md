# Vision — Phase 5 Cognito and Authorization Implementation Review Checklist

**Status:** Review checklist  
**Use when:** Kiro finishes Cognito integration and ASP.NET Core authorization policies  
**Reviewer:** ChatGPT / project owner  
**Related:** Phase 5 authorization matrix, threat model, auth integration-test specification

---

# 1. Review Goal

This checklist is for reviewing Kiro's concrete implementation.

Do not approve Phase 5 merely because login works.

The review must establish:

```text
the right token is validated
the right issuer/client context is trusted
the right Cognito groups map to the right policies
Technician ownership is enforced server-side
401/403 semantics are correct
frontend token/session handling is reasonable
secrets/tokens are not leaked
the Phase 4 workflow has not weakened Phase 5 authorization
```

---

# 2. Repository / Configuration Inventory

Locate and review:

```text
authentication registration
JwtBearer configuration
authorization policy registration
middleware ordering
endpoint authorization metadata
Cognito configuration settings
Next.js authentication integration
token acquisition path
API client/fetch wrapper
logout path
Technician subject mapping
authorization integration tests
development configuration
production/deployment configuration
```

Record the exact files reviewed.

---

# 3. Cognito User Pool Configuration

Confirm configured values are explicit and environment-driven where appropriate:

```text
AWS region
user pool ID
issuer/authority
app client ID
frontend callback URL(s)
frontend logout URL(s)
Cognito domain/hosted UI configuration if used
```

Reject:

```text
real credentials committed to repository
real tokens in configuration
unexplained hard-coded production values
```

---

# 4. App Client Type

For a browser-based Next.js/public OAuth client, verify the implementation is not exposing a Cognito client secret to browser JavaScript.

If a server-side confidential client is intentionally used, verify the secret remains server-side and is supplied through secret configuration.

A secret embedded in the browser bundle is a blocker.

---

# 5. OAuth/OIDC Flow

Identify the actual Cognito flow Kiro implemented.

For an interactive browser application, review whether the selected library/flow is appropriate and whether redirect/callback handling is coherent.

Verify:

```text
redirect URI is exact
logout URI is exact
state/nonce/PKCE behavior is handled by the selected library where applicable
tokens are not passed through arbitrary application URLs
```

Do not approve a hand-built OAuth implementation if a maintained library is already being used appropriately.

---

# 6. API Uses Access Token

Trace an actual frontend API request.

Confirm:

```text
Authorization: Bearer <Cognito access token>
```

not an ID token merely because the ID token contains identity claims.

Review the token's intended-use claim handling.

Block approval if the API silently accepts the wrong Cognito token type.

---

# 7. JWT Signature Validation

Confirm ASP.NET Core JwtBearer authentication performs cryptographic signature validation using trusted Cognito metadata/signing keys.

Reject code that:

```text
decodes JWT manually and trusts it
disables signature validation
accepts unsigned tokens
turns off issuer signing-key validation
```

---

# 8. Issuer Validation

Confirm the configured trusted issuer corresponds exactly to the intended Cognito user pool.

Test/review:

```text
correct issuer -> accepted
wrong Cognito user pool issuer -> rejected with 401
```

---

# 9. Expiration / Lifetime Validation

Confirm token lifetime validation is enabled.

Review any clock-skew customization.

Reject:

```text
ValidateLifetime = false
custom code that treats expired token as authenticated
```

Expected:

```text
expired access token -> 401
```

---

# 10. App Client Validation

This deserves explicit review.

Amazon Cognito access tokens identify the originating app client with `client_id`; depending on Cognito configuration they may also carry an audience.

Verify the implementation validates the intended Vision app-client context.

Do not assume that setting a generic ASP.NET Core `Audience` property automatically validates Cognito access tokens correctly.

Inspect the actual token claims and the configured validation logic.

Expected:

```text
approved Vision app client -> accepted
unapproved app client in same user pool -> rejected
```

---

# 11. Token-Type Validation

Verify the API requires Cognito access tokens.

Inspect validation for the Cognito token-use/type claim.

Expected:

```text
access token -> eligible
ID token -> 401
```

This is a Phase 5 approval gate.

---

# 12. Cognito Groups Claim

Confirm Kiro maps the Cognito groups claim deliberately.

Expected source claim:

```text
cognito:groups
```

Expected Vision group names:

```text
SecurityManager
Technician
CredentialAdministrator
```

Do not authorize from a role value supplied by the client.

---

# 13. Authorization Policy Definitions

Expected small capability policy set:

```text
SecurityOperationsManager
WorkOrderManager
TechnicianWork
CredentialAdmin
```

Equivalent names are acceptable if semantics are clear.

Review the actual allowed groups.

Expected:

```text
SecurityOperationsManager
    SecurityManager

WorkOrderManager
    SecurityManager

TechnicianWork
    Technician
    plus resource ownership checks

CredentialAdmin
    SecurityManager OR CredentialAdministrator
```

Avoid one giant generic `VisionUser` policy for business endpoints.

---

# 14. SecurityOperationsService Coverage

Review every business endpoint.

Expected:

```text
SecurityManager -> allowed
Technician -> denied
CredentialAdministrator -> denied
```

Confirm no endpoint was left anonymous accidentally.

`/health` may remain intentionally anonymous.

---

# 15. WorkOrderService Manager Coverage

Expected SecurityManager access:

```text
list WorkOrders
detail
summary
manual create
assignment
Technician directory
```

Confirm policy is applied consistently.

---

# 16. WorkOrderService Technician-Only Actions

Expected Technician-only routes:

```text
/start
/notes
/complete
```

Confirm SecurityManager is not included simply for demo convenience.

A SecurityManager receiving success from these routes is a blocker unless the project owner explicitly changes the authorization contract.

---

# 17. Technician Subject Mapping

Locate how Kiro maps:

```text
JWT sub
    ↓
WorkOrderService.Technician.CognitoSubject
```

Confirm:

```text
subject comes from validated principal
mapping is service-owned
client cannot choose acting Technician
missing mapping fails closed
```

Reject fallback behaviors that broaden access.

---

# 18. Technician List Filtering

Inspect `GET /api/v1/work-orders`.

For Technician:

```text
query must be constrained to authenticated Technician.Id
```

Test/review a supplied:

```text
?technicianId=<someone-else>
```

The response must never widen to another Technician's work.

---

# 19. Technician Resource Authorization

Inspect detail/start/note/complete.

Required:

```text
AssignedTechnicianId == authenticatedTechnician.Id
```

Wrong owner:

```text
403
```

Confirm this is checked before mutation.

---

# 20. Technician Note Actor Identity

Confirm stored Technician identity for a note is derived from the authenticated Technician mapping.

Reject authorization based on:

```text
request.TechnicianId
query technicianId
custom X-Technician-ID header
frontend state
```

---

# 21. CredentialService Coverage

Every business endpoint must require CredentialAdmin.

Expected:

```text
SecurityManager -> allowed
CredentialAdministrator -> allowed
Technician -> 403
```

Review all seven required endpoints, including summary.

---

# 22. No Handler-Level Hard-Coded Demo Role

CredentialService specification explicitly says not to hard-code SecurityManager inside handlers before Cognito.

After Cognito, keep role decisions in authorization policies rather than scattering:

```csharp
if (role == "SecurityManager")
```

through business handlers.

Business handlers should remain primarily business/domain logic.

---

# 23. 401 Behavior

Verify:

```text
missing token -> 401
malformed token -> 401
invalid signature -> 401
expired token -> 401
wrong issuer -> 401
wrong client context -> 401
wrong token type -> 401
```

Ensure API does not redirect to Cognito on a failed API request.

---

# 24. 403 Behavior

Verify:

```text
Technician -> CredentialService -> 403
CredentialAdministrator -> WorkOrder business API -> 403
SecurityManager -> Technician-only action -> 403
Technician -> someone else's WorkOrder -> 403
Technician group with no Technician mapping -> 403
```

Do not convert authenticated authorization failures to 401.

---

# 25. Middleware Ordering

Review `Program.cs`.

Authentication and authorization middleware must be correctly ordered relative to routing/endpoints for the selected ASP.NET Core hosting model.

The concrete implementation should demonstrate that endpoint policies execute.

A passing integration test is stronger evidence than code appearance alone.

---

# 26. Default / Fallback Policy

Review whether Kiro uses:

```text
fallback policy
default policy
explicit RequireAuthorization on route groups
```

Any approach is acceptable if all business endpoints are protected intentionally.

Prefer a fail-closed posture where newly added business endpoints do not silently become anonymous.

Document intentional anonymous endpoints.

---

# 27. CORS

Confirm CredentialService uses the same configuration-driven pattern as SecurityOperationsService.

Development:

```text
http://localhost:3000
```

Production:

```text
explicit configured frontend origin
```

Reject unrestricted production CORS.

Do not confuse CORS with API authorization.

---

# 28. Frontend Token Acquisition

Trace:

```text
login
session established
access token obtained
access token attached to API call
```

Verify the frontend does not invent role values that the API trusts.

Frontend may use token/session claims to decide what navigation/actions to display.

---

# 29. Frontend Token Storage

Identify actual storage behavior.

Review:

```text
access token persistence
refresh token persistence
cookie flags if cookies are used
localStorage/sessionStorage use if present
server/client boundary
token exposure to JavaScript
```

There is no single storage pattern required by the Vision source documents, so evaluate the selected library's established approach rather than forcing a custom design.

Block obvious leakage such as tokens in URLs, logs, or source-controlled fixtures.

---

# 30. Logout

Trace the actual logout flow.

Verify:

```text
frontend authenticated session ends
normal protected UI is no longer available
application-held auth state is cleared
Cognito/library logout behavior is invoked as designed
```

Do not incorrectly require the API to treat a previously issued JWT as expired merely because browser state was cleared unless token revocation is explicitly implemented.

---

# 31. Frontend Role UX

Verify role-specific UI matches backend:

## SecurityManager

```text
Security Operations visible
WorkOrder supervisory actions visible
Credential Management visible
Technician-only repair actions not normally offered
```

## Technician

```text
own WorkOrders visible
repair actions visible for own work
Credential Management hidden
Security Operations administration hidden
```

## CredentialAdministrator

```text
Credential Management visible
Security Operations / WorkOrders hidden
```

Then verify direct HTTP calls remain protected.

---

# 32. Repair-to-Security-Resolution Workflow

This is a targeted Phase 5 review item.

Verify Technician completion does not require Technician authorization to:

```text
PATCH SecurityAsset -> Operational
resolve SecurityIncident
```

If old frontend orchestration still attempts both under a Technician token, ensure expected 403s do not make successful WorkOrder completion look failed.

Preferred MVP behavior:

```text
Technician completes WorkOrder
SecurityManager later uses Finish Security Resolution
```

Do not broaden roles to preserve the old sequence.

---

# 33. Error Handling / Problem Details

Inspect representative:

```text
401
403
400
404
409
500
```

Verify responses do not expose:

```text
stack trace
SQL internals
connection string
JWT
AWS credential
Cognito secret
```

Preserve existing Problem Details conventions.

---

# 34. Logging

Search code for logging of:

```text
Authorization
Bearer
access_token
refresh_token
id_token
JWT
client secret
```

Review authentication exception handlers/events.

Safe logs may include:

```text
correlation ID
subject
policy name
resource ID
authorization outcome
```

Do not log raw tokens.

---

# 35. Secrets / Git Hygiene

Search tracked files for:

```text
AWS_ACCESS_KEY
AWS_SECRET_ACCESS_KEY
client secret
Neon production password
real JWT
refresh token
```

Review:

```text
appsettings.json
appsettings.Development.json
.env*
launchSettings
frontend env files
GitHub Actions
Terraform variables if Phase 5 touched them
```

Local-only non-secret development configuration is acceptable.

---

# 36. Health Endpoint

If anonymous, verify minimal output.

Do not expose sensitive dependency details.

---

# 37. OpenAPI / Swagger

Review whether bearer authentication is represented accurately for developer testing.

Reject:

```text
hard-coded real token
hard-coded demo password
development bypass of authorization
```

Swagger should exercise the normal authentication/authorization path.

---

# 38. Integration Tests Present

Confirm Kiro implements the dedicated auth integration specification, especially:

```text
invalid token dimensions
401 vs 403
SecurityManager CredentialService access
SecurityManager Technician-action denial
Technician own-work enforcement
client technicianId cannot widen access
CredentialAdministrator service restriction
no-mapping Technician denial
repair-to-security-resolution boundary
```

---

# 39. Cognito Smoke Test

Before Phase 5 approval, use the actual configured Cognito development environment to prove at least:

```text
SecurityManager can log in
access token calls API successfully
SecurityManager can perform credential administration
Technician receives expected API denials
CredentialAdministrator can perform credential administration
expired/invalid token fails
logout ends normal UI session
```

Record the observed result.

---

# 40. Build / Test Gate

Expected:

```bash
dotnet build
dotnet test

cd src/frontend
npm ci
npm run lint
npm run build
```

Also run any dedicated auth integration test command/project.

If ChatGPT's review environment cannot execute the SDK/toolchain, Kiro/local execution results must be supplied explicitly.

---

# 41. Blocker Findings

Treat as Phase 5 blockers:

```text
API accepts unvalidated/decoded JWT claims
wrong issuer accepted
wrong app client accepted
ID token accepted as API access token unintentionally
expired token accepted
business endpoint anonymous unexpectedly
Technician can access CredentialService
SecurityManager can perform Technician-only action
Technician can read/mutate another Technician's WorkOrder
client-supplied TechnicianId establishes identity
CredentialAdministrator can access SecurityOperations/WorkOrders
real token/secret committed or logged
unrestricted production CORS
authorization integration tests bypass production policy logic
```

---

# 42. Non-Blocking Polish

Examples likely to be non-blocking if security is correct:

```text
slightly repetitive policy registration
minor authorization helper naming
frontend role-based menu polish
extra safe diagnostic logging
Swagger UX polish
test helper refactoring
```

Do not reopen architecture solely for cosmetic issues.

---

# 43. Final Review Output Format

When reviewing Kiro's implementation, report findings by severity:

```text
BLOCKER
HIGH
MEDIUM
LOW / POLISH
```

For each finding include:

```text
file/location
observed behavior
why it matters
required correction
verification test
```

Finish with one gate:

```text
PHASE 5 AUTH: NOT APPROVED
PHASE 5 AUTH: CONDITIONALLY APPROVED
PHASE 5 AUTH: APPROVED
```

Do not approve until all blocker/high authorization defects are resolved.

---

# 44. Current Cognito/ASP.NET Implementation Notes to Verify

These notes are implementation references, not replacements for Vision's source specifications:

- Amazon Cognito user-pool groups are represented in the `cognito:groups` claim.
- Cognito access tokens and ID tokens are distinct token types and should be verified independently.
- Cognito access tokens identify the app client with `client_id`; do not assume an ID-token-style `aud` check alone is the correct client validation.
- Cognito token validation includes signature, issuer, and time validity.
- ASP.NET Core JWT bearer authentication should return 401 for failed authentication and 403 for an authenticated principal denied authorization.

Review Kiro's concrete configuration against the actual access-token claims generated by the Vision Cognito user pool.
