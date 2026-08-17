# Vision — Observability (Phase 6)

This document explains the OpenTelemetry tracing, health checks, and logging added
in Phase 6 for all three backend services (`SecurityOperationsService`,
`WorkOrderService`, `CredentialService`).

## Service names

Each service registers a stable OpenTelemetry `service.name`:

| Service | `service.name` |
|---|---|
| SecurityOperationsService | `vision-security-operations-service` |
| WorkOrderService | `vision-work-order-service` |
| CredentialService | `vision-credential-service` |

The resource also carries `deployment.environment.name` (from
`IWebHostEnvironment.EnvironmentName`, e.g. `Development`) and `service.version`
(the assembly version).

## Correlation ID vs. W3C trace context

Vision intentionally keeps **two** identifiers, and they serve different purposes:

- **`X-Correlation-ID`** — a durable, Vision-level identifier. It is generated (or
  echoed back if the caller supplies one) by `CorrelationMiddleware` in
  `SecurityOperationsService`, stored on `CorrelationContext`, persisted on the
  outbox row, carried in the `IncidentCreated.v1` payload, and preserved on the
  resulting `WorkOrder`. It remains useful for log search and database
  investigation long after a trace has expired from a telemetry backend.
- **W3C trace context (`traceparent` / `tracestate`)** — OpenTelemetry's standard
  distributed-tracing identifiers. They connect spans across services/processes
  and are used by tracing backends to reconstruct one logical trace.

Neither replaces the other. A single workflow can be searched by `CorrelationId`
in logs/DB and separately visualized end-to-end by `TraceId` in a tracing backend.

## What's instrumented

All three services enable:

- **ASP.NET Core server spans** for inbound HTTP requests (via
  `OpenTelemetry.Instrumentation.AspNetCore`). Requests to `/health*` are filtered
  out of exported traces so routine liveness/readiness polling does not dominate
  trace volume — the health endpoints themselves are unaffected.
- **Outbound `HttpClient` spans** (via `OpenTelemetry.Instrumentation.Http`) —
  covers, among other things, the AWS SDK's SQS calls (`GetQueueUrl`,
  `SendMessage`, `ReceiveMessage`, `DeleteMessage`), since the AWS SDK for .NET
  does not ship its own OpenTelemetry instrumentation.
- **PostgreSQL command spans** via `Npgsql.OpenTelemetry` (`AddNpgsql()` on the
  `TracerProviderBuilder`). This traces at the Npgsql/ADO.NET boundary, so it
  covers every command EF Core issues without depending on the still-beta
  `OpenTelemetry.Instrumentation.EntityFrameworkCore` package. Query parameter
  values are never captured — only command text, database name, and timing.

`SecurityOperationsService` and `WorkOrderService` additionally register a custom
`ActivitySource` (`Vision.SecurityOperationsService` / `Vision.WorkOrderService`)
for the transactional-outbox / SQS boundary — see below. `CredentialService` has
no asynchronous messaging per the architecture rules, so it has no custom
ActivitySource; automatic instrumentation covers every boundary it has.

## Trace propagation through the outbox and SQS

The hardest part of Phase 6 is keeping one distributed trace alive across an
asynchronous boundary: the HTTP request that creates a Critical incident
completes before the background `OutboxPublisher` ever sends the corresponding
SQS message.

```text
POST /api/v1/incidents          (ASP.NET Core server span)
        |
CreateIncidentCommandHandler
        |
Activity.Current captured -> OutboxMessage.TraceParent / TraceState
        |
OutboxBatchProcessor.PublishBatchAsync (background)
        |
resumes the stored trace context (or starts a new one if none was stored)
        |
"IncidentCreated.v1 publish" producer span (Vision.SecurityOperationsService ActivitySource)
        |
SQS SendMessage — traceparent / tracestate injected as message attributes
        |
IncidentCreatedConsumer receives the message (WorkOrderService)
        |
IncidentCreatedMessageProcessor extracts traceparent/tracestate from message attributes
        |
"IncidentCreated.v1 process" consumer span (Vision.WorkOrderService ActivitySource)
        |
IncidentCreatedHandler creates the WorkOrder
```

Key properties:

- **No parent trace context is not an error.** If `Activity.Current` was `null`
  when the outbox row was created (e.g. a maintenance script or a future
  non-HTTP path), `TraceParent`/`TraceState` simply stay `null`. The publisher
  starts a fresh trace instead of failing to publish.
- **Malformed stored/received trace context never poisons a valid business
  message.** `ActivityContext.TryParse` failures fall back to starting a new
  trace; the SQS message is still sent/processed normally.
- **The existing SQS `CorrelationId` and `EventType` message attributes are
  unchanged** — `traceparent`/`tracestate` are added alongside them, never in
  place of them.
- **Only safe, low-cardinality span attributes are set**: messaging system/
  operation/destination name, the integration event type, and the event ID.
  The message body/payload is never attached to a span.

## Health endpoints

Each service exposes:

- `GET /health/live` — is the process alive? A lightweight self-check. Never
  depends on PostgreSQL or SQS, so a temporary dependency outage does not make
  the app look dead and trigger a container-orchestrator restart loop.
- `GET /health/ready` — is the service ready to do its own work? Includes an EF
  Core connectivity check (`SELECT 1`) against the service's own schema.
- `GET /health` — a compatibility alias retained for existing local tooling.
  Do not add new health semantics here; use `/health/live` and `/health/ready`.

Responses are intentionally minimal (`status` + per-check name/status) and never
include exception details, connection strings, or other configuration.

## Logging

Logging continues to use `ILogger<T>` / `Microsoft.Extensions.Logging` with
structured message templates — no second logging framework was introduced.
`TraceId`/`SpanId`/`ParentId` are attached to the logging scope automatically via
`ActivityTrackingOptions`, so any active trace is visible in log output without
manually adding those fields to every log call.

Console output is:

- Human-readable, single-line, with scopes, in `Development`.
- Structured JSON, with scopes, in any other environment (container-friendly,
  stdout-based — no file-based logging inside containers).

## Configuration

A small `Observability` section (all three services, `appsettings.json` /
environment variables) covers settings not already handled cleanly by standard
`OTEL_*` environment variables:

```json
{
  "Observability": {
    "Enabled": true,
    "ConsoleExporterEnabled": false,
    "SamplingRatio": 1.0
  }
}
```

- `Enabled: false` disables all OpenTelemetry provider registration. Application
  logging and health endpoints are never affected by this setting.
- `ConsoleExporterEnabled: true` prints trace data to stdout for local
  development visibility, without requiring a collector/backend. Off by default
  so normal development console output isn't flooded with span dumps.
- `SamplingRatio` is a `ParentBasedSampler(TraceIdRatioBasedSampler(...))` — child
  spans of an already-sampled trace are always kept; only new root traces are
  subject to the ratio. `1.0` (full sampling) is the MVP default given Vision's
  low traffic volume.

### Exporting to an OTLP collector/backend

The OTLP exporter (`OpenTelemetry.Exporter.OpenTelemetryProtocol`) is always
registered and is the production-shaped default. It reads its destination from
standard environment variables — nothing is hard-coded in source:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://<your-collector>:4317
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <token>
```

If no OTLP endpoint is configured, the exporter fails safely in the background
(batch export retries/logs) — it never becomes a hard dependency that blocks
startup or business request processing. This keeps the concrete choice of
tracing backend (Grafana Cloud free tier, a self-hosted collector, etc.) a
Phase 7 decision, without committing to one now.

### Local trace visibility without a collector

Set `Observability__ConsoleExporterEnabled=true` (or the equivalent
`appsettings.Development.json` value) and run a service locally — spans print to
the console as they complete. This requires no external account or service.

## Secrets and safety

No OTLP endpoint, API key, or other telemetry credential is committed to source
control. Never logged or attached to spans: JWTs, refresh tokens, Authorization
headers, Cognito/AWS/database secrets, raw SQS message bodies, or raw HTTP
request bodies. `CredentialService` in particular never adds person names, email
addresses, or credential numbers as trace/span attributes.
