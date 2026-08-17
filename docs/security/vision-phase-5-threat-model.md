# Vision — Phase 5 Lightweight Threat Model and Security Design

**Status:** Phase 5 architecture/security specification  
**Audience:** Amazon Kiro implementation agent / Vision project owner  
**Applies to:** SecurityOperationsService, WorkOrderService, CredentialService, Next.js frontend  
**Identity provider:** Amazon Cognito  
**Authorization contract:** `vision-phase-5-authorization-matrix.md`

---

# 1. Purpose

This document defines the Phase 5 security design and lightweight threat model for Vision.

It is intentionally scoped to the current MVP. The goal is not to build an enterprise IAM platform. The goal is to make the existing three-service application demonstrate sound authentication, authorization, token handling, service boundaries, and least-privilege decisions.

The Phase 5 security design must preserve these approved decisions:

```text
SecurityManager
    Security Operations management
    WorkOrder supervisory capabilities
    full Credential Administrator capability set
    NOT Technician repair actions

Technician
    own assigned WorkOrders only
    NOT Security Operations administration
    NOT Credential administration

CredentialAdministrator
    CredentialService business access only
```

Backend enforcement is authoritative.

---

# 2. Security Objectives

Vision must protect:

1. **Authentication integrity**
   - APIs accept only valid tokens issued for the configured Cognito user pool and intended app/API context.
   - Expired, malformed, unsigned, incorrectly signed, or untrusted tokens are rejected.

2. **Authorization integrity**
   - Authenticated users cannot exceed their approved role capabilities.
   - Technician ownership is enforced at the resource level, not only by role.

3. **Credential-management integrity**
   - Unauthorized users cannot browse, issue, or revoke physical-access credentials.
   - Credential revocation remains terminal and idempotent.

4. **Operational-security integrity**
   - Unauthorized users cannot change asset status or incident state.

5. **Work-order integrity**
   - SecurityManager retains supervisory functions.
   - Technician actions are limited to the authenticated Technician's assigned work.

6. **Confidentiality**
   - JWTs, refresh tokens, secrets, connection strings, and AWS/Cognito credentials are not exposed in logs, errors, source control, or client-visible configuration.

7. **Service-boundary integrity**
   - Authentication does not become an excuse to merge `Person`, `Technician`, and Cognito identity into a shared cross-service aggregate.

---

# 3. In-Scope Components

```text
Browser / Next.js frontend
        ↓ HTTPS
ASP.NET Core APIs
    SecurityOperationsService
    WorkOrderService
    CredentialService
        ↓
PostgreSQL / Neon

Browser
        ↕
Amazon Cognito hosted authentication / OAuth-OIDC flow

SecurityOperationsService
        ↓
Amazon SQS
        ↓
WorkOrderService
```

Existing Phase 4 SQS behavior remains governed by the approved messaging specifications.

Authentication/authorization added in Phase 5 must not break service-owned persistence or the existing integration-event contract.

---

# 4. Trust Boundaries

## Boundary A — Browser to Cognito

The browser is untrusted.

The browser may initiate sign-in and receive tokens, but the API must never assume that browser state, route visibility, local role state, or client-side claims are authoritative.

Threats include:

```text
manually altered frontend state
stolen/replayed browser token
crafted HTTP requests
modified JavaScript
direct calls to APIs
```

---

## Boundary B — Browser to Vision APIs

Every request crossing into a business API is untrusted until:

```text
token is authenticated
        ↓
role/capability is authorized
        ↓
resource ownership is authorized when applicable
```

CORS is not an authorization mechanism.

---

## Boundary C — Cognito to Vision APIs

The API trusts identity claims only after cryptographic and semantic token validation.

The API must not trust a decoded JWT merely because its JSON looks valid.

Important validation concepts include:

```text
signature / signing key
issuer
expiration
intended token type
configured Cognito app client context
required role/group claims
```

---

## Boundary D — API to Database

Each service owns only its approved schema.

```text
SecurityOperationsService -> security_operations.*
WorkOrderService          -> work_orders.*
CredentialService         -> credentials.*
```

Authorization does not change database ownership.

No service may authorize itself to directly mutate another service's schema.

---

## Boundary E — SecurityOperationsService to SQS to WorkOrderService

SQS messages are integration inputs, not authenticated human requests.

Phase 5 must not apply end-user Cognito policies to the SQS consumer.

Existing event-contract validation, idempotency, retry, DLQ, and correlation behavior remain required.

---

# 5. Protected Assets

## Identity and Authorization Assets

```text
Cognito user pool configuration
Cognito app client configuration
Cognito groups
JWT signing-key trust
role/group mapping
authenticated Cognito subject (`sub`)
Technician.CognitoSubject mapping
authorization policies
```

## Business Assets

```text
security asset state
security incident state
WorkOrders
Technician notes
assignment information
People
Credentials
credential numbers
access levels
revocation state
revocation reason
```

## Secrets and Operational Assets

```text
database credentials
AWS credentials
Cognito secrets if any server-side client requires them
deployment configuration
JWTs
refresh tokens
connection strings
```

---

# 6. Threat: Forged or Tampered JWT

## Scenario

An attacker crafts a JWT containing:

```text
cognito:groups = ["SecurityManager"]
```

and calls a protected endpoint.

## Required Mitigation

The API must validate the JWT cryptographically through ASP.NET Core JWT bearer authentication using the configured trusted Cognito issuer/signing keys.

Do not:

```text
base64-decode token and trust claims
parse role claim manually before authentication
accept unsigned JWTs
disable signature validation
```

## Expected Result

```text
invalid signature -> 401
unknown signing key that cannot be validated -> 401
malformed token -> 401
```

---

# 7. Threat: Token from Wrong Issuer / User Pool

## Scenario

A valid Cognito token from another user pool is presented to Vision.

Its signature is valid for that other issuer but the token is not a Vision identity.

## Required Mitigation

Validate the configured issuer/user-pool authority.

A validly signed token from the wrong issuer must not authenticate.

## Expected Result

```text
wrong issuer -> 401
```

---

# 8. Threat: Wrong App Client / Audience Context

## Scenario

A token comes from the correct Cognito user pool but from an app client that Vision does not intend to trust.

## Required Mitigation

Phase 5 configuration and review must verify the intended app-client validation behavior.

For Cognito access tokens, the app client is represented by the access-token `client_id` claim. Depending on Cognito configuration, an access token may also contain an audience claim. Kiro must not blindly configure ASP.NET Core `Audience` based on an ID-token assumption without validating how the selected Cognito access tokens are structured.

The API must explicitly validate the intended client/app context rather than accepting every token from the user pool.

## Expected Result

```text
token from unapproved client -> 401
```

---

# 9. Threat: ID Token Used as API Authorization Token

## Scenario

The frontend accidentally sends a Cognito ID token as the bearer token because it also contains identity/group claims.

## Security Decision

Vision APIs should use **access tokens** for API authorization.

The implementation/review must verify the expected Cognito token-use claim and reject an ID token presented where an access token is required.

## Expected Result

```text
ID token used as API bearer token -> 401
```

This is an important Phase 5 review gate.

---

# 10. Threat: Expired Token

## Scenario

A previously valid access token is reused after expiration.

## Required Mitigation

JWT lifetime validation must remain enabled.

Do not create a custom grace path that treats expired tokens as authenticated.

## Expected Result

```text
expired token -> 401
```

The frontend may obtain a fresh token through the chosen authentication library/session mechanism, but the API itself does not refresh an expired client token.

---

# 11. Threat: Role Escalation Through Request Data

## Scenario

A Technician sends:

```json
{
  "role": "SecurityManager"
}
```

or supplies:

```text
X-Role: SecurityManager
```

## Required Mitigation

Roles come only from authenticated Cognito claims after token validation.

Never authorize from:

```text
request body role
query-string role
route role
custom role header
frontend session object alone
local storage role value
```

## Expected Result

The supplied value has no authorization effect.

---

# 12. Threat: SecurityManager Accidentally Gains Technician Rights

## Scenario

To preserve the old frontend "complete then resolve security incident" flow, implementation places the SecurityManager demo user in both:

```text
SecurityManager
Technician
```

or changes Technician-only endpoints to accept SecurityManager.

## Required Mitigation

Do not broaden the approved role model.

SecurityManager remains denied from:

```text
POST /api/v1/work-orders/{id}/start
POST /api/v1/work-orders/{id}/notes
POST /api/v1/work-orders/{id}/complete
```

## Expected Result

```text
authenticated SecurityManager -> 403
```

The frontend workflow must adapt to authorization, not the reverse.

---

# 13. Threat: Technician Accesses Another Technician's Work

## Scenario

Technician A changes:

```text
/work-orders/{workOrderId}
```

to the ID of a WorkOrder assigned to Technician B.

Or Technician A adds:

```text
?technicianId=<Technician-B>
```

to the list request.

## Required Mitigation

Resolve the acting Technician from the validated Cognito `sub` through:

```text
Technician.CognitoSubject
```

Then enforce:

```text
AssignedTechnicianId == authenticatedTechnician.Id
```

For list requests, server-side query filtering must force the authenticated Technician's ID.

Client input must never establish ownership.

## Expected Result

```text
own assigned resource -> allowed
other Technician's resource -> 403
client-supplied technicianId cannot widen result set
```

---

# 14. Threat: Authenticated Technician Has No Business Mapping

## Scenario

A Cognito identity is in the Technician group but there is no `WorkOrderService.Technician` whose `CognitoSubject` matches the JWT subject.

## Required Mitigation

Fail closed.

Do not:

```text
pick first Technician
accept technicianId from request
auto-create Technician during request
fall back to broad Technician access
```

## Expected Result

```text
403
```

Log a safe diagnostic containing the Cognito subject and correlation ID, not the bearer token.

---

# 15. Threat: Technician Accesses Credential Inventory

## Scenario

A Technician directly calls CredentialService even though the frontend hides Credential Management.

## Required Mitigation

Every CredentialService business endpoint must enforce the `CredentialAdmin` policy.

Allowed:

```text
SecurityManager
CredentialAdministrator
```

Denied:

```text
Technician
```

## Expected Result

```text
authenticated Technician -> 403
```

---

# 16. Threat: CredentialAdministrator Gains Broader Business Access

## Scenario

Because CredentialAdministrator is authenticated, implementation accidentally uses a generic "authenticated user" rule on SecurityOperationsService or WorkOrderService.

## Required Mitigation

Business endpoints require capability policies, not merely `RequireAuthenticatedUser()`.

CredentialAdministrator is denied from Security Operations and WorkOrder business APIs.

## Expected Result

```text
403
```

---

# 17. Threat: Unauthorized Credential Issuance

## Scenario

A wrong-role user calls:

```text
POST /api/v1/people/{personId}/credentials
```

## Required Mitigation

Authorize `CredentialAdmin` before mutation.

Then preserve existing CredentialService business validation:

```text
Person exists
Person is active
CredentialNumber unique
ExpiresAt > IssuedAt
AccessLevel valid
```

## Expected Result

Unauthorized callers must not create any database state.

---

# 18. Threat: Unauthorized or Altered Credential Revocation

## Scenario

A Technician or other denied principal tries to revoke a badge, or a caller repeatedly revokes a credential with a different reason.

## Required Mitigation

Require `CredentialAdmin`.

Preserve CredentialService's existing domain rule:

```text
revocation is terminal
repeated revoke is idempotent
original RevokedAt preserved
original RevocationReason preserved
```

Authorization must happen before mutation.

---

# 19. Threat: Token or Secret Leakage in Logs

## Scenario

Authentication diagnostics log:

```text
Authorization: Bearer <token>
access token
refresh token
ID token
Cognito secret
AWS credentials
connection string
```

## Required Mitigation

Never log raw token values.

Safe contextual logging may include:

```text
correlation ID
Cognito subject
resolved policy/role
endpoint
resource ID
authorization result
```

Authentication failure diagnostics must remain useful without exposing credentials.

---

# 20. Threat: Secret Leakage in Configuration or Source Control

## Required Mitigation

Tracked configuration must not contain:

```text
real Neon credentials
real AWS credentials
Cognito client secrets
real JWTs
refresh tokens
```

Use environment variables, .NET User Secrets, or deployment secret stores for real credentials.

Public identifiers such as user-pool IDs or public app-client IDs may be configuration values when appropriate, but secrets must remain separate.

Do not create a client secret for a browser/public client when the selected Cognito flow does not require one.

---

# 21. Threat: Overly Broad CORS

## Scenario

Production API uses:

```text
AllowAnyOrigin
```

or otherwise accepts arbitrary web origins.

## Required Mitigation

Use the configuration-driven CORS pattern already established in Vision.

Development:

```text
http://localhost:3000
```

Production:

```text
explicit deployed frontend origin(s)
```

CORS does not replace authentication/authorization.

A non-browser client can still call the API, so backend policies remain mandatory.

---

# 22. Threat: Frontend Token Exposure

## Security Requirement

The implementation must minimize unnecessary token exposure.

Review:

```text
where access tokens are held
whether refresh tokens are exposed to application JavaScript
whether tokens are persisted longer than necessary
whether tokens appear in URLs
whether tokens appear in console logging
whether tokens are copied into server-rendered output
```

Never put bearer tokens in:

```text
URL query strings
application logs
analytics events
error messages
HTML rendered for unrelated clients
```

The selected Cognito/Next.js integration should use the established library/session mechanism consistently rather than hand-rolling token persistence.

---

# 23. Threat: Logout Misunderstood as Immediate Token Revocation

## Scenario

User clicks Logout, but a previously issued JWT remains independently valid until its expiry unless explicitly revoked/invalidated by the selected Cognito mechanism.

## Required Security Behavior

Logout must:

```text
end the frontend authenticated session
clear local/session auth state controlled by the application/library
prevent continued normal UI access
```

Testing should not assume that deleting browser state cryptographically changes an already issued JWT's expiry.

For the MVP, short normal token lifetimes plus correct session/logout handling are sufficient unless the implementation explicitly adds Cognito token revocation.

---

# 24. Threat: 401 / 403 Confusion

Use:

```text
401
    authentication failed / no accepted principal

403
    principal authenticated but not permitted
```

Examples:

```text
no token -> 401
invalid signature -> 401
expired token -> 401
wrong issuer -> 401
wrong token type -> 401
unapproved app client -> 401

Technician -> CredentialService -> 403
SecurityManager -> Technician-only action -> 403
CredentialAdministrator -> WorkOrders -> 403
Technician -> another Technician's WorkOrder -> 403
```

Do not use 401 to hide ordinary role-denial behavior already defined by the authorization contract.

---

# 25. Threat: Authorization Happens After Mutation

## Scenario

Handler mutates entity state and only later discovers caller is not allowed.

## Required Mitigation

For business mutations:

```text
authenticate
authorize capability
authorize resource ownership if needed
then execute business mutation
then persist
```

No unauthorized request may leave partial state behind.

---

# 26. Threat: Cross-Service Identity Coupling

Do not merge:

```text
Cognito user
CredentialService.Person
WorkOrderService.Technician
```

These have distinct responsibilities.

Allowed mapping:

```text
Cognito sub
    ↓
WorkOrderService.Technician.CognitoSubject
```

for Technician ownership.

Do not add a cross-service EF navigation or shared `Employee` aggregate.

CredentialService.Person remains a business record for physical credentials, not the login user table.

---

# 27. Threat: Health Endpoint Leaks Internals

`GET /health` may remain anonymous for infrastructure probes.

The response must not reveal:

```text
connection strings
database credentials
AWS credentials
JWT configuration secrets
stack traces
internal exception details
```

Keep the anonymous surface minimal.

---

# 28. Threat: OpenAPI / Swagger Accidentally Bypasses Security

Swagger/OpenAPI may document bearer authentication and allow a developer to supply a token interactively.

It must not:

```text
hard-code a real token
ship test credentials
add an authorization bypass
disable API policies in development
```

Development convenience must exercise the same backend authorization path.

---

# 29. Threat: SQS Consumer Confused with Human Authorization

The WorkOrder SQS consumer is not a Cognito user.

Do not require an end-user bearer token on the existing SQS integration path.

Existing controls remain:

```text
event contract validation
idempotency by SourceEventId and SecurityIncidentId
transactional persistence
retry / visibility behavior
DLQ behavior
correlation
```

Human authorization applies to HTTP business endpoints, not message delivery.

---

# 30. Abuse Cases to Test Explicitly

```text
1. Fabricated JWT with SecurityManager claim.
2. Valid JWT from wrong Cognito user pool.
3. Correct user pool, wrong app client.
4. ID token sent as API access token.
5. Expired access token.
6. Technician calls CredentialService.
7. CredentialAdministrator calls WorkOrderService.
8. SecurityManager calls /start, /notes, /complete.
9. Technician lists WorkOrders using another technicianId.
10. Technician requests another Technician's WorkOrder by ID.
11. Technician mutates another Technician's WorkOrder.
12. Technician-group user has no Technician.CognitoSubject mapping.
13. Revocation request from wrong role.
14. Authorized repeated revocation preserves original data.
15. Browser sends request from unapproved production origin.
16. Authentication failure path inspected for token leakage.
17. Problem Details response inspected for secret/internal leakage.
18. Logout removes normal frontend session access.
19. Health endpoint inspected for sensitive information.
20. Frontend role hiding bypassed with direct HTTP request; API still denies.
```

---

# 31. Security Review Gates

Phase 5 cannot be approved until all of these are true:

- JWT signature validation is enabled.
- Cognito issuer/user pool is validated.
- APIs use Cognito access tokens for authorization.
- Token type is validated.
- Intended Cognito app-client context is validated.
- Cognito groups are mapped deliberately to policies.
- `SecurityManager` and `CredentialAdministrator` both satisfy `CredentialAdmin`.
- `Technician` does not satisfy `CredentialAdmin`.
- SecurityManager cannot satisfy Technician repair operations merely because it is the demo role.
- Technician ownership uses authenticated subject-to-Technician mapping.
- Technician list queries cannot be widened by client-supplied technician ID.
- Wrong authenticated role returns 403.
- Invalid/expired/untrusted token returns 401.
- Production CORS is explicit.
- No raw JWTs or secrets are logged.
- CredentialService.Person remains separate from Cognito identity.
- Technician remains separate from CredentialService.Person.
- `/health` remains minimal if anonymous.
- The repair-to-security-resolution flow respects role separation.
- Authorization integration tests exercise the actual production policy path.

---

# 32. Implementation Scope Guardrails

Do not introduce for Phase 5:

```text
authorization microservice
custom permissions database
Amazon Verified Permissions
Cedar
custom identity provider
local ASP.NET Identity user tables
shared Employee aggregate
service-to-service user impersonation
workflow engine
token introspection service
```

These are unnecessary for the approved MVP.

Use:

```text
Cognito
JWT bearer authentication
small ASP.NET Core policy set
resource ownership checks
existing service boundaries
```

---

# 33. Source-Derived vs Implementation-Reference Notes

The Vision role boundaries, service ownership, CredentialService security requirements, CORS expectations, 401/403 contract, and credential-domain rules in this document come from the Phase 5 handoff, the CredentialService specification, and the Phase 5 authorization matrix.

The Cognito-specific token-validation cautions in this document are implementation guidance added for Phase 5 review. Current Amazon Cognito documentation distinguishes access tokens from ID tokens, identifies `cognito:groups` as a token claim for user-pool groups, and requires validation of token signature, issuer, expiry, and app-client context. ASP.NET Core JWT bearer authentication likewise distinguishes authentication failure from authorization denial.

If Kiro's selected Cognito library/configuration behaves differently in a material way, review the concrete implementation rather than weakening the Vision authorization contract.
