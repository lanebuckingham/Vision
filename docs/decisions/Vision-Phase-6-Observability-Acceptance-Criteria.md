# Vision — Phase 6 Observability Acceptance Criteria

**Project:** Vision  
**Phase:** 6 — Observability, Testing, and CI  
**Artifact type:** Kiro implementation specification / acceptance criteria  
**Status:** Ready for implementation  

---

## 1. Purpose

This document defines the observability work that must be implemented and demonstrated before the observability portion of Vision Phase 6 can be approved.

Vision already has meaningful logging, a durable application-level correlation ID in the Security Operations → outbox → SQS → Work Order path, and simple `/health` endpoints. Phase 6 should build on those mechanisms rather than replace them.

The goal is a small, production-shaped observability foundation that demonstrates senior-level engineering judgment while remaining appropriate for a cost-sensitive portfolio MVP.

The implementation must provide:

- OpenTelemetry distributed tracing across the three backend services.
- Trace continuity across normal HTTP boundaries.
- Trace continuity across the asynchronous transactional-outbox + Amazon SQS boundary.
- Preservation of Vision's existing application-level correlation ID.
- Structured `ILogger` logging correlated with trace/span context.
- Useful health endpoints for container orchestration.
- Safe telemetry that does not expose tokens, secrets, credential details, or unnecessary PII.
- Environment-driven exporter configuration that works locally and remains compatible with future Azure Container Apps deployment.

This phase must **not** introduce an expensive monitoring platform or turn observability into a separate infrastructure project.

---

## 2. Collaboration Boundary

### ChatGPT owns

- Observability architecture.
- Acceptance criteria.
- Review of Kiro's implementation.
- Logging/tracing review.
- Identification of follow-up findings.

### Kiro owns

- Repository changes.
- NuGet/package updates.
- OpenTelemetry registration.
- Activity creation and propagation code.
- Health-check implementation.
- Configuration changes.
- Tests.
- Documentation changes required by this specification.
- Building and running the repository.

Kiro should implement this specification directly in the existing architecture. Do not create a competing observability architecture unless a concrete incompatibility is discovered.

---

## 3. Current Repository Baseline

The current Phase 5-approved codebase already contains the following useful foundations.

### 3.1 OpenTelemetry packages already referenced

Each backend service currently references:

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`

However, OpenTelemetry is not yet wired into the three applications.

### 3.2 Existing Vision correlation model

`SecurityOperationsService` already:

- accepts or creates `X-Correlation-ID` per incoming request;
- stores it in a scoped `CorrelationContext`;
- returns the correlation ID in the HTTP response;
- stores the correlation ID in the transactional outbox;
- includes it in the `IncidentCreatedV1` integration event;
- publishes it as an SQS message attribute.

`WorkOrderService` preserves the integration event's correlation ID on the resulting `WorkOrder`.

This behavior is intentional and must remain intact.

### 3.3 Existing logging

The backend uses `Microsoft.Extensions.Logging` / `ILogger<T>` with structured message templates in important paths, including:

- incident creation;
- outbox publishing;
- SQS receive/processing;
- duplicate handling;
- work-order creation;
- errors and retry behavior.

Do not replace `ILogger` with another logging framework during Phase 6.

### 3.4 Existing health endpoints

Each backend service currently exposes a simple:

```text
GET /health
```

that returns a static JSON response.

Phase 6 should replace/standardize this with proper ASP.NET Core health checks suitable for container probes.

---

## 4. Core Design Decision: Correlation ID and Trace Context Are Different Concepts

Vision must preserve **both**.

### Application correlation ID

`X-Correlation-ID` is a durable Vision-level identifier useful for:

- support diagnostics;
- searching logs;
- associating the originating HTTP request with asynchronous work;
- storing correlation on durable domain/integration records;
- reasoning about a business workflow after an OpenTelemetry trace has expired from a telemetry backend.

### OpenTelemetry trace context

W3C trace context (`traceparent`, and `tracestate` when present) is used for:

- distributed tracing;
- parent/child span relationships;
- end-to-end trace visualization;
- latency and failure analysis.

### Required rule

Do **not** replace `X-Correlation-ID` with `TraceId`, and do **not** force them to contain the same value.

The two identifiers serve related but different purposes.

Where practical, structured logs for an operation should allow an engineer to see:

- `CorrelationId`
- `TraceId`
- `SpanId`

without manually reconstructing context.

---

# 5. OpenTelemetry Service Registration

## 5.1 All three backend services

OpenTelemetry must be registered in:

- `SecurityOperationsService`
- `WorkOrderService`
- `CredentialService`

The three applications should use a consistent implementation pattern rather than three unrelated blocks of observability code.

A small shared helper inside each service or an appropriately scoped common configuration approach is acceptable. Do **not** create a new microservice or broad shared-platform project solely for observability.

## 5.2 Resource/service naming

Each service must emit an unambiguous OpenTelemetry `service.name`.

Required names:

```text
vision-security-operations-service
vision-work-order-service
vision-credential-service
```

The resource should also identify the deployment environment where practical, for example:

```text
deployment.environment.name = Development
```

or the equivalent supported semantic convention/configuration for the installed OpenTelemetry version.

Do not embed host-specific developer information in service names.

## 5.3 Service version

If the service version can be supplied without fragile build machinery, include it as resource metadata.

A missing dynamic build version is not a Phase 6 blocker. Do not build a custom release/versioning platform just for this requirement.

---

# 6. Required Automatic Instrumentation

## 6.1 ASP.NET Core inbound requests

All three services must use ASP.NET Core instrumentation so that inbound API requests create server spans.

Expected coverage includes:

- API endpoints;
- authentication/authorization pipeline execution as part of the request trace;
- health endpoints, subject to filtering rules below.

Server spans should record standard HTTP status/error behavior supplied by OpenTelemetry instrumentation.

## 6.2 Outbound HTTP requests

All three services must enable `HttpClient` instrumentation so future/current service-to-service or external HTTP calls are traceable without redesign.

Do not create fake outbound HTTP dependencies just to demonstrate this instrumentation.

## 6.3 EF Core / PostgreSQL operations

Database operations should produce database spans in all services that use EF Core.

Kiro may add the appropriate OpenTelemetry EF Core instrumentation package if required by the installed OpenTelemetry version.

Telemetry must **not** be configured to capture database parameter values or sensitive values merely for richer traces.

It is acceptable for ordinary database operation metadata to identify PostgreSQL and the operation type while avoiding sensitive payload data.

## 6.4 Health-check trace noise

Routine liveness/readiness polling should not overwhelm useful traces.

Kiro should either:

- filter routine health-check requests from exported tracing; or
- document another small, conventional mechanism that prevents probe traffic from dominating local/cloud traces.

Health endpoints must still function normally.

---

# 7. Transactional Outbox Trace Continuity

This is the most important Phase 6 tracing requirement.

The current business flow is:

```text
HTTP request
    ↓
SecurityOperationsService
    ↓
SecurityIncident creation
    ↓
transactional outbox row
    ↓
background OutboxPublisher / OutboxBatchProcessor
    ↓
Amazon SQS
    ↓
WorkOrderService consumer
    ↓
WorkOrder creation
```

Because the outbox is processed asynchronously after the originating HTTP request may have completed, relying only on `Activity.Current` inside the background publisher is insufficient.

## 7.1 Persist trace context with the outbox record

When a qualifying incident creates an outbox message, Vision must persist enough W3C trace context to allow later continuation of that distributed trace.

At minimum, persist the originating W3C `traceparent` when a valid current activity exists.

Persist `tracestate` if present and practical.

Suggested outbox fields:

```text
TraceParent
TraceState
```

Equivalent naming is acceptable if clear.

These are observability metadata, not business identifiers.

A database migration is acceptable and expected if fields are added to the outbox table.

## 7.2 No-trace fallback

The system must continue operating when no parent trace context is available.

For example, an outbox record created from a test, maintenance process, or future non-HTTP path must still be publishable.

Missing trace context must never prevent the business transaction from completing.

## 7.3 Publisher span

When `OutboxBatchProcessor` publishes an event, it must create or participate in a producer-oriented activity/span representing the message publish operation.

If stored parent trace context is valid, the producer activity must continue from that context.

If stored trace context is absent or invalid, start a new trace rather than failing publication.

The producer span should include safe, low-cardinality messaging metadata such as:

- messaging system = AWS SQS;
- destination/queue name;
- operation = send/publish;
- integration event type;
- event ID where appropriate.

Do not attach the full message body as span data.

## 7.4 Inject W3C context into SQS message attributes

The SQS message must carry distributed trace context as message attributes.

At minimum support:

```text
traceparent
```

and, when present:

```text
tracestate
```

Use a consistent documented naming convention.

The existing `CorrelationId` SQS message attribute must remain.

Do not place access tokens, Cognito claims, person names, credential identifiers, or other sensitive user information in tracing attributes.

---

# 8. SQS Consumer Trace Continuity

## 8.1 Extract trace context before business processing

`WorkOrderService` must extract W3C trace context from the incoming SQS message attributes before processing the integration event.

Valid propagated context should become the parent of the consumer-processing span.

Invalid or missing propagated trace context must not crash the consumer. Start a new trace and continue normal contract/business validation.

## 8.2 Consumer span

Create a consumer-oriented activity/span around processing of each SQS message.

The span should cover the meaningful processing unit:

```text
receive message context
    ↓
deserialize
    ↓
contract validation
    ↓
IncidentCreatedHandler
    ↓
database commit
    ↓
DeleteMessage decision
```

The exact location may be `IncidentCreatedMessageProcessor` or another clean boundary, but it must represent one message's processing and must not incorrectly span the entire infinite background-consumer loop.

## 8.3 Span outcome

Expected behavior:

- successful processing + acknowledgement → successful span;
- duplicate/idempotent handling → successful span;
- malformed JSON / permanent contract violation → span records a failure/error condition without exposing the full message body;
- unexpected exception → span records exception/error;
- host cancellation → do not report normal shutdown as an application failure.

A poison message that is intentionally left for SQS redrive should be diagnosable from telemetry.

## 8.4 Trace relationship

For the normal demo path, an observability backend should be able to show a logical distributed trace that connects the original Security Operations request to outbox publication and Work Order message processing.

Exact visual representation can vary by OpenTelemetry backend, but the parent/child context must be technically valid.

---

# 9. Custom Activity Sources

Where automatic instrumentation does not provide the needed business/messaging spans, use `System.Diagnostics.ActivitySource` with OpenTelemetry registration.

Recommended activity source names:

```text
Vision.SecurityOperationsService
Vision.WorkOrderService
Vision.CredentialService
```

or an equally clear and stable convention.

At minimum, custom spans are expected for the asynchronous SQS/outbox boundary where automatic ASP.NET Core instrumentation alone cannot maintain the end-to-end trace.

Do not create custom spans around every method. Instrument important boundaries, not implementation trivia.

---

# 10. Structured Logging Requirements

## 10.1 Keep `ILogger`

Continue using:

```text
ILogger<T>
```

and Microsoft.Extensions.Logging.

Do not introduce Serilog, NLog, or another logging stack solely for Phase 6.

## 10.2 Structured message templates

Logs should continue using named properties rather than interpolated strings for diagnostic data.

Good:

```csharp
logger.LogInformation(
    "Created WorkOrder {WorkOrderId} from event {EventId}",
    workOrder.Id,
    evt.EventId);
```

Avoid:

```csharp
logger.LogInformation($"Created WorkOrder {workOrder.Id} from event {evt.EventId}");
```

Existing good structured log calls should remain structured.

## 10.3 Trace identifiers in log output

Configure logging so that active `Activity` information is available in emitted application logs where supported by the built-in .NET logging stack.

At minimum, engineers must be able to correlate logs with:

- OpenTelemetry `TraceId`;
- `SpanId` where an activity is active.

This should be accomplished through standard logging/activity integration or scopes, not by manually adding `TraceId` to every log statement.

## 10.4 Correlation ID logging scope

For HTTP requests in `SecurityOperationsService`, the existing `CorrelationMiddleware` should create a logging scope containing `CorrelationId` for downstream request logs.

Equivalent behavior should exist in message processing so logs generated while processing an `IncidentCreatedV1` can be searched by its Vision correlation ID.

Do not require every handler to manually repeat `CorrelationId` in every message template.

## 10.5 Container-friendly console output

Production/container logging must remain stdout/stderr based.

Do not write application log files inside containers.

A structured JSON console format is preferred for non-development/container environments if it can be implemented cleanly with built-in .NET logging.

Human-readable console output may remain available for local development.

The implementation should be controlled by environment/configuration rather than code edits per environment.

---

# 11. Sensitive Data and Telemetry Safety

Observability must not weaken the security work completed in Phase 5.

## 11.1 Never log or attach to spans

Do not log or export:

- JWT access tokens;
- refresh tokens;
- authorization headers;
- Cognito client secrets;
- AWS secret/access keys;
- Neon/PostgreSQL passwords;
- full connection strings containing credentials;
- credential/badge secrets;
- raw request bodies by default;
- raw SQS message bodies by default;
- full exception data if it would expose secrets deliberately embedded in configuration.

## 11.2 PII minimization

Do not add person name, email address, badge number, or similar credential-holder PII as trace/span attributes solely for convenience.

Use stable technical identifiers only where genuinely useful and safe.

The CredentialService should be treated as especially sensitive.

## 11.3 Authentication telemetry

It is acceptable to log/trace outcomes such as:

```text
401 unauthenticated
403 unauthorized
```

Do not record bearer tokens or full claim sets.

---

# 12. Error Recording

## 12.1 Unhandled exceptions

Unhandled request-processing exceptions should cause the active span to reflect an error and should remain logged by the existing exception-handling/logging path.

Do not create duplicate exception logs at every architectural layer.

## 12.2 Expected business/API outcomes

Normal expected outcomes such as:

- 400 validation response;
- 401 authentication failure;
- 403 authorization denial;
- 404 not found;
- idempotent duplicate handling;

should remain observable but should not automatically be treated as catastrophic service failures.

Use semantic judgment when marking custom activity status.

## 12.3 SQS failures

Unexpected SQS receive, publish, or handler exceptions must be visible in both logs and traces where an activity exists.

Normal cancellation during service shutdown must not be logged as an application error.

---

# 13. Health Checks

Replace the current static `/health` responses with ASP.NET Core health-check infrastructure.

## 13.1 Required endpoints

Each backend service should expose:

```text
GET /health/live
GET /health/ready
```

### `/health/live`

Purpose:

> Is the application process alive and capable of running?

This should be a lightweight self-check and should not require PostgreSQL or SQS to be reachable.

A temporary database or network outage must not make the application appear dead and cause an orchestrator restart loop.

### `/health/ready`

Purpose:

> Is the service ready to perform its core application work?

Readiness must include PostgreSQL connectivity for the service's own database/schema.

Kiro may use an EF Core health check or a small custom database health check.

## 13.2 SQS readiness

Do **not** perform an expensive SQS network call on every readiness probe solely to prove observability.

For SecurityOperationsService and WorkOrderService:

- required messaging configuration should be validated at startup or clearly diagnosed when invalid;
- runtime SQS failures should be represented through logs/traces and existing retry behavior;
- a lightweight SQS health check may be used only if it is demonstrably useful and does not create noisy/fragile probe behavior.

## 13.3 Compatibility endpoint

The existing `/health` endpoint may be retained temporarily as an alias if useful for developer compatibility, but Phase 6 container/CI documentation should standardize on `/health/live` and `/health/ready`.

Do not keep multiple conflicting definitions of service health.

## 13.4 Health response content

Health responses must not expose secrets, connection strings, internal exception stack traces, tokens, or sensitive configuration.

A concise response with status and service identity is sufficient.

---

# 14. Exporter Configuration

## 14.1 OTLP is the cloud-friendly default

The production-shaped exporter mechanism should be OTLP using the existing OpenTelemetry OTLP exporter package.

Exporter configuration must be environment-driven.

Prefer standard OpenTelemetry environment variables where practical, including:

```text
OTEL_EXPORTER_OTLP_ENDPOINT
OTEL_EXPORTER_OTLP_HEADERS
```

Do not hard-code a vendor endpoint or API key in source control.

## 14.2 Exporter optionality

The applications must still start and perform their business functions when an external telemetry collector/backend is not configured.

Observability backend unavailability must not become a hard dependency that takes down Vision.

## 14.3 Local developer visibility

Provide a simple documented way to see traces during local development.

Acceptable approaches include:

1. an opt-in OpenTelemetry console exporter; or
2. an optional local OTLP collector/viewer added later in the Phase 6 Docker Compose work.

If using a console exporter, it should be explicitly configuration-controlled so normal development output is not permanently flooded with trace data.

Do not require a paid SaaS observability account for local development.

---

# 15. Sampling

Vision is a small portfolio MVP and does not require a sophisticated adaptive sampling platform.

## 15.1 Development

Development may use full sampling to make trace verification straightforward.

## 15.2 Production-shaped configuration

Sampling must be configurable without recompiling the services.

A parent-based ratio sampler or equivalent standard OpenTelemetry mechanism is appropriate.

Do not implement a custom sampler.

## 15.3 Default philosophy

Choose a simple default appropriate for the MVP and document it.

The important requirement is that sampling policy can later be changed through deployment configuration if telemetry volume grows.

---

# 16. Startup and Shutdown Behavior

## 16.1 Startup

Observability registration must not interfere with:

- authentication;
- EF Core migrations in Development;
- seed data;
- SQS hosted services;
- OpenAPI;
- normal application startup.

Configuration mistakes related to optional exporters should fail safely where reasonable and produce an actionable diagnostic rather than an unexplained crash.

Configuration that is genuinely required for core service operation should still fail fast in the normal way.

## 16.2 Shutdown

OpenTelemetry providers should use normal .NET hosting lifetime/disposal so pending telemetry can be flushed during graceful shutdown where supported.

Do not add arbitrary sleeps during shutdown.

Cancellation of the SQS consumer/outbox publisher during graceful shutdown should remain normal behavior, not an error condition.

---

# 17. Configuration Shape

Kiro should keep configuration understandable and environment-driven.

A project-specific section is acceptable for settings that are not already covered cleanly by standard `OTEL_*` variables, for example:

```json
{
  "Observability": {
    "Enabled": true,
    "ConsoleExporterEnabled": false,
    "SamplingRatio": 1.0
  }
}
```

This is illustrative, not a requirement to use these exact property names.

Requirements:

- no secrets in committed `appsettings*.json`;
- environment variables can override deployment settings;
- all three services use the same convention;
- disabling telemetry must not disable application logging or health endpoints.

---

# 18. Testing Requirements

Observability code does not need brittle tests that assert the exact shape of every exported span.

Tests should target high-value behavior.

## 18.1 Required: service registration/startup smoke test

For each backend service, existing integration-test infrastructure or a focused new test should establish that the application can start with the observability configuration used by tests.

Do not require a live commercial telemetry backend.

## 18.2 Required: correlation middleware regression tests

For `SecurityOperationsService`, verify at least:

1. incoming valid `X-Correlation-ID` is preserved;
2. missing correlation ID causes a new value to be generated;
3. invalid/unsafe correlation ID is replaced;
4. response contains the final correlation ID.

If equivalent tests already exist and are sufficient, do not duplicate them.

## 18.3 Required: outbox trace-context persistence

Add a focused test showing that when an activity exists during qualifying incident creation:

- the outbox record stores the originating trace context;
- the existing Vision `CorrelationId` remains independently preserved.

Also test that no active activity does not break outbox creation.

## 18.4 Required: SQS trace-context injection

Test the production-path publisher boundary sufficiently to show:

- `traceparent` is emitted as an SQS message attribute when trace context exists;
- `CorrelationId` remains present;
- absence of stored trace context still allows publication.

Avoid asserting implementation-specific span internals that make refactoring unnecessarily difficult.

## 18.5 Required: SQS trace-context extraction

Add a focused consumer/processor test showing that a valid incoming `traceparent` is extracted and used to establish consumer processing context.

Also verify malformed/missing trace context does not crash processing or prevent otherwise valid business behavior.

## 18.6 Required: health endpoints

For each service, test:

- `/health/live` returns healthy when the process is running;
- `/health/ready` reflects database readiness;
- health output does not expose sensitive configuration.

Use existing PostgreSQL integration-test infrastructure where appropriate.

## 18.7 Required: telemetry safety review

Automated tests do not need to scan every log string, but Kiro should inspect changed logging/telemetry code and confirm no tokens, secrets, authorization headers, raw SQS payloads, or CredentialService PII were added to telemetry.

ChatGPT will independently review this during the Phase 6 gate.

---

# 19. Demo / Manual Verification Scenario

Kiro should provide a short documented manual verification procedure for the distributed trace.

The preferred verification path is the existing Vision demo workflow:

```text
SecurityManager creates or drives the critical Security Incident path
    ↓
SecurityOperationsService creates incident
    ↓
outbox publishes IncidentCreatedV1
    ↓
SQS transports event
    ↓
WorkOrderService consumes event
    ↓
WorkOrder is created
```

With tracing enabled, an engineer should be able to verify:

- the Security Operations HTTP server span;
- database work associated with incident/outbox creation;
- producer/publish span;
- consumer/process span;
- Work Order database work;
- one continuous distributed trace context across the async boundary;
- the same Vision `CorrelationId` available for log/business correlation.

This can be demonstrated through an opt-in console exporter or local OTLP-compatible viewer depending on the final Phase 6 local observability setup.

---

# 20. Logging Acceptance Examples

The following events should remain easy to diagnose.

## SecurityOperationsService

- application starts;
- incident created;
- outbox record created/published;
- SQS publish failure;
- database error;
- unexpected request exception.

## WorkOrderService

- consumer starts/stops;
- queue resolution/retry behavior;
- message received;
- malformed/unsupported event;
- duplicate event handled idempotently;
- WorkOrder created;
- message acknowledged/deleted;
- unexpected processing failure.

## CredentialService

- application starts;
- unexpected request/database failure;
- authentication/authorization outcomes through normal framework/request telemetry;
- credential operations without exposing credential-holder PII unnecessarily.

Do not add noisy `Information` logs for every trivial method entry/exit.

---

# 21. Observability Non-Goals for Phase 6

Do **not** add the following merely to satisfy this specification:

- Application Insights as a hard dependency;
- Datadog;
- New Relic;
- Splunk;
- Grafana Cloud;
- Elastic Stack;
- a production Prometheus cluster;
- a production Grafana deployment;
- Kafka;
- EventBridge;
- Lambda;
- a new observability microservice;
- a custom logging framework;
- a custom distributed tracing protocol;
- event sourcing;
- full business analytics;
- alerting/on-call infrastructure;
- long-term telemetry retention architecture;
- elaborate dashboards;
- Kubernetes/Helm observability deployment.

Phase 7 may later choose a concrete cloud monitoring destination. Phase 6 should leave that choice open through OTLP-compatible configuration.

---

# 22. Scope Guidance on Metrics

OpenTelemetry metrics are useful, but full metrics design is **not required** to pass this Phase 6 observability slice because the approved Phase 6 handoff prioritizes tracing, logging, correlation, health, and operational quality.

If Kiro can add standard low-cost runtime/HTTP metrics with minimal complexity and no vendor lock-in, that is acceptable, but it should not delay the required tracing and health work.

Do not build custom business KPI instrumentation during this slice.

---

# 23. Required Documentation Changes

Kiro should add or update concise repository documentation covering:

- what OpenTelemetry instrumentation is enabled;
- service names;
- how to enable local trace export;
- how OTLP endpoint configuration works;
- health endpoints;
- the distinction between `X-Correlation-ID` and W3C trace context;
- how trace context is carried through the transactional outbox and SQS;
- confirmation that telemetry credentials/secrets must come from environment/deployment secret storage.

This can live in an existing appropriate documentation area or a focused Phase 6 observability document.

Do not duplicate the same large explanation across all three service READMEs.

---

# 24. Implementation Quality Requirements

The implementation must:

- preserve existing service boundaries;
- preserve the transactional-outbox guarantee;
- preserve SQS delete-after-success behavior;
- preserve idempotency rules;
- preserve existing authorization behavior;
- preserve `CancellationToken` propagation;
- avoid synchronous blocking of async telemetry paths;
- avoid a telemetry backend becoming a required business dependency;
- use bounded, maintainable configuration;
- avoid hard-coded developer machine paths;
- avoid hard-coded cloud credentials;
- avoid adding a second logging framework;
- compile cleanly;
- keep existing tests passing.

---

# 25. Phase 6 Observability Acceptance Checklist

Kiro should consider the observability slice complete only when all applicable items below are satisfied.

## OpenTelemetry foundation

- [ ] OpenTelemetry is registered in all three backend services.
- [ ] Each service emits the required unique `service.name`.
- [ ] Deployment environment metadata is included where practical.
- [ ] ASP.NET Core server tracing is enabled.
- [ ] `HttpClient` tracing is enabled.
- [ ] EF Core/PostgreSQL tracing is enabled without sensitive parameter capture.
- [ ] Routine health probes do not dominate exported traces.

## Correlation and messaging

- [ ] Existing `X-Correlation-ID` behavior remains intact.
- [ ] Correlation ID is available in a logging scope for Security Operations request processing.
- [ ] Correlation ID is available in a logging scope during Work Order SQS processing.
- [ ] W3C parent trace context is persisted with the outbox when available.
- [ ] Outbox creation works when no trace context exists.
- [ ] Publisher creates a meaningful producer span.
- [ ] SQS carries `traceparent`.
- [ ] SQS carries `tracestate` when present and supported by the implementation.
- [ ] Existing SQS `CorrelationId` attribute is preserved.
- [ ] WorkOrderService extracts valid W3C context.
- [ ] WorkOrderService creates a per-message consumer/process span.
- [ ] Missing/malformed trace context does not break message processing.
- [ ] Normal critical-incident → WorkOrder flow can be observed as one distributed trace.

## Logging

- [ ] Existing `ILogger<T>` architecture is retained.
- [ ] Changed/new logs use structured message templates.
- [ ] Active trace IDs/span IDs are available in log output.
- [ ] Container logging goes to stdout/stderr.
- [ ] No file-based container logging is introduced.
- [ ] No duplicate logging framework is introduced.

## Safety

- [ ] Authorization headers are not logged.
- [ ] JWTs are not logged or attached to spans.
- [ ] AWS/DB/Cognito secrets are not logged or attached to spans.
- [ ] Raw SQS message bodies are not attached to spans/logged by default.
- [ ] Credential-holder PII is not added to trace attributes solely for convenience.

## Health

- [ ] All three services expose `/health/live`.
- [ ] All three services expose `/health/ready`.
- [ ] Liveness does not depend on PostgreSQL or SQS network availability.
- [ ] Readiness verifies the service's PostgreSQL connectivity.
- [ ] Health responses do not leak sensitive details.

## Export/configuration

- [ ] OTLP export is environment/configuration driven.
- [ ] No vendor endpoint or secret is hard-coded.
- [ ] Application business behavior still works without an external telemetry backend.
- [ ] Local trace visibility is documented and usable.
- [ ] Sampling is simple and configurable.

## Tests

- [ ] Service startup with observability configuration is covered.
- [ ] Correlation middleware behavior is covered or existing tests are confirmed sufficient.
- [ ] Outbox trace-context persistence is covered.
- [ ] SQS trace-context injection is covered.
- [ ] SQS trace-context extraction/fallback is covered.
- [ ] Liveness/readiness endpoints are covered.
- [ ] Existing tests continue to pass.

## Documentation

- [ ] Developer documentation explains how to enable/view telemetry locally.
- [ ] Developer documentation explains OTLP configuration.
- [ ] Developer documentation explains health endpoints.
- [ ] Developer documentation explains correlation ID vs trace context.
- [ ] Developer documentation explains async trace propagation through outbox + SQS.

---

# 26. Definition of Done

The Phase 6 observability slice is ready for ChatGPT review when Kiro can report all of the following:

```text
1. dotnet build succeeds.
2. dotnet test succeeds.
3. All three backend services start with normal local configuration.
4. /health/live works for all three services.
5. /health/ready works for all three services with PostgreSQL available.
6. A critical Security Incident can still produce a WorkOrder through the real outbox + SQS path.
7. The flow preserves the existing Vision CorrelationId.
8. The flow propagates W3C trace context across the outbox/SQS boundary.
9. Trace/log output can be viewed using the documented local mechanism.
10. No real secrets are required in source-controlled configuration.
11. No Phase 5 authorization/security behavior regresses.
```

Kiro should provide the updated codebase plus the actual build/test results to the project owner for ChatGPT's independent review.

---

# 27. Review Priorities for ChatGPT

On the next code-review snapshot, ChatGPT will specifically inspect:

1. OpenTelemetry registration in all services.
2. Resource/service naming.
3. ASP.NET Core, HTTP, and EF Core instrumentation.
4. Activity source registration and custom messaging spans.
5. Correct persistence of W3C trace context through the transactional outbox.
6. Correct SQS trace-context injection/extraction.
7. Continued preservation of Vision `CorrelationId`.
8. Span lifetime and parentage around message processing.
9. Error/cancellation semantics.
10. Structured logging and trace/log correlation.
11. Secret/token/PII leakage risk.
12. Health-check semantics.
13. Environment-driven exporter configuration.
14. Sampling/configuration simplicity.
15. Tests covering the important observability boundaries.
16. Preservation of existing messaging, idempotency, authorization, and business behavior.
17. Avoidance of unnecessary infrastructure or vendor lock-in.

---

# 28. Final Engineering Principle

The Phase 6 observability implementation should make this question answerable during a demo or production incident:

> A Security Manager created a critical security incident. What happened next, which services participated, did the message reach WorkOrderService, where did time get spent, did anything fail, and which logs belong to the same workflow?

Vision should answer that with a combination of:

```text
OpenTelemetry TraceId / SpanId
+
Vision CorrelationId
+
structured ILogger logs
+
health status
```

without exposing sensitive information and without requiring an expensive observability platform.
