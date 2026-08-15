# Vision — WorkOrderService Acceptance Test Specification

**Status:** Approved pre-implementation test specification  
**Service:** `WorkOrderService`  
**Target implementation agent:** Amazon Kiro  
**Target repository location:** `docs/testing/work-order-acceptance-tests.md`  
**Depends on:** `docs/business-domain-specification.md`, `docs/service-specifications/work-order-service.md`, `docs/integration-contracts/incident-created-sqs-contract.md`  
**Primary framework:** xUnit  
**Scope:** Phase 4 Work Orders and Messaging, with authorization-ready cases reserved for Phase 5

---

# 1. Purpose

This document defines the acceptance-test contract for Vision's `WorkOrderService`.

The goal is not arbitrary code coverage.

The goal is to prove that the implementation preserves the business rules, API contracts, persistence guarantees, service boundaries, asynchronous idempotency, and five-minute demo workflow already approved for Vision.

Kiro should use these scenarios to guide:

- domain unit tests,
- validation tests,
- API integration tests,
- persistence tests,
- SQS consumer tests,
- failure-window tests,
- authorization tests when Cognito is added,
- a small end-to-end demo acceptance suite.

---

# 2. Testing Principle

Tests should answer:

> **Does the implemented WorkOrder slice behave like the approved Vision product under both normal and failure conditions?**

Prefer tests that protect business behavior over tests that merely mirror implementation details.

Do not create fragile tests around:

- private methods,
- exact internal class layouts,
- exact logging wording,
- EF-generated SQL strings,
- framework implementation details.

---

# 3. Source-of-Truth Precedence

When deciding expected behavior:

```text
1. Business & Domain Specification
2. WorkOrderService Detailed Specification
3. IncidentCreated SQS Integration Contract
4. Technology Specification
5. README
```

Tests must not silently redefine product behavior.

---

# 4. Test Levels

Use four primary test levels.

```text
Domain Unit Tests
    |
    v
Application / Validation Tests
    |
    v
API / Persistence Integration Tests
    |
    v
Messaging / End-to-End Acceptance Tests
```

Not every scenario needs every level.

Choose the cheapest level that reliably protects the behavior.

---

# 5. Test Categories

Required categories:

```text
WO-DOM   Domain behavior
WO-VAL   Validation
WO-API   REST API
WO-DB    Persistence / constraints
WO-QRY   Query behavior
WO-MSG   SQS messaging / idempotency
WO-FAIL  Failure windows
WO-AUTH  Authorization
WO-DEMO  End-to-end employer demo
```

These IDs are documentation identifiers.

Kiro does not have to encode the IDs in C# class names.

---

# 6. Phase Classification

Tests are classified as:

```text
MUST-PASS-PHASE-4
READY-FOR-PHASE-5
OPTIONAL-POLISH
```

`MUST-PASS-PHASE-4` should be implemented with WorkOrderService and messaging.

`READY-FOR-PHASE-5` documents expected authorization behavior but does not block Phase 4 before Cognito exists.

---

# 7. Domain Fixtures

Tests should use small explicit fixtures.

Example helpers are acceptable:

```text
CreateNewWorkOrder()
CreateAssignedWorkOrder()
CreateInProgressWorkOrder()
CreateCompletedWorkOrder()
CreateActiveTechnician()
CreateInactiveTechnician()
```

Fixtures must not hide important state transitions.

A test for assignment should visibly communicate that the initial state is `New`.

---

# 8. Time Assertions

Avoid brittle exact-clock assertions.

Prefer:

```text
before <= AssignedAt <= after
```

or use an injectable clock if the repository already uses one.

Do not introduce a large time abstraction solely for tests unless it meaningfully improves domain determinism.

All persisted timestamps must represent UTC `DateTimeOffset` values.

---

# 9. Database Integration Strategy

Persistence tests should use real PostgreSQL behavior where the test is specifically protecting:

- PostgreSQL constraints,
- unique partial indexes,
- enum/string mappings,
- owned technician-note persistence,
- concurrency/idempotency constraints.

Do not rely solely on EF Core's in-memory provider for these behaviors.

---

# 10. Required Domain Lifecycle

The lifecycle under test is:

```text
New -> Assigned -> InProgress -> Completed
```

No state skipping is part of the MVP.

`Completed` is terminal.

---

# 11. WO-DOM-001 — New WorkOrder Initial State

**Priority:** MUST-PASS-PHASE-4

Given a valid newly created WorkOrder  
When the aggregate is constructed  
Then:

```text
Status == New
AssignedTechnicianId == null
AssignedAt == null
StartedAt == null
CompletedAt == null
```

And:

```text
CreatedAt populated
UpdatedAt populated
SecurityAssetId preserved
Title preserved
Description preserved
Priority preserved
```

---

# 12. WO-DOM-002 — Empty Asset ID Rejected

**Priority:** MUST-PASS-PHASE-4

Given:

```text
SecurityAssetId == Guid.Empty
```

When a WorkOrder is created  
Then creation must be rejected.

No invalid aggregate should be persisted.

---

# 13. WO-DOM-003 — Blank Title Rejected

**Priority:** MUST-PASS-PHASE-4

Given a blank/whitespace title  
When a WorkOrder is created  
Then creation is rejected.

---

# 14. WO-DOM-004 — Blank Description Rejected

**Priority:** MUST-PASS-PHASE-4

Given a blank/whitespace description  
When a WorkOrder is created  
Then creation is rejected.

---

# 15. WO-DOM-005 — Assign Active Technician

**Priority:** MUST-PASS-PHASE-4

Given:

```text
WorkOrder.Status == New
Technician.IsActive == true
```

When `AssignTechnician` is executed  
Then:

```text
Status == Assigned
AssignedTechnicianId == Technician.Id
AssignedAt populated
UpdatedAt advanced
```

---

# 16. WO-DOM-006 — Inactive Technician Cannot Be Assigned

**Priority:** MUST-PASS-PHASE-4

Given:

```text
WorkOrder.Status == New
Technician.IsActive == false
```

When assignment is attempted  
Then the operation is rejected.

The WorkOrder remains:

```text
Status == New
AssignedTechnicianId == null
AssignedAt == null
```

---

# 17. WO-DOM-007 — Assigned WorkOrder Cannot Be Assigned Again

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Assigned
```

When another assignment is attempted  
Then the operation is rejected.

Existing assignment metadata remains unchanged.

---

# 18. WO-DOM-008 — InProgress WorkOrder Cannot Be Reassigned

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
```

When assignment is attempted  
Then the operation is rejected.

---

# 19. WO-DOM-009 — Completed WorkOrder Cannot Be Reassigned

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Completed
```

When assignment is attempted  
Then the operation is rejected.

---

# 20. WO-DOM-010 — Start Assigned Work

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Assigned
AssignedTechnicianId != null
```

When `StartWork` is executed  
Then:

```text
Status == InProgress
StartedAt populated
UpdatedAt advanced
```

---

# 21. WO-DOM-011 — New Work Cannot Start

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == New
```

When `StartWork` is attempted  
Then the transition is rejected.

---

# 22. WO-DOM-012 — InProgress Work Cannot Start Again

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
```

When `StartWork` is attempted  
Then the operation is rejected.

Do not replace the original `StartedAt`.

---

# 23. WO-DOM-013 — Completed Work Cannot Start

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Completed
```

When `StartWork` is attempted  
Then the operation is rejected.

---

# 24. WO-DOM-014 — Complete With Summary

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
AssignedTechnicianId != null
```

And a nonblank completion summary  
When completion occurs  
Then:

```text
Status == Completed
CompletionSummary == supplied summary
CompletedAt populated
UpdatedAt advanced
```

---

# 25. WO-DOM-015 — Complete With Technician Note Only

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
AssignedTechnicianId != null
```

And:

```text
CompletionSummary blank/null
at least one meaningful TechnicianNote exists
```

When completion occurs  
Then completion succeeds.

This protects the approved rule that completion information may be either:

```text
completion summary
OR
final technician note
```

---

# 26. WO-DOM-016 — Completion Without Repair Information Rejected

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
CompletionSummary blank/null
Notes collection empty
```

When completion is attempted  
Then completion is rejected.

Status remains:

```text
InProgress
```

And:

```text
CompletedAt == null
```

---

# 27. WO-DOM-017 — Assigned Work Cannot Complete

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Assigned
```

When completion is attempted  
Then the transition is rejected.

---

# 28. WO-DOM-018 — New Work Cannot Complete

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == New
```

When completion is attempted  
Then the transition is rejected.

---

# 29. WO-DOM-019 — Completed Work Is Terminal

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Completed
```

Verify the aggregate cannot:

```text
be assigned
start work
complete again
accept a new technician note
return to an earlier state
```

---

# 30. WO-DOM-020 — Completion Timestamp Is Stable

**Priority:** MUST-PASS-PHASE-4

Given a completed WorkOrder  
When an invalid later completion attempt occurs  
Then the original:

```text
CompletedAt
CompletionSummary
```

must not be overwritten.

---

# 31. Technician Note Rules

Technician notes are owned by the WorkOrder aggregate.

They are not independent application business entities.

---

# 32. WO-DOM-021 — Add Note To Assigned Work

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Assigned
AssignedTechnicianId == technician.Id
```

When a valid note is added  
Then:

```text
one note exists
note.WorkOrderId == WorkOrder.Id
note.TechnicianId == technician.Id
note.Content preserved
note.CreatedAt populated
```

---

# 33. WO-DOM-022 — Add Note To InProgress Work

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == InProgress
```

When the assigned technician adds a valid note  
Then it succeeds.

---

# 34. WO-DOM-023 — Blank Technician Note Rejected

**Priority:** MUST-PASS-PHASE-4

Given blank/whitespace content  
When a note is added  
Then it is rejected.

---

# 35. WO-DOM-024 — Note Length Limit

**Priority:** MUST-PASS-PHASE-4

Given content greater than the configured 2,000-character maximum  
When a note is added  
Then validation rejects it.

Boundary case:

```text
exactly 2000 characters -> accepted
2001 characters         -> rejected
```

---

# 36. WO-DOM-025 — New Work Cannot Receive Technician Note

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == New
```

When a note is added  
Then it is rejected.

---

# 37. WO-DOM-026 — Completed Work Cannot Receive Technician Note

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Status == Completed
```

When a note is added  
Then it is rejected.

---

# 38. WO-DOM-027 — Notes Preserve Chronology

**Priority:** MUST-PASS-PHASE-4

Given several notes  
When WorkOrder detail is returned  
Then notes are ordered:

```text
CreatedAt ASC
```

---

# 39. Technician Domain Tests

Protect assignment eligibility and technician identity semantics.

---

# 40. WO-DOM-028 — Active Technician Is Assignable

**Priority:** MUST-PASS-PHASE-4

Given a valid Technician with:

```text
IsActive == true
```

Then it may be used in assignment.

---

# 41. WO-DOM-029 — Inactive Technician Remains Historical

**Priority:** MUST-PASS-PHASE-4

Given a Technician assigned to a historical WorkOrder  
When the Technician later becomes inactive  
Then historical WorkOrder assignment remains readable.

Do not erase historical assignment data.

---

# 42. WO-VAL-001 — Manual Create Validation

**Priority:** MUST-PASS-PHASE-4

Reject requests with:

```text
empty SecurityAssetId
blank Title
Title > 150
blank Description
Description > 2000
invalid Priority
```

---

# 43. WO-VAL-002 — Assignment Validation

**Priority:** MUST-PASS-PHASE-4

Reject:

```text
empty WorkOrderId
empty TechnicianId
```

before unnecessary persistence operations where practical.

---

# 44. WO-VAL-003 — Note Validation

**Priority:** MUST-PASS-PHASE-4

Reject:

```text
blank Content
Content > 2000
```

---

# 45. WO-VAL-004 — Completion Validation

**Priority:** MUST-PASS-PHASE-4

A blank completion summary is not automatically invalid at request-validation level because an existing technician note may satisfy the domain completion requirement.

The handler/domain must evaluate the aggregate state.

This test prevents FluentValidation from accidentally narrowing the approved domain rule.

---

# 46. WO-API-001 — Health Endpoint

**Priority:** MUST-PASS-PHASE-4

Given WorkOrderService is running  
When:

```text
GET /health
```

Then:

```text
200 OK
```

and the response identifies WorkOrderService appropriately.

---

# 47. WO-API-002 — List WorkOrders

**Priority:** MUST-PASS-PHASE-4

When:

```text
GET /api/v1/work-orders
```

Then:

```text
200 OK
```

and response follows:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

---

# 48. WO-QRY-001 — Default WorkOrder Sort

**Priority:** MUST-PASS-PHASE-4

Given WorkOrders with different creation timestamps  
When the unfiltered list is requested  
Then items are ordered:

```text
CreatedAt DESC
```

---

# 49. WO-QRY-002 — Status Filter

**Priority:** MUST-PASS-PHASE-4

For each:

```text
New
Assigned
InProgress
Completed
```

When requested as `status`  
Then only matching WorkOrders are returned.

Invalid status:

```text
400
```

---

# 50. WO-QRY-003 — Priority Filter

**Priority:** MUST-PASS-PHASE-4

For:

```text
Low
Medium
High
Critical
```

return only matching WorkOrders.

Invalid priority:

```text
400
```

---

# 51. WO-QRY-004 — Technician Filter

**Priority:** MUST-PASS-PHASE-4

Given several assigned WorkOrders  
When:

```text
technicianId={id}
```

Then only WorkOrders assigned to that Technician are returned.

---

# 52. WO-QRY-005 — Asset Filter

**Priority:** MUST-PASS-PHASE-4

When:

```text
assetId={securityAssetId}
```

Then WorkOrderService returns WorkOrders referencing that external asset ID without querying SecurityOperations tables.

---

# 53. WO-QRY-006 — Incident Filter

**Priority:** MUST-PASS-PHASE-4

When:

```text
incidentId={securityIncidentId}
```

Then zero or one WorkOrder is returned under valid MVP data.

No direct SecurityOperations database read is performed.

---

# 54. WO-QRY-007 — Search

**Priority:** MUST-PASS-PHASE-4

Search case-insensitively across:

```text
Title
Description
AssetNameSnapshot
LocationNameSnapshot
AssignedTechnician.DisplayName
```

Use realistic cases such as:

```text
Pharmacy
camera
Marcus
```

---

# 55. WO-QRY-008 — Pagination Defaults

**Priority:** MUST-PASS-PHASE-4

Omitting pagination produces:

```text
page = 1
pageSize = 25
```

---

# 56. WO-QRY-009 — Pagination Limits

**Priority:** MUST-PASS-PHASE-4

Reject:

```text
page < 1
pageSize < 1
pageSize > 100
```

with:

```text
400 Bad Request
```

---

# 57. WO-API-003 — Get WorkOrder Detail

**Priority:** MUST-PASS-PHASE-4

Given a known WorkOrder  
When:

```text
GET /api/v1/work-orders/{id}
```

Then return:

```text
core fields
assigned technician context
timestamps
completion information
notes
```

Notes appear chronologically.

---

# 58. WO-API-004 — Unknown WorkOrder Detail

**Priority:** MUST-PASS-PHASE-4

Unknown ID:

```text
GET /api/v1/work-orders/{id}
```

returns:

```text
404
application/problem+json
```

---

# 59. WO-API-005 — Manual WorkOrder Creation

**Priority:** MUST-PASS-PHASE-4

Given a valid request  
When:

```text
POST /api/v1/work-orders
```

Then:

```text
201 Created
```

And persisted WorkOrder has:

```text
server-generated Id
Status == New
CreatedAt populated
UpdatedAt populated
SourceEventId == null
```

---

# 60. WO-API-006 — Manual WorkOrder With Incident

**Priority:** MUST-PASS-PHASE-4

Given valid:

```text
SecurityIncidentId
SecurityAssetId
snapshots
```

When manually created  
Then the incident ID is preserved as an external reference.

No cross-schema FK is created or queried.

---

# 61. WO-API-007 — Duplicate Manual Incident Rejected

**Priority:** MUST-PASS-PHASE-4

Given a WorkOrder already references Incident X  
When another manual create request references Incident X  
Then:

```text
409 Conflict
```

and no second WorkOrder is persisted.

---

# 62. WO-API-008 — Assign Technician

**Priority:** MUST-PASS-PHASE-4

Given:

```text
New WorkOrder
active Technician
```

When:

```text
POST /api/v1/work-orders/{id}/assignment
```

Then:

```text
200
Status == Assigned
AssignedTechnician.Id == technicianId
AssignedAt populated
```

---

# 63. WO-API-009 — Unknown Technician Assignment

**Priority:** MUST-PASS-PHASE-4

Unknown technician ID returns:

```text
404
```

No WorkOrder mutation occurs.

---

# 64. WO-API-010 — Inactive Technician Assignment

**Priority:** MUST-PASS-PHASE-4

Inactive technician assignment returns:

```text
409
```

WorkOrder remains `New`.

---

# 65. WO-API-011 — Assignment Wrong State

**Priority:** MUST-PASS-PHASE-4

Assignment against:

```text
Assigned
InProgress
Completed
```

returns:

```text
409
```

---

# 66. WO-API-012 — Start Work

**Priority:** MUST-PASS-PHASE-4

Given an Assigned WorkOrder  
When:

```text
POST /api/v1/work-orders/{id}/start
```

Then:

```text
200
Status == InProgress
StartedAt populated
```

---

# 67. WO-API-013 — Start Wrong State

**Priority:** MUST-PASS-PHASE-4

Starting:

```text
New
InProgress
Completed
```

returns:

```text
409
```

---

# 68. WO-API-014 — Add Repair Note

**Priority:** MUST-PASS-PHASE-4

Given valid work state and content  
When:

```text
POST /api/v1/work-orders/{id}/notes
```

Then:

```text
201 Created
```

and the note appears in later WorkOrder detail.

---

# 69. WO-API-015 — Add Note To Completed Work

**Priority:** MUST-PASS-PHASE-4

Returns:

```text
409
```

or equivalent business-conflict Problem Details.

No note is persisted.

---

# 70. WO-API-016 — Complete With Summary

**Priority:** MUST-PASS-PHASE-4

Given InProgress WorkOrder  
When valid summary is posted to:

```text
POST /api/v1/work-orders/{id}/complete
```

Then:

```text
200
Status == Completed
CompletedAt populated
CompletionSummary preserved
```

---

# 71. WO-API-017 — Complete With Existing Note And Blank Summary

**Priority:** MUST-PASS-PHASE-4

Given:

```text
InProgress
one valid TechnicianNote
```

When completion request contains no meaningful summary  
Then completion succeeds.

---

# 72. WO-API-018 — Complete Without Repair Information

**Priority:** MUST-PASS-PHASE-4

Given:

```text
InProgress
no notes
blank/null summary
```

Then:

```text
400
```

or validation-oriented Problem Details.

WorkOrder remains `InProgress`.

---

# 73. WO-API-019 — Completed Is Terminal

**Priority:** MUST-PASS-PHASE-4

After successful completion:

```text
assignment -> rejected
start -> rejected
new note -> rejected
complete again -> rejected
```

No timestamp is replaced.

---

# 74. WO-API-020 — Technician List

**Priority:** MUST-PASS-PHASE-4

When:

```text
GET /api/v1/technicians
```

Then default behavior returns active technicians only if `activeOnly=true` is the selected default contract.

Sort:

```text
DisplayName ASC
```

---

# 75. WO-QRY-010 — Technician Search

**Priority:** MUST-PASS-PHASE-4

Search across:

```text
DisplayName
Email
Specialty
```

case-insensitively.

---

# 76. WO-API-021 — Technician Detail

**Priority:** MUST-PASS-PHASE-4

Known technician returns:

```text
Id
DisplayName
Email
Specialty
IsActive
CreatedAt
```

Unknown ID:

```text
404
```

---

# 77. WO-API-022 — WorkOrder Summary

**Priority:** MUST-PASS-PHASE-4

When:

```text
GET /api/v1/work-orders/summary
```

Then:

```text
openCount = New + Assigned + InProgress
```

Completed is excluded.

`byStatus` reflects persisted data.

---

# 78. WO-DB-001 — WorkOrder Schema Ownership

**Priority:** MUST-PASS-PHASE-4

Verify WorkOrderService-owned business tables exist only in:

```text
work_orders.*
```

The DbContext must not expose:

```text
SecurityIncident
SecurityAsset
Location
Building
Person
Credential
```

as WorkOrder-owned DbSets.

---

# 79. WO-DB-002 — No Cross-Schema Foreign Keys

**Priority:** MUST-PASS-PHASE-4

Verify:

```text
SecurityAssetId
SecurityIncidentId
```

have no database foreign keys into:

```text
security_operations.*
```

---

# 80. WO-DB-003 — Technician Assignment FK

**Priority:** MUST-PASS-PHASE-4

Verify `AssignedTechnicianId` uses an internal WorkOrderService relationship to Technician.

Historical WorkOrders remain readable.

---

# 81. WO-DB-004 — TechnicianNote Ownership

**Priority:** MUST-PASS-PHASE-4

Verify technician notes persist and load through WorkOrder.

The DbContext should not require a top-level public `DbSet<TechnicianNote>`.

---

# 82. WO-DB-005 — Unique SecurityIncidentId

**Priority:** MUST-PASS-PHASE-4

Given WorkOrder A references:

```text
SecurityIncidentId = X
```

Attempt to persist WorkOrder B with the same non-null X.

Database must reject the duplicate.

Null incident IDs may appear on multiple unrelated manual WorkOrders.

---

# 83. WO-DB-006 — Unique SourceEventId

**Priority:** MUST-PASS-PHASE-4

Given WorkOrder A stores:

```text
SourceEventId = E
```

Attempt another WorkOrder with E.

Database must reject the duplicate.

Null `SourceEventId` remains valid for multiple manually created WorkOrders.

---

# 84. WO-DB-007 — Seed Is Idempotent

**Priority:** MUST-PASS-PHASE-4

Run seed twice.

Verify:

```text
technician count unchanged
seeded WorkOrder count unchanged
no duplicate notes
no duplicate deterministic IDs
```

---

# 85. WO-DB-008 — Seed Covers Lifecycle

**Priority:** MUST-PASS-PHASE-4

Seed must provide believable examples of:

```text
New
Assigned
InProgress
Completed
```

---

# 86. WO-DB-009 — Cross-Service Seed IDs Are Real

**Priority:** MUST-PASS-PHASE-4

Seeded:

```text
SecurityAssetId
SecurityIncidentId
```

must match intended deterministic SecurityOperations seed landmarks.

Do not accept arbitrary orphan UUIDs in the approved seed set.

---

# 87. Messaging Test Scope

The following tests validate the event contract and SQS consumer behavior.

They should be implemented against the formal `IncidentCreated.v1` contract.

---

# 88. WO-MSG-001 — Valid Incident Event Creates WorkOrder

**Priority:** MUST-PASS-PHASE-4

Given:

```text
eventType = vision.security-operations.incident-created.v1
severity = Critical
asset present
```

When consumed  
Then exactly one WorkOrder is persisted.

Verify:

```text
Status == New
Priority == Critical
SecurityAssetId copied
SecurityIncidentId copied
AssetNameSnapshot copied
LocationNameSnapshot copied
SourceEventId copied
CorrelationId copied
```

---

# 89. WO-MSG-002 — Event Title Mapping

**Priority:** MUST-PASS-PHASE-4

Given incident title:

```text
Pharmacy storage camera offline
```

Created WorkOrder title follows the approved mapping, e.g.:

```text
Repair: Pharmacy storage camera offline
```

Do not create an empty or generic untraceable title.

---

# 90. WO-MSG-003 — Event Description Mapping

**Priority:** MUST-PASS-PHASE-4

Incident description becomes useful initial WorkOrder repair context.

The consumer must not query SecurityOperations DB to reconstruct it.

---

# 91. WO-MSG-004 — Same Event Delivered Twice

**Priority:** MUST-PASS-PHASE-4

Given identical Event E is consumed twice  
Then:

```text
one WorkOrder total
```

Second delivery is treated as successful idempotent processing.

---

# 92. WO-MSG-005 — Same Incident, Different Event IDs

**Priority:** MUST-PASS-PHASE-4

Given:

```text
Event E1 -> Incident X
Event E2 -> Incident X
```

When both are consumed  
Then:

```text
one WorkOrder total for Incident X
```

---

# 93. WO-MSG-006 — Existing Manual WorkOrder Wins Cardinality

**Priority:** MUST-PASS-PHASE-4

Given a manual WorkOrder already references Incident X  
When automatic IncidentCreated event for X arrives  
Then:

```text
no second WorkOrder
message handled idempotently
```

---

# 94. WO-MSG-007 — Non-Critical Event Rejected

**Priority:** MUST-PASS-PHASE-4

Given a v1 payload with:

```text
severity = High
```

When received on the automatic WorkOrder queue  
Then:

```text
no WorkOrder created
message classified as contract violation
```

It should remain eligible for DLQ redrive rather than being silently discarded.

---

# 95. WO-MSG-008 — Missing Asset Rejected

**Priority:** MUST-PASS-PHASE-4

Given an invalid automatic-work message without required asset context  
Then:

```text
no WorkOrder
permanent contract failure
```

---

# 96. WO-MSG-009 — Unsupported Version Rejected

**Priority:** MUST-PASS-PHASE-4

Given:

```text
eventType = vision.security-operations.incident-created.v2
```

while only v1 is supported  
Then:

```text
no WorkOrder
v1 deserializer/handler does not treat it as v1
message remains available for DLQ handling
```

---

# 97. WO-MSG-010 — Invalid JSON

**Priority:** MUST-PASS-PHASE-4

Given malformed JSON  
When consumer receives it  
Then:

```text
no partial WorkOrder
message not silently deleted
failure logged
event eventually eligible for DLQ
```

---

# 98. WO-MSG-011 — Correlation Preserved

**Priority:** MUST-PASS-PHASE-4

Given:

```text
CorrelationId = C
```

Then persisted WorkOrder contains:

```text
CorrelationId == C
```

and consumer logging scope includes C where practical.

---

# 99. WO-MSG-012 — Source Event Preserved

**Priority:** MUST-PASS-PHASE-4

Given event ID E  
Then:

```text
WorkOrder.SourceEventId == E
```

---

# 100. Failure-Window Tests

Distributed systems fail between operations.

These tests are especially valuable for the portfolio because they prove the implementation's reliability model rather than only the happy path.

---

# 101. WO-FAIL-001 — WorkOrder Commit Before Message Delete

**Priority:** MUST-PASS-PHASE-4

Verify consumer ordering:

```text
database commit
THEN
SQS DeleteMessage
```

A test double/fake SQS client may be used to assert delete is not requested before persistence completes.

---

# 102. WO-FAIL-002 — Database Failure Does Not Acknowledge Message

**Priority:** MUST-PASS-PHASE-4

Simulate WorkOrder database failure.

Expected:

```text
no committed WorkOrder
DeleteMessage not called
message eligible for retry
```

---

# 103. WO-FAIL-003 — Crash After Commit Before Delete

**Priority:** MUST-PASS-PHASE-4

Simulate:

```text
WorkOrder INSERT commits
consumer exits/fails before DeleteMessage
```

Then redeliver the same event.

Expected:

```text
existing WorkOrder recognized
no duplicate created
second attempt safely acknowledges
```

---

# 104. WO-FAIL-004 — Concurrent Duplicate Delivery

**Priority:** MUST-PASS-PHASE-4

Run two consumer operations concurrently for the same EventId/IncidentId.

Expected:

```text
one WorkOrder persists
other path resolves uniqueness race as idempotent outcome
```

No unhandled database error escapes as repeated poison processing.

---

# 105. WO-FAIL-005 — Poison Message Not Silently Deleted

**Priority:** MUST-PASS-PHASE-4

Malformed/unsupported message:

```text
does not create WorkOrder
does not call DeleteMessage as success
```

This protects DLQ observability.

---

# 106. WO-FAIL-006 — Consumer Cancellation

**Priority:** MUST-PASS-PHASE-4

Given service shutdown cancellation  
Then hosted consumer:

```text
stops receiving new work
passes CancellationToken to async I/O
does not acknowledge uncommitted work
```

---

# 107. WO-FAIL-007 — Query Cancellation Propagates

**Priority:** MUST-PASS-PHASE-4

API request cancellation must flow into EF Core async operations.

The test may inspect handler behavior or use a cancellable integration path.

Do not use `.Result` or `.Wait()`.

---

# 108. Producer/Outbox Acceptance Cases

These technically execute in SecurityOperationsService but are required for the WorkOrder workflow to be accepted end-to-end.

---

# 109. WO-MSG-013 — Critical + Asset Creates One Outbox Event

**Priority:** MUST-PASS-PHASE-4

Given a qualifying Critical asset incident  
Then the same transaction creates:

```text
SecurityIncident
one IncidentCreated.v1 OutboxMessage
```

---

# 110. WO-MSG-014 — Nonqualifying Incident Creates No Outbox Event

**Priority:** MUST-PASS-PHASE-4

Verify no automatic event for:

```text
Critical + no asset
High + asset
Medium + asset
Low + asset
```

---

# 111. WO-FAIL-008 — Outbox Atomicity

**Priority:** MUST-PASS-PHASE-4

If qualifying incident persistence succeeds but required outbox persistence fails  
Then the entire database transaction rolls back.

Do not permit:

```text
committed qualifying incident
without required outbox record
```

---

# 112. WO-FAIL-009 — SQS Down After Incident Commit

**Priority:** MUST-PASS-PHASE-4

Simulate SQS send failure.

Expected:

```text
incident remains committed
outbox remains unpublished
attempt count increments
event remains durable
```

---

# 113. WO-FAIL-010 — Publish Success Then Publisher Crash

**Priority:** MUST-PASS-PHASE-4

Simulate:

```text
SQS SendMessage succeeds
publisher crashes before PublishedAt is saved
```

Next publication may send duplicate event.

Consumer must still produce:

```text
one WorkOrder
```

This is a cross-component acceptance scenario.

---

# 114. WO-MSG-015 — Outbox Event ID Stable Across Retry

**Priority:** MUST-PASS-PHASE-4

Publish the same outbox record multiple times.

Verify:

```text
eventId unchanged
correlationId unchanged
payload business identity unchanged
```

---

# 115. WO-MSG-016 — Outbox Success Marks Published

**Priority:** MUST-PASS-PHASE-4

After confirmed successful `SendMessage`:

```text
PublishedAt populated
```

The record must no longer appear as unpublished.

---

# 116. WO-MSG-017 — Outbox Failure Remains Unpublished

**Priority:** MUST-PASS-PHASE-4

After failed send:

```text
PublishedAt == null
AttemptCount incremented
LastError recorded safely
```

---

# 117. DLQ Infrastructure Acceptance

These may be implemented as infrastructure verification rather than ordinary xUnit tests if that is more appropriate.

---

# 118. WO-MSG-018 — Standard Queue Is Used

**Priority:** MUST-PASS-PHASE-4

Infrastructure configuration should create a Standard SQS queue, not FIFO.

No application correctness assumption depends on ordering.

---

# 119. WO-MSG-019 — DLQ Redrive Policy Exists

**Priority:** MUST-PASS-PHASE-4

Primary queue must reference:

```text
vision-*-incident-created-dlq
```

with approved:

```text
maxReceiveCount = 5
```

or the environment-specific equivalent.

---

# 120. WO-MSG-020 — Poison Message Reaches DLQ

**Priority:** MUST-PASS-PHASE-4

In an environment capable of SQS integration testing:

1. enqueue permanently invalid message,
2. allow repeated receives without success acknowledgement,
3. verify eventual presence in DLQ.

This may be a slower integration/infrastructure test and need not run on every local unit-test execution.

---

# 121. API Error Contract Tests

Errors should use useful Problem Details.

---

# 122. WO-API-023 — Validation Error Shape

**Priority:** MUST-PASS-PHASE-4

Invalid request returns:

```text
400
application/problem+json
```

with understandable validation information.

Do not expose stack traces.

---

# 123. WO-API-024 — Conflict Error Shape

**Priority:** MUST-PASS-PHASE-4

Invalid lifecycle transition returns:

```text
409
application/problem+json
```

with a useful title/detail.

---

# 124. WO-API-025 — Unexpected Failure Shape

**Priority:** MUST-PASS-PHASE-4

Unexpected server exception returns:

```text
500
```

without:

```text
stack trace
connection string
SQL text
AWS credentials
internal secret values
```

---

# 125. OpenAPI Acceptance

**Priority:** MUST-PASS-PHASE-4

OpenAPI should describe:

```text
WorkOrder routes
Technician routes
query parameters
request DTOs
response DTOs
enum values
important status codes
```

A reviewer should be able to understand the WorkOrder API without reading source code.

---

# 126. Query Efficiency Acceptance

These are review/test-assisted acceptance rules.

---

# 127. WO-QRY-011 — WorkOrder List Does Not Load Notes

**Priority:** MUST-PASS-PHASE-4

List query should not load entire `TechnicianNote` collections for every row.

Verify by implementation review and/or query instrumentation.

---

# 128. WO-QRY-012 — Read Queries Use No Tracking

**Priority:** MUST-PASS-PHASE-4

Read-only list/detail queries should use `AsNoTracking()` where appropriate.

This may be verified primarily in code review.

---

# 129. WO-QRY-013 — Filtering Happens In Database

**Priority:** MUST-PASS-PHASE-4

WorkOrder filters/search must not fetch an entire table and then filter in memory.

Verification may combine integration tests with code review.

---

# 130. Service Boundary Acceptance

---

# 131. WO-DB-010 — No SecurityOperations Mutation

**Priority:** MUST-PASS-PHASE-4

Search/review WorkOrderService code.

It must not:

```text
UPDATE security_operations.*
INSERT security_operations.*
DELETE security_operations.*
```

---

# 132. WO-DB-011 — No Cross-Service EF Navigation

**Priority:** MUST-PASS-PHASE-4

WorkOrder domain/persistence must not contain navigation properties to:

```text
SecurityIncident
SecurityAsset
Location
Building
```

External IDs/snapshots only.

---

# 133. WO-DB-012 — No Credential Coupling

**Priority:** MUST-PASS-PHASE-4

WorkOrderService must not use CredentialService `Person` as Technician.

No shared Employee aggregate should appear.

---

# 134. Authorization Acceptance — Phase 5

The following scenarios are defined now but are classified:

```text
READY-FOR-PHASE-5
```

They become required once Cognito and backend authorization are enabled.

---

# 135. WO-AUTH-001 — Security Manager Views All WorkOrders

**Priority:** READY-FOR-PHASE-5

Authenticated `SecurityManager` can:

```text
GET /api/v1/work-orders
GET /api/v1/work-orders/{id}
GET /api/v1/work-orders/summary
```

---

# 136. WO-AUTH-002 — Security Manager Creates WorkOrder

**Priority:** READY-FOR-PHASE-5

`SecurityManager` can:

```text
POST /api/v1/work-orders
```

---

# 137. WO-AUTH-003 — Security Manager Assigns Technician

**Priority:** READY-FOR-PHASE-5

`SecurityManager` can:

```text
POST /api/v1/work-orders/{id}/assignment
```

---

# 138. WO-AUTH-004 — Security Manager Cannot Perform Technician Repair Action

**Priority:** READY-FOR-PHASE-5

Under the approved role matrix, SecurityManager is not the Technician actor for:

```text
start
technician notes
complete
```

unless the project owner later explicitly broadens the demo role.

Expected:

```text
403
```

---

# 139. WO-AUTH-005 — Technician Views Own Assigned Work

**Priority:** READY-FOR-PHASE-5

Authenticated Technician can see WorkOrders assigned to their Technician identity.

---

# 140. WO-AUTH-006 — Technician Cannot View Another Technician's Work

**Priority:** READY-FOR-PHASE-5

Attempting detail access to another Technician's assigned WorkOrder returns:

```text
403
```

or an approved not-found masking strategy if later selected consistently.

Do not rely on frontend filtering.

---

# 141. WO-AUTH-007 — Technician Starts Own Work

**Priority:** READY-FOR-PHASE-5

Assigned authenticated Technician can start the WorkOrder.

Different Technician:

```text
403
```

---

# 142. WO-AUTH-008 — Technician Adds Note To Own Work

**Priority:** READY-FOR-PHASE-5

Assigned authenticated Technician succeeds.

Different Technician:

```text
403
```

---

# 143. WO-AUTH-009 — Technician Completes Own Work

**Priority:** READY-FOR-PHASE-5

Assigned authenticated Technician succeeds when lifecycle/completion requirements are met.

Different Technician:

```text
403
```

---

# 144. WO-AUTH-010 — Technician Cannot Create WorkOrder

**Priority:** READY-FOR-PHASE-5

Returns:

```text
403
```

---

# 145. WO-AUTH-011 — Technician Cannot Assign

**Priority:** READY-FOR-PHASE-5

Returns:

```text
403
```

---

# 146. WO-AUTH-012 — Credential Administrator Has No WorkOrder Admin Rights

**Priority:** READY-FOR-PHASE-5

CredentialAdministrator attempting WorkOrder mutations returns:

```text
403
```

---

# 147. WO-AUTH-013 — Unauthenticated Protected Mutation

**Priority:** READY-FOR-PHASE-5

Returns:

```text
401
```

---

# 148. Frontend Acceptance

The frontend does not replace backend tests, but the Phase 4 slice is not done unless the business workflow is usable.

---

# 149. WO-DEMO-001 — WorkOrder List Is Understandable

**Priority:** MUST-PASS-PHASE-4

First-time reviewer can identify:

```text
title
asset
location
priority
status
technician
```

without knowing database IDs.

---

# 150. WO-DEMO-002 — Critical Work Is Obvious

**Priority:** MUST-PASS-PHASE-4

Critical WorkOrder is visually distinguishable using more than color alone.

---

# 151. WO-DEMO-003 — Assignment UI Uses Active Technicians

**Priority:** MUST-PASS-PHASE-4

Normal assignment chooser does not offer inactive technicians as ordinary choices.

---

# 152. WO-DEMO-004 — Lifecycle Controls Match State

**Priority:** MUST-PASS-PHASE-4

A WorkOrder screen must not show meaningless actions.

Examples:

```text
New        -> Assign
Assigned   -> Start Work
InProgress -> Add Note / Complete
Completed  -> no mutation controls
```

Backend remains authoritative.

---

# 153. WO-DEMO-005 — Mutation Failure Is Visible

**Priority:** MUST-PASS-PHASE-4

If API mutation fails:

```text
UI does not optimistically pretend success
useful error shown
current persisted status retained/reloaded
```

---

# 154. WO-DEMO-006 — Loading State

**Priority:** MUST-PASS-PHASE-4

WorkOrder data pages have an intentional loading state.

Do not render fake production values while loading.

---

# 155. WO-DEMO-007 — Empty State

**Priority:** MUST-PASS-PHASE-4

No matching WorkOrders shows a useful empty state rather than a broken table.

---

# 156. WO-DEMO-008 — Error State

**Priority:** MUST-PASS-PHASE-4

Failed read displays a recoverable/useful error state.

No indefinite spinner.

---

# 157. Pharmacy Storage End-to-End Acceptance

This is the most important WorkOrder demo scenario.

It should be possible to run manually and, where practical, automate parts of it.

---

# 158. WO-DEMO-009 — Async Pharmacy WorkOrder Appears

**Priority:** MUST-PASS-PHASE-4

Given the approved Pharmacy Storage camera:

```text
Pharmacy Storage Camera 02
Status = Offline
```

And a qualifying Critical incident is created  
When outbox publication and SQS consumption complete  
Then a new WorkOrder becomes discoverable by:

```text
SecurityIncidentId
```

---

# 159. WO-DEMO-010 — Pharmacy WorkOrder Context

**Priority:** MUST-PASS-PHASE-4

The automatically created WorkOrder clearly shows:

```text
Critical priority
Pharmacy Storage Camera 02
Pharmacy Storage
source incident context
New status
```

No cross-service database lookup is required to render basic asset/location snapshots.

---

# 160. WO-DEMO-011 — Assign Pharmacy WorkOrder

**Priority:** MUST-PASS-PHASE-4

Security Manager selects an active seeded Technician.

Then:

```text
Status = Assigned
Technician visible
AssignedAt visible
```

---

# 161. WO-DEMO-012 — Start Pharmacy Repair

**Priority:** MUST-PASS-PHASE-4

Technician starts the assigned WorkOrder.

Then:

```text
Status = InProgress
StartedAt visible
```

---

# 162. WO-DEMO-013 — Add Pharmacy Repair Note

**Priority:** MUST-PASS-PHASE-4

Add a believable note such as:

```text
Replaced damaged PoE patch cable and verified stable camera feed.
```

Then the note remains visible in WorkOrder history/detail.

---

# 163. WO-DEMO-014 — Complete Pharmacy Repair

**Priority:** MUST-PASS-PHASE-4

Complete WorkOrder with valid repair information.

Then:

```text
Status = Completed
CompletedAt visible
repair information visible
```

---

# 164. WO-DEMO-015 — Repair Handoff To Security Operations Preserves Ownership

**Priority:** MUST-PASS-PHASE-4

After WorkOrder completion:

```text
WorkOrderService does not directly mutate SecurityAsset or SecurityIncident tables
```

The broader demo may use SecurityOperations API/system-supported orchestration to:

```text
asset -> Operational
incident -> Resolved
dashboard -> improved
```

---

# 165. Bounded Async UI Acceptance

If frontend polling is used to wait for the asynchronously created WorkOrder:

```text
poll is bounded
poll stops after WorkOrder appears
poll does not continue indefinitely
```

If no WorkOrder appears quickly:

```text
show "still being created" / pending state
```

rather than silently failing.

---

# 166. Cancellation / Async Code Review Gate

Before Phase 4 acceptance, review for:

```text
CancellationToken propagated to EF
CancellationToken propagated to SQS SDK calls where supported
no .Result
no .Wait()
no GetAwaiter().GetResult()
```

This is a code-review acceptance gate even where direct automated testing is impractical.

---

# 167. Logging Review Gate

Verify structured logs support debugging the workflow using:

```text
WorkOrderId
EventId
IncidentId
CorrelationId
TechnicianId where appropriate
```

Do not require exact log-message strings.

Verify secrets/tokens are not logged.

---

# 168. Test Data Safety Gate

All test/seed data must remain fictional.

Do not introduce:

```text
real patient data
PHI
real hospital employee PII
real credentials
```

---

# 169. Minimum Test Suite Before Kiro Declares WorkOrder Complete

The following groups must pass:

```text
WO-DOM-001 through WO-DOM-029
WO-VAL-001 through WO-VAL-004

core WO-API scenarios
WO-QRY filter/search/pagination scenarios
WO-DB ownership/uniqueness/seed scenarios

WO-MSG valid + duplicate + invalid cases
WO-FAIL commit/delete + duplicate race + outbox cases

WO-DEMO Pharmacy workflow
```

Authorization scenarios become blocking in Phase 5.

---

# 170. Suggested xUnit Organization

A practical structure:

```text
tests/
└── WorkOrderService.Tests/
    ├── Domain/
    │   ├── WorkOrderLifecycleTests.cs
    │   ├── WorkOrderAssignmentTests.cs
    │   └── TechnicianNoteTests.cs
    │
    ├── Application/
    │   ├── CreateWorkOrderTests.cs
    │   ├── AssignTechnicianTests.cs
    │   ├── StartWorkTests.cs
    │   ├── CompleteWorkOrderTests.cs
    │   └── QueryTests.cs
    │
    ├── Integration/
    │   ├── WorkOrderApiTests.cs
    │   ├── TechnicianApiTests.cs
    │   ├── WorkOrderPersistenceTests.cs
    │   └── SeedTests.cs
    │
    └── Messaging/
        ├── IncidentCreatedConsumerTests.cs
        ├── IdempotencyTests.cs
        └── FailureWindowTests.cs
```

Exact folder naming may follow repository conventions.

Do not create separate projects for every small test category unless it materially improves execution/isolation.

---

# 171. Test Naming Style

Prefer behavior-readable names, for example:

```text
AssignTechnician_WhenTechnicianIsActive_TransitionsToAssigned

AssignTechnician_WhenTechnicianIsInactive_RejectsAssignment

Complete_WhenSummaryBlankButRepairNoteExists_CompletesWorkOrder

ConsumeIncidentCreated_WhenEventAlreadyProcessed_DoesNotCreateDuplicate

ConsumeIncidentCreated_WhenSameIncidentHasDifferentEventId_DoesNotCreateDuplicate
```

Avoid opaque names such as:

```text
Test1
WorkOrderTestA
HandlerWorks
```

---

# 172. What Not To Test Excessively

Do not spend MVP time proving framework behavior such as:

```text
EF Core can SaveChanges
ASP.NET Core DI resolves normal services
System.Text.Json serializes a trivial string
MediatR invokes a handler in isolation
```

Test Vision behavior instead.

---

# 173. Mutation Test Focus

If time is constrained, prioritize mutation behavior over read cosmetics.

Highest-value automated tests are:

```text
assignment rules
start rule
completion rule
technician notes
duplicate incident prevention
SQS idempotency
commit-before-delete
outbox durability
authorization later
```

---

# 174. Test Independence

Tests should not require execution in a particular order.

Each integration test should arrange its own relevant records or use safely reset deterministic fixtures.

Do not make:

```text
Test B depends on Test A creating data
```

---

# 175. Parallel Test Safety

Where integration tests run in parallel, avoid shared mutable IDs that cause false uniqueness conflicts.

If using deterministic demo landmarks, isolate tests that intentionally modify them or reset state between cases.

---

# 176. Acceptance Threshold

Phase 4 WorkOrderService should not be accepted merely because:

```text
build succeeds
UI looks correct
happy path works once
```

Acceptance requires confidence in:

```text
business lifecycle
persistence constraints
duplicate safety
failure behavior
service ownership
API contracts
demo usability
```

---

# 177. ChatGPT Review After Kiro Implementation

When Kiro completes the WorkOrder slice, provide ChatGPT the updated repository.

Review will compare implementation against this specification and specifically inspect:

```text
test coverage of lifecycle transitions
test coverage of completion-with-note
test coverage of database uniqueness
test coverage of duplicate SQS delivery
test coverage of crash-after-commit window
test coverage of poison-message behavior
API response semantics
frontend state-dependent controls
```

---

# 178. Phase 4 Exit Criteria

Phase 4 is ready to move toward Credentials/Authorization when:

```text
✓ manual WorkOrder workflow passes
✓ automatic WorkOrder workflow passes
✓ full lifecycle passes
✓ notes pass
✓ summary/read APIs pass
✓ PostgreSQL constraints pass
✓ duplicate EventId safe
✓ duplicate IncidentId safe
✓ concurrent duplicate safe
✓ transient failure retries safely
✓ poison failure remains observable
✓ outbox durability proven
✓ Pharmacy Storage demo workflow succeeds
✓ WorkOrderService does not cross service ownership boundaries
```

---

# 179. Governing Test Principle

The WorkOrder tests should make this statement defensible:

> **Vision can reliably turn a qualifying security incident into one—and only one—maintenance WorkOrder, assign it to an eligible technician, carry it through repair, preserve repair evidence, and complete it without violating service ownership even when asynchronous delivery is duplicated or temporarily fails.**

That is the behavior the suite exists to protect.
