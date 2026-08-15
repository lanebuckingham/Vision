# Vision — Dead-Letter Queue Behavior

**Status:** Phase 4 implementation specification  
**Primary queue:** `vision-{environment}-incident-created`  
**DLQ:** `vision-{environment}-incident-created-dlq`  
**Target repository location:** `docs/integration-contracts/dead-letter-queue-behavior.md`  
**Depends on:** `incident-created-sqs-contract.md`, `messaging-retry-expectations.md`

---

# 1. Purpose

The DLQ exists to isolate messages that repeatedly cannot be processed successfully.

Its purpose is:

```text
visibility
failure isolation
diagnosis
safe recovery
```

It is not an alternative business workflow.

---

# 2. Queue Relationship

Configure:

```text
Primary Standard Queue
        |
        | repeated failed receives
        v
Dead-Letter Queue
```

Approved redrive threshold:

```text
maxReceiveCount = 5
```

Approved DLQ retention:

```text
14 days
```

These are project configuration decisions.

---

# 3. What Should Reach the DLQ

Messages may reach the DLQ because of persistent infrastructure failure, but the most important expected DLQ population is poison/contract-invalid messages.

Examples:

```text
invalid JSON
unsupported event version
missing EventId
missing Incident.Id
missing Asset.Id
invalid severity
non-Critical automatic-work payload
invalid payload shape
persistent database processing failure
```

---

# 4. What Must Not Reach the DLQ

The following should be handled as successful idempotent outcomes:

```text
duplicate EventId
same IncidentId already processed
manual WorkOrder already exists for incident
concurrent uniqueness race representing same business work
```

Do not poison the DLQ with normal duplicate delivery.

---

# 5. Consumer Behavior for Poison Messages

For a permanent message failure:

```text
log classification
do not create WorkOrder
do not DeleteMessage
```

SQS redelivery increments receive count.

After the configured threshold, the queue redrive policy moves the message to the DLQ.

The application does not manually send poison messages to the DLQ for the MVP.

---

# 6. DLQ Message Integrity

The failed message body should remain the original message body provided by SQS redrive.

Do not replace it with:

```text
only an error string
only a stack trace
a rewritten event
```

Diagnosis depends on preserving the original payload.

---

# 7. Error Context

Useful failure context should be available through structured logs using:

```text
SQS MessageId
EventId when parseable
IncidentId when parseable
CorrelationId when parseable
failure category
receive count
```

The DLQ itself does not need a custom wrapper/envelope solely to carry this information.

---

# 8. Replay Ownership

Vision does not require an automated DLQ replay service.

MVP recovery is operational/manual:

```text
1. inspect failed message
2. identify cause
3. fix producer/consumer/configuration
4. deliberately redrive/replay if appropriate
```

Do not build:

```text
DLQ admin UI
automatic replay daemon
generic replay microservice
self-healing poison-message rewriter
```

---

# 9. Replay Safety

Any replay mechanism must remain safe because WorkOrderService is idempotent.

If a previously failed message is replayed after partial success occurred elsewhere:

```text
SourceEventId / SecurityIncidentId uniqueness
```

must still prevent duplicate WorkOrders.

Replay safety comes from the consumer, not from assuming operators never replay twice.

---

# 10. Unsupported Version Behavior

An unsupported event such as:

```text
vision.security-operations.incident-created.v2
```

must not be coerced into v1.

It should:

```text
fail contract validation
remain unacknowledged
eventually enter DLQ
```

This makes version mismatch visible.

---

# 11. Non-Critical Contract Drift

If a producer incorrectly sends:

```text
Severity = High
```

to this automatic Critical-work queue:

```text
no WorkOrder
contract violation logged
event eventually reaches DLQ
```

Do not silently accept producer drift.

---

# 12. DLQ Observability

A non-empty DLQ is an operational warning.

At minimum, the deployed environment should make it possible to inspect:

```text
ApproximateNumberOfMessagesVisible
```

A simple CloudWatch alarm for:

```text
DLQ message count > 0
```

is recommended if inexpensive to provision.

It is not required to block Phase 4 code completion.

---

# 13. Application Health

DLQ contents must not make application liveness fail.

A poison message should not cause:

```text
container restart loops
service-wide outage
healthy messages to stop processing
```

Messages are isolated individually.

---

# 14. Security

DLQ payloads must not contain:

```text
credentials
tokens
connection strings
PHI
real patient data
```

The event contract should carry only the approved fictional physical-security operational context.

Access to the DLQ should follow least privilege.

---

# 15. Terraform Expectations

Infrastructure should define:

```text
primary Standard queue
DLQ
redrive policy
maxReceiveCount = 5
DLQ retention = 14 days
producer/consumer IAM
```

Queue names should be environment-scoped.

---

# 16. Acceptance Criteria

```text
✓ primary queue has configured DLQ
✓ maxReceiveCount is 5
✓ DLQ retention is 14 days
✓ invalid JSON is not acknowledged as success
✓ unsupported version is not acknowledged as success
✓ contract-invalid message eventually becomes DLQ-eligible
✓ duplicate EventId does not enter DLQ
✓ duplicate IncidentId does not enter DLQ
✓ original failed payload is preserved
✓ no automated replay service is required
✓ replay remains safe through idempotency
✓ one poison message does not stop healthy message processing
```

---

# 17. Governing Rule

> **The DLQ is for messages that cannot be safely processed after repeated attempts—not for ordinary duplicate delivery or business idempotency.**
