# Vision — SQS Integration Event Contract

**Status:** MVP implementation contract  
**Workflow:** Security Incident → Maintenance Work Order  
**Producer:** `SecurityOperationsService`  
**Consumer:** `WorkOrderService`  
**Transport:** Amazon SQS  
**Event:** `vision.security-operations.incident-created.v1`  
**Target repository location:** `docs/integration-contracts/incident-created-sqs-contract.md`  
**Depends on:** `docs/business-domain-specification.md`, `docs/service-specifications/security-operations-service-specification-revised.md`, `docs/service-specifications/work-order-service.md`  
**Scope:** Vision MVP

---

# 1. Purpose

This document defines the formal asynchronous integration contract between:

```text
SecurityOperationsService
        ↓
Amazon SQS
        ↓
WorkOrderService
```

The workflow exists to demonstrate one meaningful distributed-system interaction in Vision:

> A Critical security incident associated with a physical-security asset causes a maintenance WorkOrder to be created asynchronously.

The contract defines:

- event qualification,
- event schema,
- event versioning,
- producer behavior,
- transactional-outbox behavior,
- queue behavior,
- consumer behavior,
- idempotency,
- retry semantics,
- dead-letter behavior,
- poison-message handling,
- correlation,
- logging,
- failure isolation,
- test expectations.

This document is the authoritative implementation contract for this specific SQS workflow.

---

# 2. Business Workflow

The asynchronous path is:

```text
Security Manager creates Critical incident
                  |
                  v
       SecurityOperationsService
                  |
                  | same PostgreSQL transaction
                  |
          +-------+--------+
          |                |
          v                v
 SecurityIncident      OutboxMessage
          |
          | transaction commits
          v
   Outbox publisher
          |
          v
      Amazon SQS
          |
          v
    WorkOrderService
          |
          v
   New WorkOrder created
```

The synchronous incident-creation operation must not depend on immediate SQS availability.

---

# 3. Qualification Rule

An incident qualifies for automatic WorkOrder creation only when:

```text
Incident.Severity == Critical
AND
Incident.SecurityAssetId != null
```

Therefore:

```text
Critical + asset     -> publish IncidentCreated.v1
Critical + no asset  -> no automatic WorkOrder event
High + asset         -> no automatic WorkOrder event
Medium + asset       -> no automatic WorkOrder event
Low + asset          -> no automatic WorkOrder event
```

Other incidents may receive manually created WorkOrders through WorkOrderService.

Do not broaden this rule during the MVP.

---

# 4. Event Type

The canonical event type is:

```text
vision.security-operations.incident-created.v1
```

The exact string is part of the contract.

Producer and consumer must not use:

```text
IncidentCreated
incident.created
vision.incident.created
vision.security-operations.incident-created
```

as substitutes on the wire.

---

# 5. Event Version

Version is encoded explicitly in:

```text
eventType
```

using:

```text
.v1
```

The v1 contract is immutable once implemented.

Compatible additions should generally be optional.

Breaking changes require a new event type, for example:

```text
vision.security-operations.incident-created.v2
```

Do not silently change the meaning or type of an existing v1 field.

---

# 6. Canonical JSON Payload

```json
{
  "eventId": "6ac3b602-51a5-470d-b0e8-686f5f713a04",
  "eventType": "vision.security-operations.incident-created.v1",
  "occurredAt": "2026-08-10T12:12:00Z",
  "correlationId": "1f8fdb31e3b84b25adcc2565ded23321",
  "incident": {
    "id": "2f785125-4630-43c1-ab30-239919cb4a57",
    "title": "Pharmacy storage camera offline",
    "description": "Camera stopped responding and is not producing video.",
    "severity": "Critical"
  },
  "asset": {
    "id": "99750ccc-976b-49ee-a485-f3677b9b91ef",
    "name": "Pharmacy Storage Camera 02",
    "assetTag": "CAM-PHARM-002",
    "assetType": "Camera"
  },
  "location": {
    "id": "72533c8e-5541-48bd-8821-8ae4c434634f",
    "name": "Pharmacy Storage",
    "buildingId": "9ca90164-c910-44f6-98f0-142058ffdf1b",
    "buildingName": "Main Hospital"
  }
}
```

---

# 7. Top-Level Field Contract

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `eventId` | UUID | Yes | Unique identifier for this integration event |
| `eventType` | string | Yes | Exact versioned event type |
| `occurredAt` | ISO 8601 UTC timestamp | Yes | When the business event was created |
| `correlationId` | string | Yes | Correlates request, outbox, SQS publication, and consumption |
| `incident` | object | Yes | Source incident snapshot |
| `asset` | object | Yes | Associated asset snapshot |
| `location` | object | Yes | Associated location/building snapshot |

`correlationId` should normally be available because event creation originates from an HTTP/API workflow.

If no upstream correlation value exists, SecurityOperationsService must create one before constructing the event.

---

# 8. Incident Object

```json
{
  "id": "uuid",
  "title": "string",
  "description": "string",
  "severity": "Critical"
}
```

Contract:

| Field | Type | Required |
|---|---|---:|
| `id` | UUID | Yes |
| `title` | string | Yes |
| `description` | string | Yes |
| `severity` | string enum | Yes |

For automatic WorkOrder creation:

```text
severity must equal Critical
```

The consumer should not create automatic work from a non-Critical v1 message.

---

# 9. Asset Object

```json
{
  "id": "uuid",
  "name": "Pharmacy Storage Camera 02",
  "assetTag": "CAM-PHARM-002",
  "assetType": "Camera"
}
```

Contract:

| Field | Type | Required |
|---|---|---:|
| `id` | UUID | Yes |
| `name` | string | Yes |
| `assetTag` | string/null | No |
| `assetType` | string enum | Yes |

`asset.id` becomes:

```text
WorkOrder.SecurityAssetId
```

`asset.name` becomes:

```text
WorkOrder.AssetNameSnapshot
```

The snapshot exists so WorkOrderService does not have to query SecurityOperations persistence to render useful work-order context.

---

# 10. Location Object

```json
{
  "id": "uuid",
  "name": "Pharmacy Storage",
  "buildingId": "uuid",
  "buildingName": "Main Hospital"
}
```

Contract:

| Field | Type | Required |
|---|---|---:|
| `id` | UUID | Yes |
| `name` | string | Yes |
| `buildingId` | UUID | Yes |
| `buildingName` | string | Yes |

`location.name` becomes:

```text
WorkOrder.LocationNameSnapshot
```

WorkOrderService does not create a Location or Building entity from this payload.

These values are display snapshots only.

---

# 11. Snapshot Semantics

The event intentionally carries denormalized display context.

This does not transfer domain ownership.

For example:

```text
SecurityOperationsService owns:
    SecurityAsset.Name
    Location.Name
    Building.Name

WorkOrderService stores:
    AssetNameSnapshot
    LocationNameSnapshot
```

A snapshot represents what the producer knew when the event occurred.

WorkOrderService does not later synchronize these fields automatically when names change.

That eventual staleness is acceptable for the MVP because the values provide historical work context.

---

# 12. Do Not Serialize Domain Entities

SecurityOperationsService must construct an explicit integration-event DTO.

Do not serialize:

```text
SecurityIncident entity
SecurityAsset entity
Location entity
Building entity
EF navigation graph
```

directly into SQS.

Reasons:

- domain-model changes must not silently alter contracts,
- navigation properties may create oversized/unexpected payloads,
- persistence details should not cross service boundaries,
- explicit versioning is easier to review.

---

# 13. Event ID

`eventId` is:

```text
Guid
```

and must be globally unique for each logical event record.

The outbox message ID and event ID may be the same UUID for the MVP.

Recommended:

```text
OutboxMessage.Id == IncidentCreatedV1.EventId
```

This reduces identifier proliferation while retaining clear semantics.

Do not regenerate `eventId` on every publication retry.

A retry of the same outbox record must preserve the same event ID.

---

# 14. OccurredAt

`occurredAt` is:

```text
DateTimeOffset
```

serialized as UTC ISO 8601.

Example:

```text
2026-08-10T12:12:00Z
```

It represents when the integration event was created as part of the incident transaction.

It does not represent:

- SQS send time,
- SQS receive time,
- WorkOrder creation time.

---

# 15. Correlation ID

The correlation ID connects the business workflow:

```text
HTTP incident request
       ↓
CreateIncidentCommand
       ↓
SecurityIncident + OutboxMessage
       ↓
SQS send
       ↓
SQS receive
       ↓
WorkOrder creation
```

The same logical correlation value must be preserved.

Store it in:

```text
security_operations.outbox_messages.correlation_id
WorkOrder.CorrelationId
```

and include it in structured logs.

---

# 16. Correlation vs Event ID

These identifiers serve different purposes.

```text
eventId
    identifies one integration event

correlationId
    identifies the broader distributed workflow

incident.id
    identifies the source business incident

workOrder.id
    identifies the created maintenance aggregate
```

Do not treat these as interchangeable.

---

# 17. Producer Transactional Outbox

PostgreSQL persistence and Amazon SQS cannot participate in one atomic transaction.

Therefore Vision uses a transactional outbox.

For a qualifying incident:

```text
BEGIN DATABASE TRANSACTION

INSERT SecurityIncident

INSERT OutboxMessage

COMMIT
```

If either database insert fails:

```text
ROLLBACK
```

The incident must not commit without the required outbox record.

---

# 18. Outbox Table

Use:

```text
security_operations.outbox_messages
```

Required fields:

```text
id              uuid primary key
event_type      varchar(200) not null
payload         jsonb not null
occurred_at     timestamptz not null
published_at    timestamptz null
attempt_count   integer not null default 0
last_error      varchar(2000) null
correlation_id  varchar(100) not null
```

Recommended index:

```text
WHERE published_at IS NULL
ORDER BY occurred_at
```

Use an appropriate PostgreSQL partial index for unpublished records.

---

# 19. Outbox Persistence Behavior

When a qualifying incident is created:

1. create `SecurityIncident`,
2. construct `IncidentCreatedV1`,
3. serialize the explicit event DTO,
4. create `OutboxMessage`,
5. save both in the same database transaction.

Do not call Amazon SQS from inside the incident transaction.

Do not make API success dependent on SQS being reachable.

---

# 20. Incident API Success Semantics

A successful incident POST means:

```text
incident committed
AND
required outbox record committed
```

It does not mean:

```text
WorkOrder already created
```

The workflow is eventually consistent.

The frontend must tolerate a short interval between:

```text
Incident created
```

and:

```text
WorkOrder visible
```

---

# 21. Outbox Publisher

Implement a lightweight hosted background service in SecurityOperationsService.

Conceptual loop:

```text
poll unpublished outbox records
        ↓
send each record to SQS
        ↓
send succeeds?
   /             \
 yes              no
  |                |
set publishedAt    leave unpublished
clear error        increment attempt count
                   record/log error
```

Use a scoped `SecurityOperationsDbContext` per processing cycle or batch.

Respect host shutdown cancellation.

---

# 22. Outbox Polling

Recommended MVP polling interval when idle:

```text
5 seconds
```

This value should be configuration-driven.

The objective is:

- fast enough for the five-minute demo,
- simple enough for the MVP,
- not a busy loop.

A successful publish cycle may immediately process additional pending records without waiting another full idle interval.

---

# 23. Outbox Batch Size

Recommended maximum records per polling batch:

```text
20
```

This is not a throughput-optimization project.

The batch merely prevents an unbounded query.

Configuration name example:

```text
Messaging:Outbox:BatchSize
```

---

# 24. Outbox Publisher Retries

The outbox itself supplies durable retry behavior.

On SQS send failure:

```text
PublishedAt remains null
AttemptCount += 1
LastError = safe/truncated failure description
```

The publisher retries the record on a later poll.

Do not delete failed outbox records.

Do not mark `published_at` until SQS confirms the send operation succeeded.

---

# 25. Producer Retry Backoff

Avoid retrying a failing SQS request in a tight loop.

Recommended application-level delay strategy:

```text
normal polling interval: 5 seconds
```

AWS SDK transient retry behavior may still operate underneath.

The MVP does not require a complex exponential-backoff scheduler stored per outbox row.

If SQS remains unavailable, unpublished messages remain durable in PostgreSQL and are retried on future polling cycles.

---

# 26. Producer Failure Isolation

If SQS is unavailable:

```text
SecurityOperations API remains usable
incident remains committed
outbox message remains pending
publisher logs failure
future publication attempts continue
```

This is intentional.

The distributed workflow may be delayed without losing the incident or event.

---

# 27. Producer Message Body

SQS message body must contain the canonical JSON event directly.

Do not wrap it in an unnecessary generic envelope such as:

```json
{
  "message": "...serialized-json..."
}
```

unless an AWS integration layer requires such wrapping.

For direct AWS SDK `SendMessage` usage, the event JSON itself should be the message body.

---

# 28. SQS Queue Type

Use:

```text
Amazon SQS Standard Queue
```

not FIFO.

Rationale:

- strict ordering is not required,
- duplicate delivery must already be handled correctly,
- Standard Queue demonstrates realistic at-least-once messaging,
- FIFO complexity is not justified for the MVP.

Do not rely on queue ordering for correctness.

---

# 29. Queue Names

Recommended logical names:

```text
vision-incident-created
vision-incident-created-dlq
```

Environment-specific physical names should include the environment when necessary, for example:

```text
vision-dev-incident-created
vision-dev-incident-created-dlq

vision-prod-incident-created
vision-prod-incident-created-dlq
```

Do not make application code depend on hard-coded queue URLs.

Use configuration.

---

# 30. SQS Long Polling

Consumer receive calls should use long polling.

Recommended:

```text
WaitTimeSeconds = 20
```

This reduces empty polling and API calls.

The value should be configuration-driven.

---

# 31. Visibility Timeout

Recommended initial visibility timeout:

```text
60 seconds
```

WorkOrder creation should normally complete far faster than this.

The timeout should exceed expected consumer processing time with comfortable margin.

Do not use an extremely short timeout that causes normal processing to create avoidable duplicate concurrent deliveries.

If actual processing later approaches the timeout, tune it based on measurement.

---

# 32. Consumer Batch Size

Recommended receive batch:

```text
up to 10 messages
```

which aligns with SQS receive batching and is more than sufficient for portfolio/demo load.

Each message must still be processed independently for success/failure semantics.

A failure in one message must not force successful sibling messages to be treated as failures.

---

# 33. Dead-Letter Queue

Configure:

```text
vision-incident-created-dlq
```

as the dead-letter queue for the primary queue.

Recommended redrive policy:

```text
maxReceiveCount = 5
```

Meaning:

- a transient failure gets several opportunities to recover,
- a permanently invalid message does not retry forever,
- broken messages become visible for diagnosis.

This value should be infrastructure configuration rather than application code.

---

# 34. DLQ Retention

Recommended DLQ message retention:

```text
14 days
```

This is long enough for a portfolio/demo environment to diagnose failures without maintaining indefinite message storage.

Primary queue retention may use the standard project/infrastructure setting; it does not need custom application behavior.

---

# 35. Consumer Hosted Service

WorkOrderService should contain a lightweight hosted SQS consumer.

Conceptual structure:

```text
BackgroundService
    ↓
ReceiveMessageAsync
    ↓
for each message
    ↓
deserialize / validate
    ↓
application handler
    ↓
database commit
    ↓
DeleteMessageAsync
```

Use DI scopes so each processing operation uses an appropriate scoped `WorkOrderDbContext`.

Respect host shutdown cancellation.

---

# 36. Consumer DTO

Use a dedicated DTO matching the v1 contract, for example:

```text
IncidentCreatedV1
IncidentCreatedIncidentV1
IncidentCreatedAssetV1
IncidentCreatedLocationV1
```

Do not reference SecurityOperationsService domain types from WorkOrderService.

Do not create a shared domain assembly merely for this event.

If a tiny shared contracts package later becomes justified, it must contain contract DTOs only, never service-owned domain entities.

For the MVP, duplicating the explicit contract shape in producer and consumer projects is acceptable and often clearer.

---

# 37. Consumer Contract Validation

Before creating a WorkOrder, validate at minimum:

```text
EventId != empty
EventType == vision.security-operations.incident-created.v1
OccurredAt valid
CorrelationId nonblank

Incident.Id != empty
Incident.Title nonblank
Incident.Description nonblank
Incident.Severity == Critical

Asset.Id != empty
Asset.Name nonblank
Asset.AssetType valid

Location.Id != empty
Location.Name nonblank
Location.BuildingId != empty
Location.BuildingName nonblank
```

A malformed message must not produce a partial WorkOrder.

---

# 38. WorkOrder Mapping

A valid event maps to:

```text
WorkOrder.Id
    = new Guid

WorkOrder.SecurityAssetId
    = event.Asset.Id

WorkOrder.SecurityIncidentId
    = event.Incident.Id

WorkOrder.Title
    = "Repair: " + event.Incident.Title

WorkOrder.Description
    = event.Incident.Description

WorkOrder.Priority
    = Critical

WorkOrder.Status
    = New

WorkOrder.AssetNameSnapshot
    = event.Asset.Name

WorkOrder.LocationNameSnapshot
    = event.Location.Name

WorkOrder.SourceEventId
    = event.EventId

WorkOrder.CorrelationId
    = event.CorrelationId

WorkOrder.CreatedAt
    = now UTC

WorkOrder.UpdatedAt
    = same creation timestamp
```

Do not assign a Technician automatically.

---

# 39. Idempotency Requirement

SQS Standard Queue is an at-least-once delivery mechanism.

Therefore duplicate messages are normal.

The consumer must guarantee:

```text
one qualifying SecurityIncident
    -> at most one WorkOrder
```

even when messages are delivered more than once.

---

# 40. Idempotency Keys

Use two business protections:

```text
SourceEventId
SecurityIncidentId
```

Recommended database constraints:

```text
UNIQUE source_event_id
WHERE source_event_id IS NOT NULL
```

```text
UNIQUE security_incident_id
WHERE security_incident_id IS NOT NULL
```

This protects both:

- duplicate copies of the exact same event,
- different event IDs representing the same incident.

---

# 41. Idempotency Algorithm

Before insertion:

```text
if WorkOrder exists where SourceEventId == EventId
    -> already processed
    -> acknowledge successfully

else if WorkOrder exists where SecurityIncidentId == Incident.Id
    -> work already exists
    -> acknowledge successfully

else
    -> create WorkOrder
```

Database uniqueness constraints are still required because two consumers/deliveries may race between the check and insert.

---

# 42. Concurrent Duplicate Race

Possible race:

```text
Consumer A checks -> none
Consumer B checks -> none

Consumer A inserts
Consumer B inserts
```

Database uniqueness must cause one insertion to fail.

When the failure is specifically a unique violation on:

```text
source_event_id
or
security_incident_id
```

and the existing record represents the same logical work:

```text
treat as idempotent success
acknowledge message
```

Do not retry forever.

---

# 43. Existing Manual WorkOrder

A Security Manager may manually create a WorkOrder for an incident before the async message is consumed.

If the later event arrives and a WorkOrder already exists for:

```text
SecurityIncidentId == event.Incident.Id
```

the consumer must:

```text
not create another
log that work already exists
acknowledge the message
```

The business invariant matters more than the creation mechanism.

---

# 44. Message Acknowledgement Rule

Delete/acknowledge an SQS message only after one of these outcomes:

```text
1. WorkOrder successfully committed
2. Message is a confirmed idempotent duplicate
3. Message is deliberately classified as permanently non-actionable and should not retry
```

For normal processing failure:

```text
do not delete
```

The visibility timeout expires and SQS retries delivery.

---

# 45. Transient Failure

Examples:

```text
PostgreSQL temporarily unavailable
network failure
temporary AWS dependency failure
database timeout
temporary lock/contention
```

Behavior:

```text
log failure
do not delete message
allow SQS redelivery
```

After repeated receives, SQS redrive policy eventually moves it to the DLQ.

---

# 46. Permanent / Poison Failure

Examples:

```text
invalid JSON
unsupported eventType
missing required IDs
invalid enum string
non-Critical payload sent to this automatic-work queue
structurally impossible payload
```

These failures will not heal through retry.

Recommended behavior for the MVP:

```text
log as permanent message failure
do not create WorkOrder
do not manually delete
allow SQS redrive policy to move message to DLQ
```

This makes the bad message observable instead of silently discarding it.

Do not throw the message away just because parsing failed.

---

# 47. Unsupported Event Version

If WorkOrderService receives:

```text
vision.security-operations.incident-created.v2
```

before it supports v2:

```text
do not process as v1
do not create WorkOrder
classify as unsupported/permanent
allow DLQ redrive
```

Do not attempt best-effort deserialization into the v1 DTO.

Version mismatch must be visible.

---

# 48. Unexpected Non-Critical Event

The producer is responsible for qualification, but the consumer must defend itself.

If:

```text
Incident.Severity != Critical
```

the message violates the v1 automatic-work business contract.

Do not create a WorkOrder.

Treat the message as a permanent contract violation and allow it to reach the DLQ for diagnosis.

This is preferable to silently accepting producer drift.

---

# 49. Consumer Exception Handling

Do not use:

```text
catch (Exception) { DeleteMessage(); }
```

A broad catch may log, but acknowledgement depends on the classified outcome.

Conceptual flow:

```text
try
    deserialize
    validate
    consume
    delete message

catch PermanentMessageException
    log contract failure
    leave message for DLQ redrive

catch transient/system exception
    log processing failure
    leave message for retry
```

Exact exception type design is implementation-specific.

Do not create a large custom exception framework.

---

# 50. Retry Ownership

Retry exists at multiple layers:

```text
AWS SDK transient request retries
        +
SQS redelivery after visibility timeout
        +
DLQ after maxReceiveCount
```

Do not add an additional application retry loop around the entire message-processing transaction unless measurements show it is required.

Simple infrastructure-driven retry is preferable for the MVP.

---

# 51. Duplicate Delivery During Long Processing

Because visibility timeout is finite, a message may occasionally be redelivered while another consumer is still processing it.

Correctness must come from:

```text
idempotency + database uniqueness
```

not from assuming visibility timeout prevents all duplicates.

---

# 52. Queue Ordering

No correctness rule may assume messages arrive in creation order.

Although this workflow currently creates independent WorkOrders, SQS Standard ordering is not guaranteed.

Do not write code such as:

```text
"process oldest incident first or fail"
```

No ordering requirement exists.

---

# 53. Eventual Consistency

After the incident API returns success, WorkOrder creation may not yet be visible.

Expected UI flow:

```text
Incident created
      ↓
Work order may show "being created" / not yet available
      ↓
consumer processes message
      ↓
WorkOrder becomes discoverable
```

The frontend should query:

```text
GET /api/v1/work-orders?incidentId={incidentId}
```

when it needs to discover the associated WorkOrder.

Do not implement cross-schema reads for immediate consistency.

---

# 54. Frontend Polling

If the UI needs to demonstrate automatic creation immediately after incident creation, a short bounded frontend polling strategy is acceptable.

Recommended behavior:

```text
poll WorkOrder lookup every ~1–2 seconds
for a short bounded period
stop once WorkOrder appears
```

Do not create infinite polling.

If the WorkOrder does not appear within the bounded period:

```text
show a non-destructive "Work order is still being created" state
```

rather than claiming failure automatically.

The exact UX implementation may be adjusted to fit the Phase 4 UI.

---

# 55. Producer Logging

Recommended structured events:

```text
Queued integration event {EventId} for incident {IncidentId}
Publishing integration event {EventId} to queue {QueueName}
Published integration event {EventId} for incident {IncidentId}
Failed to publish integration event {EventId}; attempt {AttemptCount}
```

Include:

```text
EventId
IncidentId
CorrelationId
```

where useful.

---

# 56. Consumer Logging

Recommended structured events:

```text
Received IncidentCreated event {EventId} for incident {IncidentId}
Creating WorkOrder from event {EventId}
Created WorkOrder {WorkOrderId} from incident {IncidentId}
Duplicate event {EventId} already handled by WorkOrder {WorkOrderId}
Incident {IncidentId} already has WorkOrder {WorkOrderId}
Failed to process event {EventId}
Rejected invalid event {EventId}
```

Include correlation ID in logging scope where practical.

---

# 57. Sensitive Logging

Never log:

```text
AWS access keys
AWS secret keys
bearer tokens
connection strings
database passwords
full exception details to client-facing responses
```

The event contains operational asset information but no PHI should ever be included.

Vision seed/demo data must remain fictional.

---

# 58. OpenTelemetry

When OpenTelemetry is added, the desired trace story is:

```text
POST /api/v1/incidents
        |
        v
CreateIncident handler
        |
        v
PostgreSQL transaction
        |
        +--> SecurityIncident
        |
        +--> OutboxMessage
        |
        v
Outbox publisher
        |
        v
SQS SendMessage
        |
        v
SQS ReceiveMessage
        |
        v
WorkOrder consumer
        |
        v
PostgreSQL INSERT WorkOrder
```

At minimum, correlation IDs should make these operations reconstructable from structured logs even before full trace propagation is implemented.

---

# 59. AWS Configuration Boundary

Application code receives configuration for:

```text
queue URL/name
region
poll interval
visibility timeout where explicitly set
batch size
```

AWS credentials must come from the normal environment/identity chain.

Do not store AWS credentials in:

```text
appsettings.json
source control
Docker image
frontend configuration
```

Production infrastructure should use least-privilege IAM.

---

# 60. Producer IAM

SecurityOperationsService requires only the permissions necessary to publish to the incident-created queue.

Conceptually:

```text
sqs:SendMessage
```

Do not grant broad:

```text
sqs:*
```

unless infrastructure tooling requires a separate administrative identity.

Runtime service permissions should remain least privilege.

---

# 61. Consumer IAM

WorkOrderService requires only the queue permissions necessary to consume.

Conceptually:

```text
sqs:ReceiveMessage
sqs:DeleteMessage
sqs:GetQueueAttributes
sqs:ChangeMessageVisibility
```

Only include `ChangeMessageVisibility` if implementation needs it.

Do not give WorkOrderService permission to publish unrelated messages merely because it consumes SQS.

---

# 62. DLQ Operations

The application does not need an automated DLQ replay subsystem for the MVP.

Diagnosis workflow may be manual:

```text
inspect DLQ
identify failure
fix producer/consumer/configuration
redrive or recreate message deliberately
```

Do not build:

```text
DLQ administration UI
automatic poison-message repair
generic replay service
```

---

# 63. DLQ Observability

A non-empty DLQ should be considered an operational warning.

At minimum, infrastructure/operations documentation should make it possible to inspect:

```text
ApproximateNumberOfMessagesVisible
```

for the DLQ.

A CloudWatch alarm may be added if straightforward, but it must not block MVP delivery.

The important portfolio behavior is that poison messages are not silently lost.

---

# 64. Health Checks

Do not make the standard liveness endpoint fail solely because SQS is temporarily unavailable.

Liveness answers:

```text
is the process alive?
```

A separate readiness/dependency check may report SQS problems where useful.

Avoid allowing a transient AWS outage to create container restart loops.

---

# 65. Graceful Shutdown

Hosted publisher and consumer services must respect:

```text
CancellationToken
```

On shutdown:

- stop receiving new messages,
- allow in-progress work to finish when practical,
- do not acknowledge messages whose database commit did not complete,
- dispose scopes cleanly.

---

# 66. Serialization

Use:

```text
System.Text.Json
```

unless the repository already standardizes another serializer.

Use explicit DTOs.

Enum serialization should match the wire strings required by this contract.

The event payload uses camelCase JSON property names.

---

# 67. Contract Compatibility

The v1 consumer should tolerate unknown additional JSON fields unless there is a security or correctness reason not to.

This allows non-breaking producer additions.

However, required v1 fields must remain validated.

Example:

```text
new optional diagnostic property
    -> consumer may ignore safely
```

Changing:

```text
incident.id from UUID to integer
```

would be breaking and requires v2.

---

# 68. Message Size

The event should remain small.

Do not add:

```text
full incident history
binary attachments
images
audit history
entire SecurityAsset entity
entire Location entity
entire Building entity
```

The current payload is intentionally sufficient for WorkOrder creation and display snapshots.

---

# 69. WorkOrder Creation Transaction

Within WorkOrderService, event consumption should create the WorkOrder in a normal PostgreSQL transaction.

Conceptually:

```text
BEGIN

check duplicate identifiers
insert WorkOrder

COMMIT
```

Only after commit:

```text
DeleteMessage
```

---

# 70. No Distributed Transaction

Do not attempt a distributed transaction across:

```text
SecurityOperations PostgreSQL
Amazon SQS
WorkOrder PostgreSQL
```

The system deliberately uses:

```text
transactional outbox
+
at-least-once delivery
+
idempotent consumer
```

to provide reliability.

---

# 71. Failure Scenario — Incident Commit Fails

If the incident database transaction fails:

```text
incident not committed
outbox event not committed
nothing published
no WorkOrder created
```

Correct.

---

# 72. Failure Scenario — Incident Commits, SQS Down

```text
incident committed
outbox event committed
SQS send fails
published_at remains null
publisher retries later
```

No event is lost.

---

# 73. Failure Scenario — SQS Send Succeeds, Publisher Crashes Before Marking Published

Possible sequence:

```text
SQS SendMessage succeeds
publisher crashes
PublishedAt remains null
publisher restarts
same event sent again
```

Result:

```text
duplicate SQS delivery possible
```

This is expected.

WorkOrderService idempotency must make it harmless.

Do not attempt to "solve" this by assuming exactly-once publication.

---

# 74. Failure Scenario — Consumer Creates WorkOrder, Crashes Before DeleteMessage

Possible sequence:

```text
WorkOrder committed
consumer crashes
SQS message not deleted
message delivered again
```

Result:

```text
duplicate delivery
existing SourceEventId/IncidentId detected
no second WorkOrder
message acknowledged
```

This scenario is a required idempotency test.

---

# 75. Failure Scenario — Consumer DB Down

```text
message received
database unavailable
WorkOrder not committed
message not deleted
visibility expires
message retried
```

After `maxReceiveCount`, persistent failure sends the message to DLQ.

---

# 76. Failure Scenario — Invalid JSON

```text
message received
deserialization fails
no WorkOrder created
message not deleted
retries occur
message moves to DLQ
```

This is intentionally observable poison-message behavior.

---

# 77. Failure Scenario — Duplicate Different Event ID

If two different event IDs reference the same incident:

```text
Event A -> Incident X
Event B -> Incident X
```

only one WorkOrder may exist for:

```text
Incident X
```

`SecurityIncidentId` uniqueness protects the business rule.

---

# 78. Acceptance Tests — Producer Qualification

### Critical + asset

```text
Given a Critical incident with SecurityAssetId
When the incident is committed
Then one IncidentCreated.v1 outbox message exists
```

### Critical + no asset

```text
Then no IncidentCreated.v1 outbox message exists
```

### High/Medium/Low + asset

```text
Then no automatic IncidentCreated.v1 outbox message exists
```

---

# 79. Acceptance Tests — Atomic Outbox

Force outbox insert failure.

Expected:

```text
incident transaction rolls back
```

Force incident insert failure.

Expected:

```text
outbox insert does not remain committed
```

Verify:

```text
qualifying incident and outbox record commit atomically
```

---

# 80. Acceptance Tests — Event Shape

Verify serialized JSON includes:

```text
eventId
eventType
occurredAt
correlationId

incident.id
incident.title
incident.description
incident.severity

asset.id
asset.name
asset.assetTag
asset.assetType

location.id
location.name
location.buildingId
location.buildingName
```

Verify:

```text
eventType == vision.security-operations.incident-created.v1
```

---

# 81. Acceptance Tests — Outbox Retry

Simulate SQS publish failure.

Verify:

```text
PublishedAt remains null
AttemptCount increments
LastError populated safely
event ID unchanged
```

Next successful attempt:

```text
PublishedAt populated
same event ID published
```

---

# 82. Acceptance Tests — Valid Consumer

Given one valid Critical v1 event:

```text
exactly one WorkOrder created
Status = New
Priority = Critical
SecurityAssetId copied
SecurityIncidentId copied
AssetNameSnapshot copied
LocationNameSnapshot copied
SourceEventId copied
CorrelationId copied
```

---

# 83. Acceptance Tests — Same Event Delivered Twice

Deliver the identical message twice.

Expected:

```text
one WorkOrder total
second delivery handled successfully
```

---

# 84. Acceptance Tests — Crash After WorkOrder Commit

Simulate:

```text
WorkOrder committed
message not deleted
```

Redeliver.

Expected:

```text
existing SourceEventId detected
no second WorkOrder
message successfully acknowledged
```

---

# 85. Acceptance Tests — Same Incident, Different Event IDs

Deliver:

```text
Event A / Incident X
Event B / Incident X
```

Expected:

```text
one WorkOrder total
SecurityIncidentId uniqueness preserved
```

---

# 86. Acceptance Tests — Manual Work Already Exists

Create manual WorkOrder for Incident X.

Then deliver automatic event for Incident X.

Expected:

```text
no new WorkOrder
existing work recognized
message handled idempotently
```

---

# 87. Acceptance Tests — Transient Consumer Failure

Simulate database timeout.

Expected:

```text
no message delete
no partial WorkOrder
message eligible for retry
```

---

# 88. Acceptance Tests — Poison Message

Send invalid JSON or unsupported event version.

Expected:

```text
no WorkOrder
message not silently discarded
repeated processing eventually moves message to DLQ
```

---

# 89. Acceptance Tests — Correlation

Given:

```text
CorrelationId = ABC
```

Verify it appears consistently in:

```text
event
outbox row
publisher logs
consumer logs
WorkOrder.CorrelationId
```

---

# 90. Acceptance Tests — Service Isolation

Verify WorkOrderService consumer does not query:

```text
security_operations.*
```

Verify SecurityOperationsService publisher does not query or mutate:

```text
work_orders.*
```

The only workflow coupling is:

```text
versioned event contract
```

plus external IDs/snapshots.

---

# 91. Configuration Contract

Recommended configuration shape:

```json
{
  "Messaging": {
    "IncidentCreated": {
      "QueueName": "vision-dev-incident-created",
      "QueueUrl": "",
      "WaitTimeSeconds": 20,
      "VisibilityTimeoutSeconds": 60,
      "MaxNumberOfMessages": 10
    },
    "Outbox": {
      "PollIntervalSeconds": 5,
      "BatchSize": 20
    }
  }
}
```

Do not store credentials here.

Infrastructure may provide queue URLs/names through environment variables.

---

# 92. Terraform / Infrastructure Contract

When infrastructure is implemented, provision:

```text
1 Standard SQS primary queue
1 Standard SQS dead-letter queue
redrive policy
queue retention settings
least-privilege producer IAM
least-privilege consumer IAM
```

Recommended:

```text
maxReceiveCount = 5
DLQ retention = 14 days
```

Do not provision:

```text
SNS
EventBridge
Kafka
Lambda
FIFO queue
```

for this workflow.

---

# 93. Local Development

AWS SQS integration must not make normal coding impossible without production AWS credentials.

The repository may support one of these clean local approaches:

```text
real AWS dev queue
or
an approved local AWS-compatible emulator
```

Do not build a custom message broker abstraction solely to avoid SQS locally.

If messaging configuration is absent in a local synchronous-development mode, hosted messaging services may be disabled explicitly through configuration.

The behavior must not silently pretend events were published.

---

# 94. Demo Expectations

The asynchronous workflow should be demonstrable:

```text
1. Create Critical Pharmacy Storage asset incident.
2. Incident API returns successfully.
3. Outbox contains/publishes IncidentCreated.v1.
4. SQS receives event.
5. WorkOrderService consumes it.
6. New Critical WorkOrder appears.
7. WorkOrder displays asset/location context without cross-service DB read.
```

This should happen quickly enough that a reviewer understands cause and effect.

Do not hide the asynchronous nature by creating the WorkOrder synchronously in the incident request.

---

# 95. Architectural Explanation for Portfolio Review

The implementation should support this explanation:

> Vision uses a transactional outbox because PostgreSQL and Amazon SQS cannot be committed atomically. A qualifying incident and its outbound event are committed together in PostgreSQL. A background publisher sends the durable outbox message to SQS. SQS provides at-least-once delivery, so WorkOrderService treats duplicates as normal and protects the business invariant with both event-level and incident-level idempotency plus database uniqueness. Poison messages are retried and ultimately isolated in a dead-letter queue.

The code should make that explanation visibly true.

---

# 96. Explicit Non-Goals

Do not implement:

```text
exactly-once delivery claims
distributed transactions
two-phase commit
event sourcing
generic enterprise service bus
generic event framework
schema registry
Kafka
SNS fan-out
EventBridge
Lambda
FIFO ordering
DLQ management UI
automatic DLQ remediation
generic replay platform
generic message auditing subsystem
```

---

# 97. Kiro Implementation Sequence

Recommended Phase 4 messaging order:

## Producer

1. Create explicit `IncidentCreatedV1` DTO.
2. Add `OutboxMessage` persistence model.
3. Add outbox EF mapping and migration.
4. Modify qualifying incident creation transaction.
5. Add serialization tests.
6. Add outbox atomicity tests.
7. Implement SQS configuration.
8. Implement outbox publisher.
9. Add publication retry/error tests.

## Consumer

10. Create consumer-side v1 DTO.
11. Add required WorkOrder uniqueness indexes.
12. Implement event-to-WorkOrder application handler.
13. Implement idempotency lookup.
14. Implement concurrency/unique-violation handling.
15. Implement hosted SQS consumer.
16. Implement acknowledgement classification.
17. Add duplicate-delivery tests.
18. Add transient-failure tests.
19. Add poison-message tests.

## Infrastructure

20. Provision queue.
21. Provision DLQ.
22. Configure redrive policy.
23. Configure producer IAM.
24. Configure consumer IAM.
25. Verify environment configuration.

## End-to-End

26. Create Critical incident.
27. Observe outbox.
28. Observe publication.
29. Observe consumption.
30. Verify exactly one WorkOrder.

---

# 98. ChatGPT Phase 4 Review Checklist

Review:

## Contract

```text
exact v1 event name
explicit DTOs
correct fields
correct JSON names
no EF/domain serialization
```

## Producer

```text
qualifying incidents only
incident + outbox atomic
event ID stable across retry
PublishedAt only after SQS success
failure remains durable
```

## SQS

```text
Standard queue
long polling
reasonable visibility timeout
DLQ configured
maxReceiveCount configured
no ordering dependency
```

## Consumer

```text
explicit v1 validation
WorkOrder mapping correct
delete only after successful outcome
transient failures retry
poison messages DLQ
```

## Idempotency

```text
SourceEventId uniqueness
SecurityIncidentId uniqueness
same event duplicate safe
same incident different event safe
manual existing WorkOrder safe
concurrent duplicate safe
```

## Correlation

```text
same correlation ID through workflow
structured logs contain identifiers
WorkOrder retains correlation ID
```

## Boundaries

```text
no cross-schema EF relationships
no WorkOrder writes from SecurityOperations
no SecurityOperations writes from WorkOrderService
```

## Portfolio quality

```text
failure modes understandable
architecture small
distributed-system reasoning visible
no needless messaging framework
```

---

# 99. Definition of Done

The SQS integration is complete when:

```text
✓ exact IncidentCreated.v1 contract implemented
✓ only Critical + asset incidents qualify
✓ event contains required incident/asset/location snapshots
✓ event ID is stable and unique
✓ correlation ID is propagated

✓ qualifying incident + outbox message commit atomically
✓ incident API does not require live SQS
✓ outbox publisher retries failed sends
✓ published_at set only after successful send

✓ Standard SQS queue used
✓ long polling configured
✓ visibility timeout configured
✓ DLQ configured
✓ maxReceiveCount configured

✓ WorkOrderService consumes explicit v1 DTO
✓ valid event creates New Critical WorkOrder
✓ no SecurityOperations DB read needed
✓ SourceEventId retained
✓ SecurityIncidentId retained
✓ snapshots retained

✓ identical duplicate event creates no duplicate
✓ same incident with different event ID creates no duplicate
✓ concurrent duplicates create no duplicate
✓ existing manual WorkOrder prevents duplicate

✓ WorkOrder commit happens before SQS delete
✓ transient failure results in retry
✓ poison message is not silently discarded
✓ repeated poison failure reaches DLQ

✓ correlation is visible across producer and consumer logs
✓ CancellationToken respected
✓ credentials are not stored in source/config
✓ IAM follows least privilege

✓ integration tests cover key failure windows
✓ end-to-end Pharmacy Storage flow works
```

---

# 100. Governing Principle

The messaging workflow exists to demonstrate a real distributed-systems problem, not to maximize infrastructure.

The required reliability model is:

```text
durable producer
        +
at-least-once transport
        +
idempotent consumer
        +
dead-letter isolation
```

In practical terms:

> **Never lose a qualifying incident event, never create two WorkOrders for one incident, and never let one malformed message disappear silently or block healthy work forever.**
