---
inclusion: always
---

# Vision Architecture Rules

## Architectural Intent

Vision is deliberately designed as a small cloud-native microservice application that demonstrates senior-level engineering judgment while remaining inexpensive and achievable within a one-week MVP.

Architecture must remain proportional to the business problem.

## Microservices

Target three MVP services:

- SecurityOperationsService
- WorkOrderService
- CredentialService

Do not create additional microservices merely for separation or portfolio breadth.

If a new service is proposed, explain:

1. The business capability it owns.
2. Why it needs independent deployment/runtime behavior.
3. Why keeping the behavior in an existing service is inferior.
4. The additional operational cost and complexity.

## Production Hosting

Backend containers run in Azure Container Apps.

Do not introduce AKS, EKS, or another permanent cloud Kubernetes cluster for the MVP.

Local Kubernetes exists for learning and portability.

## Runtime Strategy

Optimize for the best employer-facing first impression.

- Keep one strategically important Azure Container App warm if required.
- SecurityOperationsService is the initial candidate because it powers the dashboard.
- Allow less frequently used services to scale to zero.
- Keep the database warm only if benchmarks show that database wake-up materially harms the demo.

Make warm/cold decisions from measurements rather than assumptions.

## Database

Use PostgreSQL through Neon.

- Start with Neon Free.
- Benchmark warm and cold behavior.
- Move to Neon Launch when justified.
- Azure SQL Database Basic is the fallback.
- Do not introduce a second database technology without a concrete domain requirement.

A single physical database instance is acceptable for the MVP, but preserve service-level logical ownership.

## Messaging

Use Amazon SQS for one strong asynchronous workflow rather than introducing messaging everywhere.

Initial preferred workflow:

```text
Critical maintenance-relevant incident
        ↓
SecurityOperationsService
        ↓
Integration event
        ↓
Amazon SQS
        ↓
WorkOrderService
        ↓
Work order creation/processing
```

Design asynchronous consumers assuming:

- At-least-once delivery
- Duplicate messages
- Consumer failure
- Retries
- Dead-letter handling
- Eventual consistency

Consumers must be idempotent where duplicate effects would be harmful.

## Observability

Use OpenTelemetry to make important workflows traceable across:

- ASP.NET Core
- PostgreSQL
- HTTP calls
- SQS producer/consumer boundaries

Propagate correlation/trace context where technically practical.

## Architecture Changes

Do not silently deviate from established architecture.

If a meaningful architecture change is justified, propose an ADR describing:

- Context
- Decision
- Alternatives
- Tradeoffs
- Cost impact
- Performance impact
- Security impact
