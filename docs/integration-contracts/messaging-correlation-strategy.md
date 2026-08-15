# Vision — Messaging Correlation Strategy

**Status:** Phase 4 implementation specification  
**Workflow:** Incident creation → outbox → SQS → WorkOrder creation  
**Target repository location:** `docs/integration-contracts/messaging-correlation-strategy.md`  
**Depends on:** `incident-created-sqs-contract.md`, `work-order-service.md`

---

# 1. Purpose

This document defines how Vision correlates one business workflow across HTTP, PostgreSQL, Amazon SQS, and WorkOrder persistence.

The primary objective is:

> **A developer or reviewer should be able to follow one Critical incident from the incoming request through event publication and into the resulting WorkOrder.**

---

# 2. Identifier Roles

Vision uses several identifiers, each with a distinct meaning.

```text
CorrelationId
    broader distributed workflow

EventId
    one integration-event record

IncidentId
    SecurityOperations business aggregate

WorkOrderId
    WorkOrder business aggregate

SQS MessageId
    transport delivery identity
```

Do not conflate them.

---

# 3. Correlation ID Source

When handling the incident-creation HTTP request:

1. use an existing trusted correlation value if the application already establishes one;
2. otherwise generate a new correlation ID.

The value must be nonblank.

Recommended representation:

```text
string
max 100 characters
```

Do not require it to be a UUID if current tracing infrastructure produces another safe identifier format.

---

# 4. Correlation Flow

The same correlation value should flow through:

```text
POST /api/v1/incidents
        ↓
CreateIncidentCommand
        ↓
SecurityIncident processing
        ↓
OutboxMessage.CorrelationId
        ↓
IncidentCreated.v1.CorrelationId
        ↓
SQS send
        ↓
WorkOrder consumer logging scope
        ↓
WorkOrder.CorrelationId
```

Do not generate a fresh correlation ID at each hop.

---

# 5. Event ID Generation

`EventId` is generated once when the outbox event is created.

It identifies the event, not the whole workflow.

Across publication retry:

```text
EventId remains unchanged
```

---

# 6. SQS Message ID

Amazon SQS assigns its own transport message ID.

Use it for transport diagnostics.

Do not store it as:

```text
WorkOrder.SourceEventId
```

`SourceEventId` must store the contract's `EventId`.

---

# 7. Message Attributes

Recommended message attributes:

```text
EventType
CorrelationId
```

These are useful for diagnostics.

However:

```text
message body is authoritative
```

The consumer must validate the body's:

```text
eventType
correlationId
```

Do not create correctness dependence on message attributes.

---

# 8. Persistence

SecurityOperations outbox stores:

```text
correlation_id
```

WorkOrder stores:

```text
CorrelationId
SourceEventId
SecurityIncidentId
```

This gives durable reconstruction across services.

---

# 9. Structured Logging

Producer logging scope should include where relevant:

```text
CorrelationId
EventId
IncidentId
```

Consumer logging scope should include:

```text
CorrelationId
EventId
IncidentId
WorkOrderId after creation
SQS MessageId
```

Do not require every log line to repeat every value if a structured scope carries them.

---

# 10. Example Trace Narrative

A reviewer should be able to reconstruct:

```text
CorrelationId = C123

POST incident
    IncidentId = I456

Outbox
    EventId = E789
    CorrelationId = C123

SQS
    MessageId = M321
    EventId = E789

WorkOrder
    WorkOrderId = W654
    SourceEventId = E789
    SecurityIncidentId = I456
    CorrelationId = C123
```

---

# 11. OpenTelemetry

When OpenTelemetry is added, tracing should complement—not replace—the business correlation strategy.

Desired spans:

```text
HTTP request
PostgreSQL transaction
outbox publication
SQS send
SQS receive/process
WorkOrder database insert
```

Where SDK/instrumentation support allows it, propagate standard trace context.

The durable application-level `CorrelationId` should still remain available for logs and database investigation.

---

# 12. Correlation on Retries

Retries of the same logical event preserve:

```text
CorrelationId
EventId
IncidentId
```

A new SQS transport message ID may appear after republishing.

That is expected.

---

# 13. Correlation on Duplicate Delivery

Duplicate SQS delivery must log enough context to identify:

```text
EventId already processed
existing WorkOrderId
CorrelationId
```

The duplicate does not create a new correlation workflow.

---

# 14. Correlation on Manual WorkOrder Existing

If an automatic event arrives after a manual WorkOrder already exists for the Incident:

```text
event CorrelationId remains useful for the attempted workflow
existing WorkOrder is reused as the business cardinality outcome
```

Do not overwrite historical WorkOrder correlation metadata solely because a later duplicate/alternate creation path arrives.

Log the relationship instead.

---

# 15. HTTP Response Correlation

Where practical, expose a safe correlation/trace identifier in Problem Details or response headers.

This is useful for debugging.

Do not expose:

```text
internal secrets
database identifiers not already safe
AWS credentials
```

---

# 16. Correlation Validation

Consumer validation requires:

```text
CorrelationId nonblank
```

An absent correlation ID makes the v1 message contract-invalid.

This protects observability as part of the formal integration contract.

---

# 17. Privacy and Security

Correlation identifiers must not encode:

```text
patient names
employee names
credential numbers
secrets
tokens
email addresses
```

Use opaque identifiers.

---

# 18. Anti-Patterns

Do not:

```text
generate new CorrelationId in consumer for a valid incoming event
use SQS MessageId as EventId
use WorkOrderId as the cross-service correlation ID
store access tokens as correlation values
log full message payload on every success
invent a separate custom tracing framework
```

---

# 19. Acceptance Criteria

```text
✓ incident request has a correlation ID
✓ outbox stores same correlation ID
✓ IncidentCreated.v1 carries same correlation ID
✓ producer logs contain correlation/event/incident identifiers
✓ WorkOrder consumer receives same correlation ID
✓ WorkOrder persists same correlation ID
✓ SourceEventId equals event EventId, not SQS MessageId
✓ retries preserve EventId and CorrelationId
✓ duplicate handling retains correlation context
✓ identifiers contain no sensitive data
✓ future OpenTelemetry integrates with this strategy
```

---

# 20. Governing Rule

> **One business workflow keeps one correlation identity across service and transport boundaries, while EventId, IncidentId, WorkOrderId, and SQS MessageId retain their separate meanings.**
