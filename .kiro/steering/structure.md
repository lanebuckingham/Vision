---
inclusion: always
---

# Vision Project Structure

Use this repository organization as the default direction.

```text
/
|-- README.md
|
|-- docs/
|   |-- technology-specification.md
|   |-- business-domain-specification.md
|   |-- architecture.md
|   |
|   |-- architecture-diagrams/
|   |
|   `-- decisions/
|
|-- src/
|   |-- frontend/
|   |-- SecurityOperationsService/
|   |-- WorkOrderService/
|   `-- CredentialService/
|
|-- tests/
|
|-- deploy/
|   |-- docker/
|   |-- kubernetes/
|   |-- helm/
|   `-- terraform/
|
|-- .github/
|   `-- workflows/
|
`-- .kiro/
    `-- steering/
```

## MVP Service Boundaries

### SecurityOperationsService

Owns:

- Hospitals/buildings/locations needed by the MVP
- Security assets
- Asset operational status
- Security incidents
- Dashboard operational data

### WorkOrderService

Owns:

- Work orders
- Technician assignment
- Work-order lifecycle
- Technician repair notes

### CredentialService

Owns:

- People relevant to physical access
- Credentials
- Credential state
- Simple access levels
- Credential issuance
- Credential revocation

Authentication is provided by Amazon Cognito and must not become a custom identity microservice.

## Service Internal Structure

Prefer a clean, pragmatic structure similar to:

```text
ServiceName/
|
|-- API/
|
|-- Application/
|   |-- Commands/
|   |-- Queries/
|   |-- Handlers/
|   `-- Validators/
|
|-- Domain/
|
|-- Infrastructure/
|   |-- Persistence/
|   |-- Messaging/
|   `-- Repositories/
```

Do not create layers, projects, interfaces, repositories, or abstractions that provide no concrete value.

## Data Ownership

The MVP may use one physical PostgreSQL/Neon instance, but services must maintain clear logical data ownership.

Do not allow one service to casually reach into another service's EF Core context or tables.

If cross-service information is required, prefer an API contract or approved asynchronous integration.

## Naming

Use business-domain names rather than generic technical names.

Prefer:

- SecurityIncident
- SecurityAsset
- WorkOrder
- Credential

Avoid vague types such as:

- Manager
- Processor
- Helper
- Utility

unless their responsibility is genuinely generic and clear.
