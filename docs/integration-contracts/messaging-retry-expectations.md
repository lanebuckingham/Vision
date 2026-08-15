# Vision — Messaging Retry Expectations

**Status:** Phase 4 implementation specification  
**Workflow:** `SecurityOperationsService` → Amazon SQS → `WorkOrderService`  
**Event:** `vision.security-operations.incident-created.v1`  
**Target repository location:** `docs/integration-contracts/messaging-retry-expectations.md`  
**Depends on:** `incident-created-sqs-contract.md`, `work-order-service.md`

---

# 1. Purpose

This document defines retry behavior for Vision's one required asynchronous workflow.

The goal is:

> **Retry failures that may recover, avoid retrying successful/idempotent outcomes, and allow permanently bad messages to become operationally visible instead of looping forever.**

Vision uses a layered retry model:

```text
Producer outbox retry
        +
AWS SDK transient retry
        +
SQS message redelivery
        +
DLQ after repeated receive failures
```

Do not add another generalized retry framework unless implementation evidence requires it.

---

# 2. Producer Retry Ownership

`SecurityOperationsService` uses the transactional outbox as the durable producer retry mechanism.

For a qualifying incident:

```text
incident + outbox record commit atomically
```

The HTTP request does not retry SQS publication.

The background outbox publisher owns publication retries.

---

# 3. Producer Send Failure

If `SendMessage` fails:

```text
PublishedAt remains null
AttemptCount += 1
LastError updated safely
```

The outbox record remains eligible for a later publication cycle.

Do not:

```text
delete the outbox row
mark it published
roll back the already committed incident
block future incident API requests
```

---

# 4. Outbox Retry Interval

Recommended idle polling interval:

```text
5 seconds
```

Configuration-driven.

Do not implement a tight retry loop.

The AWS SDK may perform its own transient request retries.

The outbox does not need per-message exponential retry scheduling for the MVP.

---

# 5. Event Identity Across Producer Retries

The following must remain stable across every retry of the same outbox record:

```text
EventId
EventType
OccurredAt
CorrelationId
Incident.Id
```

Do not generate a new `EventId` on publication retry.

Duplicate publication is acceptable.

Duplicate business work is not.

---

# 6. Consumer Retry Ownership

`WorkOrderService` does not manually sleep/retry the full business transaction.

For recoverable processing failures:

```text
do not delete the SQS message
```

The queue visibility timeout expires and SQS redelivers it.

---

# 7. Retryable Consumer Failures

Treat failures as retryable when a later attempt may succeed.

Examples:

```text
PostgreSQL temporarily unavailable
database timeout
temporary network failure
temporary AWS SDK failure
transient lock/contention
host shutdown during processing
unexpected infrastructure exception before commit
```

Behavior:

```text
log
do not acknowledge/delete
allow redelivery
```

---

# 8. Non-Retry Success Outcomes

The following are successful terminal processing outcomes and should be acknowledged:

```text
WorkOrder created and committed
same EventId already processed
same IncidentId already has a WorkOrder
manual WorkOrder already exists for the incident
concurrent duplicate resolved as the same logical work
```

Duplicate delivery is not an error condition.

---

# 9. Permanent Contract Failures

Examples:

```text
malformed JSON
missing required identifiers
unsupported event version
invalid required enum
non-Critical event on the automatic-work queue
missing required asset context
structurally impossible payload
```

These are not expected to become valid on retry.

However, for the MVP they should **not** be immediately deleted.

Behavior:

```text
classify as permanent/poison
log clearly
do not create WorkOrder
do not delete
allow SQS receive count to advance
eventually redrive to DLQ
```

This preserves observability.

---

# 10. Visibility Timeout

Project default:

```text
60 seconds
```

WorkOrder creation should normally complete well inside this window.

Correctness must not depend on the visibility timeout preventing duplicates.

Idempotency remains mandatory.

---

# 11. Maximum Consumer Attempts

Project default:

```text
maxReceiveCount = 5
```

After repeated unsuccessful receives, SQS moves the message to the configured DLQ.

This value belongs in infrastructure configuration.

Do not hard-code it in business logic.

---

# 12. Delete Ordering

Required ordering:

```text
receive
validate
process
commit WorkOrder
delete message
```

Never:

```text
delete
then commit
```

---

# 13. Crash Window — Consumer

If WorkOrder commit succeeds but the process crashes before `DeleteMessage`:

```text
message is delivered again
consumer detects SourceEventId/IncidentId duplicate
second attempt is acknowledged
```

Required behavior.

---

# 14. Crash Window — Producer

If SQS send succeeds but the publisher crashes before saving `PublishedAt`:

```text
same outbox event may be sent again
```

Required consumer behavior:

```text
one WorkOrder total
```

---

# 15. Retry Logging

Producer logs should include:

```text
EventId
IncidentId
CorrelationId
AttemptCount
```

Consumer retry logs should include where available:

```text
EventId
IncidentId
CorrelationId
SQS receive count
failure classification
```

Do not require exact wording.

---

# 16. Retry Anti-Patterns

Do not implement:

```text
while(true) retry inside one message handler
Thread.Sleep
blocking waits
retrying domain validation failures
new EventId for each publish retry
deleting a message after any caught exception
exactly-once delivery assumptions
```

---

# 17. Acceptance Criteria

```text
✓ SQS send failure leaves outbox unpublished
✓ EventId remains stable across publication retries
✓ transient consumer failure does not delete message
✓ successful commit deletes message
✓ duplicate EventId is acknowledged as success
✓ duplicate IncidentId is acknowledged as success
✓ crash-after-commit redelivery creates no duplicate
✓ permanent bad message is not silently discarded
✓ repeated bad message becomes DLQ-eligible
✓ CancellationToken is respected
```

---

# 18. Governing Rule

> **Retry infrastructure failures; acknowledge successful or idempotent outcomes; isolate permanent message failures through the DLQ.**
