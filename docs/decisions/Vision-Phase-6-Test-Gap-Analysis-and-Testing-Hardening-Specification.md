# Vision — Phase 6 Test-Gap Analysis and Testing-Hardening Specification

**Project:** Vision  
**Phase:** 6 — Observability, Testing, and CI  
**Artifact type:** Kiro implementation specification / test-hardening target  
**Status:** Ready for implementation  

---

## 1. Purpose

This document defines the testing work that must be completed during Vision Phase 6 before the testing-hardening portion of the phase can be approved.

Vision already has substantial automated test infrastructure from Phases 3–5. Phase 6 must **not** replace that work, reorganize the repository purely for aesthetics, or chase a meaningless global coverage percentage.

The goal is to close the **highest-value remaining gaps** around:

- business-critical domain behavior;
- security and authorization regressions;
- asynchronous integration boundaries;
- the five-minute demo path;
- observability behavior introduced in Phase 6;
- container/health/configuration behavior;
- the frontend's highest-risk authentication and API-client behavior.

The required outcome is a codebase whose most important behavior can be continuously verified by local execution and GitHub Actions.

---

## 2. Testing Philosophy for Phase 6

Kiro should optimize for **risk reduction**, not test count.

Prioritize tests that protect:

1. business invariants;
2. authorization boundaries;
3. cross-service and messaging contracts;
4. failure windows and idempotency;
5. the primary demo workflow;
6. production-shaped configuration and health behavior.

Do not add tests merely because a line or branch is uncovered.

No numeric code-coverage percentage is required for Phase 6 approval.

Coverage tooling may remain available for developer visibility, but a coverage threshold must not become the primary definition of correctness.

---

# 3. Current Repository Baseline

The repository already has a strong test base in several areas.

## 3.1 CredentialService — strong existing coverage

The current suite already covers, among other areas:

- credential status calculation;
- credential revocation behavior and idempotency;
- credential issuance;
- duplicate credential-number conflict handling;
- inactive/unknown person handling;
- people queries and filters;
- credential queries and filters;
- pagination;
- expiring-soon boundaries;
- summary counts;
- PostgreSQL uniqueness/schema behavior;
- seed idempotency;
- API behavior;
- SecurityManager and CredentialAdministrator authorization;
- Technician denial;
- production-faithful JWT validation cases including issuer, signature, expiry, client ID, and token-use validation.

CredentialService therefore needs **targeted regression additions only**.

## 3.2 WorkOrderService — strong existing coverage

The current suite already covers, among other areas:

- work-order lifecycle transitions;
- technician-note ownership behavior;
- PostgreSQL persistence constraints;
- unique `SecurityIncidentId` and `SourceEventId` rules;
- manual duplicate incident conflicts;
- authorization by role;
- technician ownership;
- client-supplied technician filters not widening access;
- state remaining unchanged after denied mutations;
- missing Cognito-subject mapping;
- SQS transport behavior;
- visibility/redelivery behavior;
- DLQ redrive behavior;
- consumer failure windows;
- production-path consumer behavior;
- duplicate delivery idempotency;
- event-contract validation;
- correlation-ID preservation;
- outbox publishing success/failure behavior.

WorkOrderService therefore needs **selective regression and observability additions only**.

## 3.3 SecurityOperationsService — major behavioral gap

The SecurityOperationsService test project currently focuses almost entirely on authorization.

Existing coverage includes:

- unauthenticated 401 behavior;
- SecurityManager read/mutation authorization;
- Technician denial;
- CredentialAdministrator denial;
- no-approved-role denial;
- positive SecurityManager asset-status mutation;
- positive SecurityManager incident creation;
- non-auth validation response behavior;
- static health endpoint access.

However, there is currently no dedicated SecurityOperations domain/application/integration suite comparable to the CredentialService and WorkOrderService suites.

Important business logic is therefore protected indirectly or not at all.

This is the **largest Phase 6 backend test gap**.

## 3.4 SecurityOperations outbox tests exist, but live in WorkOrderService.Tests

Transactional-outbox tests currently exist under `WorkOrderService.Tests` and reference `SecurityOperationsService` directly.

Those tests already protect:

- Critical + asset qualification;
- non-Critical suppression;
- Critical without asset suppression;
- stable event ID;
- publication state tracking;
- publisher failure/success behavior.

These tests are valuable and must remain passing.

Kiro does **not** need to move them merely for organizational purity during Phase 6. Test relocation is optional and should occur only if it materially improves maintainability without creating churn.

## 3.5 Frontend — no automated test foundation

The Next.js frontend currently defines:

- `dev`;
- `build`;
- `start`;
- `lint`;

but has no automated frontend-test script or test framework.

This is a meaningful Phase 6 gap because the frontend now contains security-sensitive behavior around:

- OAuth state validation;
- PKCE verifier handling;
- access-token session storage;
- Bearer-token attachment;
- 401-triggered session clearing;
- role-aware action visibility.

Phase 6 should add a **small, focused frontend test foundation**, not an extensive UI test program.

---

# 4. Priority Model

Use the following priority levels.

## P0 — Required for Phase 6 approval

These tests protect core business/security/operational behavior and must be implemented.

## P1 — Strongly recommended

Implement these unless they become disproportionately expensive or duplicate equivalent coverage.

## P2 — Optional polish

Useful, but not required for Phase 6 approval.

Kiro should not spend substantial credits/time on P2 work while P0/P1 gaps remain.

---

# 5. P0 — SecurityOperations Domain Tests

Create a dedicated domain-test area in `SecurityOperationsService.Tests`.

Suggested structure:

```text
tests/SecurityOperationsService.Tests/
    Domain/
        SecurityIncidentTests.cs
        SecurityAssetTests.cs
```

Equivalent organization is acceptable.

## 5.1 SecurityIncident lifecycle

At minimum, cover:

### Start investigation

```text
Open -> Investigating
```

Verify:

- status becomes `Investigating`;
- `UpdatedAt` advances appropriately.

### Investigation idempotency

Calling `StartInvestigation()` when already `Investigating` must:

- remain `Investigating`;
- not throw;
- avoid corrupting state.

### Resolved incident cannot reopen investigation

```text
Resolved -> Investigating
```

must throw `InvalidOperationException` and preserve resolved state.

### Resolve valid incident

Resolving an Open or Investigating incident with a valid summary must:

- set `Status = Resolved`;
- set `ResolutionSummary`;
- set `ResolvedAt`;
- update `UpdatedAt`.

### Resolve requires a meaningful summary

Blank/whitespace resolution summary must fail when the incident is not already resolved.

### Resolve is idempotent

Resolving an already-resolved incident again must preserve the original:

- `ResolvedAt`;
- `ResolutionSummary`;
- resolved status.

This protects the Phase 2 idempotency correction from regression.

## 5.2 Work-order attachment invariant

Cover `AttachWorkOrder`:

- first valid WorkOrder ID attaches successfully;
- same WorkOrder ID can be attached again idempotently;
- empty GUID is rejected;
- a different WorkOrder ID cannot overwrite an existing association.

This protects another earlier corrected domain invariant.

## 5.3 Automatic-work-order qualification

Cover the domain property/rule:

```text
Severity == Critical
AND
SecurityAssetId != null
```

Required cases:

- Critical + asset -> true;
- Critical + no asset -> false;
- High + asset -> false.

## 5.4 SecurityAsset status behavior

Cover `ChangeStatus`:

- changing to a different status updates `Status`, `StatusChangedAt`, and `UpdatedAt`;
- changing to the current status is idempotent and does not unnecessarily mutate timestamps.

---

# 6. P0 — SecurityOperations Application/API Behavior

Create focused application or API integration tests for behavior that authorization tests currently do not establish.

Do not duplicate every query permutation found in CredentialService.

## 6.1 Positive SecurityManager incident-status PATCH

The Phase 5 handoff identified one known non-blocking polish gap:

```text
SecurityManager PATCH incident status -> success
```

Add this in Phase 6.

At minimum verify:

- authenticated SecurityManager can transition a seeded incident to `Investigating`;
- the API returns success;
- persisted status changes.

Also test resolution if not otherwise covered at the API level:

- valid resolution summary -> `Resolved`;
- `ResolvedAt` is populated;
- response reflects the persisted state.

## 6.2 Invalid incident transition does not corrupt persisted state

At least one API/application test must demonstrate that an invalid transition, such as:

```text
Resolved -> Investigating
```

fails and leaves stored state unchanged.

## 6.3 Incident creation location/asset consistency

The handler contains meaningful cross-entity validation.

Cover:

- unknown location -> client error;
- unknown asset -> client error;
- asset belonging to a different location -> client error;
- valid location + matching asset -> success.

The final HTTP status should match the service's established exception mapping; test the externally observable behavior rather than internal exception wording where possible.

## 6.4 Query validation regression

At minimum verify API-level 400 behavior for invalid enum/pagination inputs that matter to the UI:

### Assets

- invalid status;
- invalid asset type;
- page < 1;
- pageSize > 100.

### Incidents

- invalid status;
- invalid severity;
- page < 1;
- pageSize > 100.

These may be theory-driven tests to avoid repetition.

## 6.5 Dashboard business truth

The dashboard is the opening screen of the five-minute demo and needs direct behavioral coverage.

Add integration/application tests that verify at least:

- total/Operational/Degraded/Offline asset counts are internally consistent;
- `OperationalPercentage` is calculated correctly;
- resolved incidents are excluded from active counts;
- active Critical incidents contribute to `ActiveCritical`;
- Critical alerts contain only active Critical incidents;
- Critical alerts are newest-first and capped at the configured limit;
- recent activity uses truthful creation/resolution timestamps rather than fabricated activity timestamps.

Do not assert every seeded row unless necessary. Prefer small deterministic fixture data for calculation tests when practical.

## 6.6 Asset/incident query smoke behavior

Add enough query integration coverage to protect the principal filters used by the UI.

Required minimum:

### Assets

- status filter;
- type filter;
- search by a supported visible field;
- pagination.

### Incidents

- status filter;
- severity filter;
- asset filter;
- search;
- pagination.

Do not attempt exhaustive Cartesian-product testing of all filters.

---

# 7. P0 — Transactional Outbox and Messaging Regression

The existing messaging suite is strong. Phase 6 should **extend rather than rewrite it**.

The current tests must remain passing.

## 7.1 Preserve existing requirements

Regression coverage must continue to prove:

- Critical + asset creates an outbox record;
- nonqualifying incidents do not create outbox records;
- event ID remains stable across retries;
- send failure does not mark the outbox record published;
- successful send marks publication;
- DB failure does not delete the SQS message;
- DB commit occurs before message deletion;
- redelivery remains idempotent;
- malformed/permanent-invalid contracts eventually reach the DLQ according to configured redrive behavior.

## 7.2 Add OpenTelemetry trace-context coverage

After the Phase 6 observability implementation, extend the existing outbox/messaging tests to verify the requirements in the Phase 6 Observability Acceptance Criteria.

At minimum cover:

### Outbox trace persistence

When a qualifying incident is created under a valid current W3C activity:

- the outbox row stores the originating trace context required by the observability specification.

### No-parent fallback

When incident creation occurs without a current Activity:

- incident creation still succeeds;
- outbox creation still succeeds;
- missing trace context does not break publication.

### Producer injection

When the outbox publisher sends to SQS:

- expected W3C trace attributes are injected when context exists;
- existing `CorrelationId` and `EventType` attributes remain present.

### Consumer extraction

When WorkOrderService receives a message carrying valid trace context:

- consumer processing continues the distributed trace as specified.

### Malformed trace context

Malformed/missing trace metadata must:

- not turn an otherwise valid business message into a poison message solely because tracing metadata is invalid;
- allow processing under a new trace/fallback context;
- preserve business idempotency.

---

# 8. P0 — Authorization Regression Preservation

Phase 6 observability/containerization must not weaken Phase 5 security behavior.

The existing authorization suites must remain enabled and passing.

Do not replace production-faithful JWT tests with only a fake authentication handler.

At minimum, CI must continue executing tests that establish:

## SecurityOperationsService

- no token -> 401;
- SecurityManager -> authorized;
- Technician -> 403;
- CredentialAdministrator -> 403;
- no approved role -> 403.

## WorkOrderService

- SecurityManager supervisory access;
- SecurityManager denied Technician-only repair actions;
- Technician own-only access;
- other Technician's work -> 403;
- client-provided technician ID cannot widen access;
- missing CognitoSubject mapping fails closed;
- denied mutations leave persistence unchanged.

## CredentialService

- SecurityManager and CredentialAdministrator access;
- Technician denial;
- no token -> 401;
- invalid signature/issuer/client ID/token-use/expiry -> 401;
- valid token without approved group -> 403.

---

# 9. P1 — WorkOrderService Selective Hardening

The WorkOrder suite is already extensive. Avoid broad duplication.

Add only high-value gaps discovered while implementing Phase 6.

Recommended targets:

## 9.1 Validation/API regression for manager actions

Ensure API-level validation exists for important manager/technician commands where invalid inputs could otherwise surface as 500s.

Prioritize:

- assign unknown/inactive technician;
- add blank Technician note;
- complete with neither completion summary nor prior note;
- invalid list enum/pagination parameters.

If equivalent tests already exist after Kiro's inspection, do not duplicate them.

## 9.2 Work-order summary correctness

Add a deterministic test for summary counts if no equivalent test exists.

The summary drives management UI and should correctly distinguish the service's WorkOrder statuses.

## 9.3 Seed idempotency

If WorkOrder seed behavior is not currently explicitly tested for repeated execution, add a small seed-idempotency test similar in spirit to CredentialService.

Do not create broad seed snapshot tests.

---

# 10. P1 — CredentialService Selective Hardening

CredentialService already has the strongest behavioral suite.

Only add tests for concrete uncovered risks found during implementation/review.

Recommended candidates:

## 10.1 Configuration/auth startup failure

If Phase 6 introduces centralized configuration validation for authentication/observability, add a test that verifies obviously invalid required production configuration fails predictably rather than silently weakening authentication.

Do not unit-test the .NET configuration binder itself.

## 10.2 Health/readiness behavior

CredentialService should receive the same health-check behavior tests as the other services after Phase 6 health standardization.

No broad new Credential domain/query suite is required.

---

# 11. P0 — Frontend Test Foundation

Add a small automated test framework to the Next.js frontend.

A conventional lightweight choice such as:

```text
Vitest
+ jsdom
+ React Testing Library where component rendering is needed
```

is appropriate.

Equivalent modern tooling is acceptable if compatible with the current Next.js/React versions.

Do not introduce multiple overlapping frontend test frameworks.

Add a script such as:

```text
npm test
```

or:

```text
npm run test
```

that can run non-interactively in CI.

## 11.1 API-client authentication behavior

At minimum test:

### Bearer token attachment

When a token exists in `tokenStore`:

```text
Authorization: Bearer <token>
```

is attached to backend requests.

### No fabricated token

When no token exists, no Authorization header is invented.

### 401 handling

When any Vision backend returns 401:

- the session-expired handler is notified;
- stale frontend authentication state can be cleared.

Test this for the shared behavior; it is not necessary to duplicate the same assertion for every API function if the helpers share the same implementation.

### API error propagation

Non-2xx responses should produce `ApiError` with the HTTP status and useful server-provided `detail`/`title` when present.

## 11.2 OAuth/PKCE critical behavior

Add focused tests around the highest-risk callback behavior.

Required:

- mismatched/missing OAuth `state` is rejected;
- missing PKCE verifier is rejected;
- successful callback stores the access token/session after a successful mocked token exchange;
- logout/session-expiry clears stored authentication state.

Do not test Cognito itself. Mock the browser/network boundary.

## 11.3 Role-aware UI action smoke tests

Add a minimal set of component/page-level tests proving the most important permission split visible to users.

Required minimum:

### SecurityManager work-order view

Can see supervisory actions such as:

- Assign Technician;
- Finish Security Resolution.

Must not be presented Technician-only repair actions as available actions.

### Technician work-order view

Can see appropriate repair actions such as:

- Start Work;
- Add Repair Note;
- Complete Work;

Must not be presented SecurityManager supervisory actions as available actions.

### Credential administration

SecurityManager and CredentialAdministrator should have credential administration UI access; Technician should not.

Frontend tests are UX regression tests only. Backend authorization remains authoritative.

---

# 12. P1 — Primary Demo Path Regression Test

Vision's portfolio value depends heavily on one coherent five-minute demo.

Add one practical automated regression test that exercises as much of the critical business path as is reasonable **without creating a fragile full-cloud E2E harness**.

Preferred scope is backend/integration level rather than Cognito/browser/cloud E2E.

The test should establish the core chain:

```text
Critical incident for Pharmacy Storage camera
    ↓
outbox event created
    ↓
IncidentCreated event processed by WorkOrderService
    ↓
WorkOrder created exactly once
```

If practical within the existing test infrastructure, continue through key WorkOrder lifecycle state transitions.

Do not force all three services into one EF transaction or violate service boundaries for the test.

A staged component/integration test using the real event contract and real service persistence behavior is acceptable.

The purpose is regression confidence in the demo story, not an artificial distributed transaction.

---

# 13. P0 — Health-Check Tests

The Phase 6 observability specification replaces static health JSON with proper ASP.NET Core health checks.

Each backend service must have automated coverage for its health behavior.

At minimum verify:

## Liveness

- liveness endpoint is reachable without authentication;
- healthy process returns success;
- it does not require Cognito credentials or user authentication.

## Readiness

If a separate readiness endpoint is implemented:

- readiness returns healthy when required dependencies/configuration are healthy;
- an unavailable required dependency causes readiness to report unhealthy/degraded according to the approved design;
- liveness should not fail merely because PostgreSQL/SQS is temporarily unavailable unless the observability specification explicitly requires that behavior.

Tests should protect the semantic difference between **process alive** and **ready to serve work**.

Do not assert exact framework-generated JSON unless the response format is intentionally part of Vision's contract.

---

# 14. P0 — Configuration Failure Tests

Phase 6 adds more environment-driven configuration for:

- OpenTelemetry;
- OTLP export;
- service identity;
- messaging;
- database connections;
- container execution.

Add targeted startup/configuration tests for failure cases that could otherwise produce insecure or misleading operation.

At minimum establish:

- missing optional OTLP endpoint does **not** prevent application startup when local export is intentionally disabled;
- invalid observability configuration does not disable core business processing silently;
- missing required service configuration fails in a clear/actionable way where the application already treats that value as required;
- no test requires real AWS, Neon, or Cognito secrets.

Do not create exhaustive tests for every environment variable.

---

# 15. P1 — Observability Instrumentation Tests

OpenTelemetry should be tested at the seams where Vision adds custom behavior.

Do not attempt to unit-test OpenTelemetry's built-in ASP.NET Core instrumentation package.

Focus on Vision-owned code.

Required custom observability behavior is already covered in Section 7.

Additional recommended tests:

- configured resource emits the expected `service.name` for each backend;
- custom messaging producer/consumer ActivitySource names are stable;
- correlation ID remains distinct from and available alongside TraceId;
- request with caller-supplied valid `X-Correlation-ID` preserves it;
- request without one receives/generated correlation ID;
- sensitive values are not intentionally added as Activity tags/logging properties by custom instrumentation.

For the last item, prefer review plus narrow tests around Vision-owned enrichers rather than attempting global log-content inspection.

---

# 16. P0 — Test Isolation and Determinism

Phase 6 must not make the suite flaky.

## 16.1 PostgreSQL tests

Integration tests must use deterministic database setup/cleanup.

Parallel execution must not cause separate test classes to delete or mutate the same database unexpectedly.

Existing collection fixtures may be extended rather than replaced.

## 16.2 LocalStack tests

Messaging tests must:

- create/use predictable test queues;
- clean up messages/queues where appropriate;
- not depend on developer AWS credentials;
- not reach real AWS.

## 16.3 Time-sensitive tests

Avoid brittle assertions such as exact equality with `UtcNow`.

Use ranges, captured-before/captured-after values, or injected time abstractions only where genuinely necessary.

Do not introduce a project-wide clock abstraction solely to satisfy a few simple timestamp assertions.

## 16.4 Random IDs

Random GUIDs are acceptable for isolation where no deterministic cross-service seed identity is required.

Use fixed IDs where the demo story or cross-service contract intentionally depends on stable seeded IDs.

---

# 17. P0 — Test Categories Must Be Runnable in CI

The final test suite should have a clear CI execution story.

At minimum, Kiro must ensure the repository can distinguish or consistently provision the dependencies needed by:

```text
fast unit/application tests
PostgreSQL-backed integration tests
LocalStack/SQS integration tests
frontend tests
```

This can be accomplished through:

- xUnit collections/traits;
- project-level separation;
- CI service containers;
- deterministic Docker Compose dependencies;
- another simple documented mechanism.

Do not introduce a complex bespoke test orchestrator.

Tests must not pass locally only because a developer happens to have unrelated services or secrets already configured.

---

# 18. CI-Facing Required Commands

Before Phase 6 closes, there must be deterministic commands suitable for GitHub Actions.

Backend baseline:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Exact optimization flags may differ once Kiro designs CI.

Frontend baseline:

```bash
cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

If the selected test script is named differently, document and use that consistently.

PostgreSQL and LocalStack integration dependencies must be provisioned by CI rather than assumed to exist externally.

---

# 19. Tests Explicitly Not Required in Phase 6

Do not expand scope into the following unless a concrete defect justifies it:

- full Selenium-style browser automation across every screen;
- testing Cognito's hosted UI itself;
- real AWS integration from CI;
- real Neon integration from CI;
- Azure Container Apps deployment tests;
- visual-regression screenshot infrastructure;
- load/performance test platform;
- mutation testing;
- contract-testing platform such as Pact unless already justified elsewhere;
- generalized test-data-builder framework;
- snapshot tests for large API responses;
- a mandatory global coverage threshold;
- broad reorganization of all existing test projects.

Deployment/performance infrastructure belongs primarily to later phases.

---

# 20. Suggested Implementation Order for Kiro

Kiro should implement test hardening in this order.

```text
1. Add SecurityOperations domain tests.

2. Add SecurityOperations application/API/dashboard/query tests.

3. Add the missing positive SecurityManager incident-status PATCH test.

4. Add the minimal frontend test framework and auth/API-client tests.

5. Implement OpenTelemetry/health work from the separate observability specification.

6. Add the trace-context/outbox/SQS observability tests.

7. Add health/readiness/configuration tests.

8. Add only the highest-value WorkOrder/Credential P1 gaps still confirmed missing.

9. Add the practical primary-demo-path regression test.

10. Run the entire backend + frontend suite repeatedly to identify isolation/flakiness problems before CI implementation.
```

This order front-loads pre-existing behavioral gaps while leaving instrumentation-specific tests until the instrumentation exists.

---

# 21. Phase 6 Testing Acceptance Criteria

The testing-hardening portion of Phase 6 is acceptable when all of the following are true.

## Existing suite

- [ ] All Phase 3–5 backend tests remain enabled and passing.
- [ ] Production-faithful JWT validation tests remain part of the suite.
- [ ] Existing PostgreSQL, SQS, outbox, retry, idempotency, and authorization tests remain meaningful and are not weakened merely to make CI pass.

## SecurityOperationsService

- [ ] SecurityIncident domain lifecycle is directly tested.
- [ ] `AttachWorkOrder` invariants are directly tested.
- [ ] automatic-work-order qualification is directly tested.
- [ ] SecurityAsset status idempotency/change behavior is directly tested.
- [ ] SecurityManager positive incident-status PATCH behavior is tested.
- [ ] invalid incident transitions preserve stored state.
- [ ] incident location/asset consistency validation is tested.
- [ ] important list validation cases return 400.
- [ ] dashboard counts/critical-alert/recent-activity behavior is directly tested.
- [ ] principal asset/incident filters and pagination have integration coverage.

## Messaging and tracing

- [ ] Existing outbox and consumer failure-window tests still pass.
- [ ] outbox trace context is tested when a parent Activity exists.
- [ ] absence of parent trace context does not break business processing.
- [ ] producer trace context is injected into SQS.
- [ ] consumer trace context is extracted/continued.
- [ ] malformed tracing metadata falls back safely without poisoning an otherwise valid business event.
- [ ] existing Vision `CorrelationId` remains preserved.

## Frontend

- [ ] A CI-capable frontend test framework exists.
- [ ] Bearer token attachment is tested.
- [ ] 401 session-expiration behavior is tested.
- [ ] API error propagation is tested.
- [ ] OAuth state validation is tested.
- [ ] PKCE verifier requirement is tested.
- [ ] successful mocked callback/session storage is tested.
- [ ] logout/session clearing is tested.
- [ ] SecurityManager vs Technician WorkOrder action visibility has smoke coverage.
- [ ] credential-administration role visibility has smoke coverage.

## Health/configuration

- [ ] Each backend liveness endpoint has automated coverage.
- [ ] readiness semantics are tested if readiness is implemented separately.
- [ ] optional telemetry export being absent does not unnecessarily break startup.
- [ ] tests do not require real cloud credentials or secrets.

## Integration/demo

- [ ] A practical regression test protects the Critical incident -> event -> WorkOrder creation chain.
- [ ] duplicate processing remains idempotent.

## Reliability

- [ ] PostgreSQL-backed tests are deterministic.
- [ ] LocalStack-backed tests are deterministic.
- [ ] test parallelism does not cause shared-database destruction/races.
- [ ] test commands run non-interactively.
- [ ] repeated full-suite execution does not reveal known flaky tests.

---

# 22. Definition of Done for Kiro's First Phase 6 Implementation Batch

For the first implementation handoff back to ChatGPT, Kiro should provide:

1. OpenTelemetry implementation required by `Vision-Phase-6-Observability-Acceptance-Criteria.md`.
2. All P0 testing-hardening work that is practical before Docker/CI.
3. The minimal frontend test framework and required P0 frontend tests.
4. Updated health-check implementation and associated tests.
5. Any database migration required for outbox trace metadata.
6. Updated package/configuration files.
7. Test execution results.

Kiro should run and report:

```bash
docker compose up -d

dotnet build
dotnet test

cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

If Kiro adds a different test script name, report the exact command used.

The project owner should then package the repository and return the updated snapshot to ChatGPT for independent review before Docker/Compose and CI work proceeds deeply.

---

# 23. Review Guidance for ChatGPT

When reviewing Kiro's testing-hardening implementation, prioritize:

- whether new tests actually assert business behavior rather than implementation details;
- whether SecurityOperations has reached a reasonable parity of risk coverage with the other services;
- whether existing authorization/JWT tests were weakened;
- whether tracing tests validate Vision-owned propagation logic rather than OpenTelemetry internals;
- whether frontend tests protect the authentication/role behavior most likely to break the demo;
- whether PostgreSQL/LocalStack tests are deterministic and CI-compatible;
- whether health checks reflect real liveness/readiness semantics;
- whether tests accidentally require developer-local secrets;
- whether Kiro introduced unnecessary frameworks or abstractions;
- whether the primary demo path is better protected after the changes.

Phase 6 approval should be based on **confidence in critical behavior**, not the raw number of tests added.

---

# 24. Final Testing Principle

Vision Phase 6 should leave behind a test suite that tells a credible engineering story:

```text
Domain rules are protected
        ↓
Authorization boundaries are protected
        ↓
Database behavior is verified against PostgreSQL
        ↓
Messaging failure windows are verified
        ↓
Distributed trace propagation is verified
        ↓
Frontend auth behavior is guarded
        ↓
Health/configuration behavior is operationally meaningful
        ↓
CI can verify the repository repeatedly
```

That is the standard for Phase 6 testing hardening.
