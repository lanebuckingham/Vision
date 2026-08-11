# Vision — Technology Architecture Specification

## 1. Document Purpose

This document defines the current technical architecture and engineering constraints for **Vision**, a portfolio-grade cloud-native application intended to demonstrate senior-level software engineering capability.

It is designed to be used by both humans and AI coding agents as an implementation baseline.

This document covers:

- Technology choices
- Deployment architecture
- Cloud services
- Data persistence
- Authentication and authorization
- Messaging
- Observability
- Testing
- CI/CD
- Infrastructure as Code
- Performance targets
- Cost constraints
- Repository organization
- Architectural principles

Business use cases, domain modeling, workflows, detailed product features, API contracts, and detailed data schemas will be specified separately.

---

# 2. Project Name

## Vision

The project is named **Vision** because the product is intended to give organizations a clear, immediate view of the security posture and operational status of their facilities and physical-security assets.

The initial target domain is physical-security operations for complex facilities such as:

- Hospitals
- Research organizations
- Laboratories
- Regulated commercial environments
- Government contractors
- Other enterprises operating sensitive or restricted facilities

Vision will eventually provide visibility into areas such as:

- Physical security assets
- Security cameras
- Access-controlled doors
- Security gates
- Badge/card readers
- Credential issuance and revocation
- Access zones
- Security incidents
- Maintenance and work orders
- Asset health
- Operational alerts
- Security-related dashboards

Detailed product requirements will be documented separately.

---

# 3. Primary Engineering Goals

Vision must demonstrate:

- Strong modern software engineering practices
- Senior-level architectural judgment
- Cloud-native application design
- Microservice architecture
- Secure authentication and authorization
- Asynchronous messaging
- Relational data modeling
- CI/CD
- Infrastructure as Code
- Observability
- Automated testing
- Cost-conscious cloud architecture
- Production-quality user experience

The project should feel like a real SaaS product rather than a tutorial or portfolio coding exercise.

---

# 4. Technology Stack

## Backend

- **C#**
- **Modern .NET**
- **ASP.NET Core Web APIs**
- **3–5 genuine microservices**
- **REST APIs**
- **OpenAPI / Swagger**
- **Dependency Injection**
- **ILogger**
- **async/await**
- **CancellationToken**
- **CQRS / MediatR where appropriate**
- **FluentValidation**
- **Entity Framework Core**

## Frontend

- **React**
- **TypeScript**
- **Next.js**
- **Responsive, mobile-friendly UI**

The frontend should be optimized for fast perceived performance and a polished first impression.

## Containers

- **Docker**

Every backend microservice will be containerized.

## Orchestration

- **Kubernetes locally**
- Preferred local Kubernetes environment: **k3d** or **kind**
- **Helm** where useful

A permanent Kubernetes cluster will **not** be maintained in the public cloud because its fixed cost is not justified for a low-traffic portfolio application.

## Cloud Containers

- **Azure Container Apps**

Azure Container Apps will host the production backend microservices.

Scale-to-zero will be used selectively to minimize idle cost.

## Frontend Hosting

- **Azure Static Web Apps**

The Next.js frontend should remain primarily focused on presentation and interaction, while ASP.NET Core services remain the authoritative backend APIs.

## Messaging

- **Amazon SQS**

SQS will be used for asynchronous communication where the domain genuinely benefits from decoupled processing.

AWS Lambda and Amazon EventBridge are intentionally excluded from the initial architecture.

## Authentication

- **Amazon Cognito**
- **OAuth 2.0 / OpenID Connect**
- **JWT access tokens**
- **Bearer-token API authentication**

ASP.NET Core APIs will validate Cognito-issued tokens and enforce authorization policies.

## Database

- **PostgreSQL**
- **Neon**
- **Entity Framework Core**

PostgreSQL is the primary and only planned database technology unless a future business requirement genuinely justifies another persistence model.

## Observability

- **OpenTelemetry**

Used for:

- Distributed tracing
- Logs
- Metrics
- Correlation across service boundaries
- Request tracing through asynchronous workflows

## Testing

- **xUnit**
- Unit tests
- Integration tests
- API-level tests where appropriate

## API Testing

- **Bruno and/or Postman**

## Source Control

- **Git**
- **GitHub**

The repository will be public and intentionally suitable for review by prospective employers.

## CI/CD

- **GitHub Actions**

## Infrastructure as Code

- **Terraform**

---

# 5. Database Architecture Decision

## Primary Database

Vision will use:

> **PostgreSQL hosted by Neon**

### Initial Environment

Begin with **Neon Free** during:

- Development
- Early deployment
- Low-traffic demonstration usage

### Performance Validation

Benchmark:

- Warm query performance
- Cold-start performance
- Connection-establishment time
- API latency with the database awake
- API latency after database inactivity
- End-to-end first-request latency

Testing must be performed under realistic portfolio usage conditions.

### Upgrade Path

If Neon Free does not provide sufficient performance, responsiveness, resource limits, or operational confidence:

> Upgrade to **Neon Launch**

The preference is to preserve usage-based billing rather than introduce a large fixed monthly database cost.

### Single Database Technology Principle

Do **not** introduce DynamoDB, MongoDB, or another database technology merely for portfolio breadth.

A second persistence technology may be introduced only if:

- A concrete domain requirement justifies it
- PostgreSQL would be a poor fit for the workload
- The added operational complexity is defensible

### Fallback Database

If Neon serverless PostgreSQL produces unacceptable:

- Cold-start behavior
- Reliability
- Availability
- Operational uncertainty
- Employer-facing demo experience

the fallback is:

> **Azure SQL Database Basic**

---

# 6. Cost Requirements

Cost is a first-class architectural constraint.

## Monthly Infrastructure Budget

### Preferred Range

- **$5–$10 per month**

### Hard Ceiling

- **$15 per month total**

The combined monthly cost of all production services should not intentionally exceed this amount.

The budget includes, where applicable:

- Azure hosting
- AWS services
- Database hosting
- Logging/monitoring
- Storage
- Network-related charges
- Any other cloud infrastructure required by Vision

The domain name may be treated separately as an annual operating expense.

## Cost Philosophy

Vision should not optimize for "free at all costs."

Priority order:

1. Excellent employer-facing experience
2. Reliability
3. Fast performance
4. Clean operation
5. Low predictable cost

A paid service is acceptable when it materially improves the quality of the demo and total infrastructure remains within budget.

---

# 7. Performance Requirements

The public demo must create a strong first impression.

## Frontend Performance

Preferred:

- Initial page load under **1 second**

Absolute target:

- Initial meaningful page experience under **2 seconds**

## API Performance

Normal API calls should preferably complete in approximately:

- **300–500 ms or less**

This is a target rather than a guarantee for every operation.

## Employer-Facing Experience

The production environment should avoid:

- Long cold-start delays
- Wake-up screens
- Database-paused errors
- Visible retry behavior
- Broken first requests
- Empty or partially initialized demo states
- Infrastructure-related error messages
- Multi-second waits for normal navigation

---

# 8. First-Impression Runtime Strategy

The preferred deployment strategy is:

## BEST FIRST IMPRESSION

> **Keep one strategically important Azure Container App warm.**

This warm service should be the component most likely to influence the first employer-facing interaction with the backend.

The exact service will be selected after microservice boundaries and primary user flows are finalized.

## Database

The database may also be kept warm if real performance measurements show that serverless wake-up behavior materially harms the demo experience.

This decision must be made from benchmark data rather than assumption.

## Remaining Services

All other services should:

> **Scale to zero whenever practical**

Conceptually:

```text
Highest-value first interaction
        |
        v
Strategically warm Container App
        |
        v
Database
(possibly warm if justified)

Less frequently used services
        |
        v
Scale to zero
```

The goal is to spend a small amount of money where it improves perceived quality while preserving aggressive scale-to-zero behavior elsewhere.

---

# 9. High-Level Production Architecture

```text
                         +------------------------+
                         |        GitHub          |
                         | source + public repo   |
                         +-----------+------------+
                                     |
                              GitHub Actions
                                     |
                      build / test / scan / deploy
                                     |
               +---------------------+----------------------+
               |                                            |
               v                                            v
       Azure Static Web Apps                       Azure Container Apps

       React                                            ASP.NET Core
       TypeScript                                       Microservices
       Next.js
       Responsive UI

                                                  +---------+---------+
                                                  |         |         |
                                                  v         v         v
                                               Service A Service B Service C
                                                  |         |         |
                                                  +----+----+----+----+
                                                       |
                                                       v
                                                Neon PostgreSQL
                                                       |
                                                       | async events
                                                       v
                                                   Amazon SQS
                                                       |
                                                       v
                                               consuming service(s)
```

---

# 10. Authentication Flow

```text
User / Browser
     |
     v
Amazon Cognito
     |
     | OAuth / OIDC
     v
JWT access token
     |
     | Authorization: Bearer <token>
     v
ASP.NET Core APIs
```

Authorization should eventually support more than simple "Admin/User" role checks.

Vision is expected to include enterprise-style permission scenarios such as:

- Security administrators
- Facilities/security managers
- Technicians
- Credential administrators
- Auditors
- Site-specific users

The exact authorization model will be defined in the business/domain specification.

---

# 11. Messaging Strategy

Amazon SQS will provide asynchronous communication between selected services.

Example conceptual workflow:

```text
Business operation completed
        |
        v
Domain / integration event
        |
        v
Amazon SQS
        |
   +----+----+
   |         |
   v         v
Consumer A  Consumer B
```

Messaging should be used only where asynchronous behavior is justified.

Important implementation concerns should include, where relevant:

- Idempotent consumers
- Duplicate message handling
- Retry behavior
- Dead-letter queues
- Eventual consistency
- Failure isolation
- Correlation IDs
- Observability across async boundaries

---

# 12. Observability Strategy

OpenTelemetry will instrument the application.

The system should support:

- Request tracing
- Cross-service trace correlation
- Database-span visibility
- Messaging-span visibility
- Structured logging
- Metrics
- Error diagnostics

Conceptually:

```text
Browser / Client
      |
      v
ASP.NET Core Service
      |
      +--> PostgreSQL
      |
      +--> another API
      |
      +--> Amazon SQS
               |
               v
         consuming service

OpenTelemetry correlates the operation across boundaries.
```

Observability is part of the portfolio and should be demonstrable.

---

# 13. Local Development Architecture

The complete application should support a professional local development environment.

## Docker Compose

Developers should be able to run core dependencies and services locally using Docker.

```text
Developer Workstation
        |
        v
Docker Compose
        |
        +-- Frontend
        +-- Microservices
        +-- PostgreSQL
        +-- Supporting local infrastructure
```

## Kubernetes

A second local deployment model should use Kubernetes.

```text
Developer Workstation
        |
        v
Docker
        |
        v
k3d / kind
        |
        v
Kubernetes
        |
        +-- Deployments
        +-- Services
        +-- ConfigMaps
        +-- Secrets
        +-- Ingress
        +-- Health probes
        +-- Resource requests/limits
        +-- Namespaces
        +-- Helm
```

This provides Kubernetes experience without requiring an expensive permanent managed cluster.

---

# 14. Infrastructure as Code

Terraform should define as much cloud infrastructure as practical.

Potential Terraform-managed resources include:

- Azure Container Apps
- Supporting Azure Container Apps infrastructure
- Amazon Cognito
- Amazon SQS
- AWS IAM resources
- Monitoring resources
- Configuration resources
- Budget alerts
- Cost-control resources

Infrastructure should be reproducible and documented.

Avoid relying on undocumented manual portal configuration.

---

# 15. CI/CD Requirements

GitHub Actions should evolve into a production-style pipeline.

Expected stages include:

1. Restore dependencies
2. Build frontend
3. Build backend
4. Run unit tests
5. Run integration tests
6. Static analysis
7. Build Docker images
8. Container/security scanning
9. Publish deployable artifacts/images
10. Deploy infrastructure and/or application
11. Run post-deployment verification where practical

The final workflow should demonstrate modern CI/CD practices without unnecessary complexity.

---

# 16. Architectural Principles

## 16.1 Use a Small Number of Genuine Microservices

Target:

> **Approximately 3–5 microservices**

Each service should own a meaningful business capability.

Do not create microservices merely to increase service count.

## 16.2 Prefer Engineering Judgment Over Pattern Collection

Technologies such as:

- CQRS
- MediatR
- SQS
- caching
- background processing
- specialized persistence

should be introduced only where they solve a real problem.

## 16.3 Keep Business Logic Out of Controllers

ASP.NET Core controllers should remain thin.

Business rules belong in appropriate application/domain layers.

## 16.4 Design for Asynchronous Failure

Where SQS is involved, assume:

- Messages can be delivered more than once
- Consumers can fail
- Downstream services can be unavailable
- Retries can occur

## 16.5 Prefer Explicit Data Ownership

Microservice boundaries should include clear responsibility for data.

The exact database ownership model will be finalized after domain modeling.

## 16.6 Security Is a Core Requirement

Because Vision models physical-security operations, security practices should be treated as first-class design concerns.

The project should demonstrate:

- Strong authentication
- Authorization policies
- Least privilege
- Secure configuration
- Secret handling
- Auditability
- Input validation

## 16.7 Performance Is a Product Feature

Low cost is not allowed to create a poor public demo experience.

Architectural decisions should be judged partly by their impact on perceived speed.

## 16.8 Cost Is a Technical Constraint

Every infrastructure choice should consider:

- Fixed monthly cost
- Idle cost
- Scale-to-zero support
- Usage-based pricing
- Operational value
- Portfolio value

---

# 17. Infrastructure to Avoid Initially

Unless future requirements justify them, avoid:

- Permanent AKS/EKS clusters
- Always-running EC2 instances
- Always-running general-purpose virtual machines
- Managed Kafka/MSK clusters
- Provisioned Redis
- Provisioned OpenSearch/Elasticsearch
- Expensive managed NAT infrastructure
- Large log-retention configurations
- Additional database technologies without a clear requirement
- Unnecessary paid SaaS dependencies

---

# 18. Proposed Repository Structure

```text
/
|-- README.md
|
|-- docs/
|   |-- technology-specification.md
|   |-- architecture.md
|   |
|   |-- architecture-diagrams/
|   |
|   `-- decisions/
|       |-- 001-microservices.md
|       |-- 002-messaging.md
|       |-- 003-database.md
|       |-- 004-container-apps.md
|       |-- 005-authentication.md
|       `-- ...
|
|-- src/
|   |-- frontend/
|   |-- ServiceA/
|   |-- ServiceB/
|   |-- ServiceC/
|   `-- ...
|
|-- tests/
|
|-- deploy/
|   |-- docker/
|   |-- kubernetes/
|   |-- helm/
|   `-- terraform/
|
`-- .github/
    `-- workflows/
```

The exact service names will be defined after the business and domain specifications are completed.

---

# 19. Example Microservice Internal Structure

A service may use a structure similar to:

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
|
`-- Tests/
```

The structure may evolve as implementation begins.

---

# 20. Portfolio Presentation Principles

Vision should communicate senior-level engineering ability even before a reviewer reads the source code.

The repository should eventually include:

- Clear README
- Live demo
- Architecture diagrams
- ADRs
- Service-boundary rationale
- Data-design rationale
- Reliability decisions
- Performance targets
- Cost decisions
- Security considerations
- Testing strategy
- Deployment documentation
- Observability documentation

The purpose is to show not merely that modern technologies were used, but **why the architecture was designed the way it was**.

---

# 21. Current Technology Decision Summary

| Area | Decision |
|---|---|
| Project | Vision |
| Backend | C# / modern .NET / ASP.NET Core |
| Frontend | React / TypeScript / Next.js |
| UI | Responsive / mobile-friendly |
| APIs | REST / OpenAPI |
| Architecture | 3–5 microservices |
| Containers | Docker |
| Local Orchestration | Kubernetes using k3d/kind |
| Kubernetes Packaging | Helm where useful |
| Cloud Containers | Azure Container Apps |
| Frontend Hosting | Azure Static Web Apps |
| Messaging | Amazon SQS |
| Primary Database | PostgreSQL |
| Database Provider | Neon |
| Database Initial Tier | Neon Free |
| Database Upgrade Path | Neon Launch |
| Database Fallback | Azure SQL Database Basic |
| ORM | Entity Framework Core |
| Authentication | Amazon Cognito |
| Auth Protocols | OAuth / OIDC / JWT / Bearer tokens |
| Architecture Patterns | CQRS / MediatR where appropriate |
| Validation | FluentValidation |
| CI/CD | GitHub Actions |
| IaC | Terraform |
| Observability | OpenTelemetry |
| Testing | xUnit / integration tests |
| API Testing | Bruno / Postman |
| Source Control | Git / GitHub |
| Public Repository | Yes |
| Preferred Monthly Cost | $5–$10 |
| Hard Monthly Ceiling | $15 |
| First-Load Target | < 1 second preferred |
| Maximum Initial Experience | < 2 seconds target |
| Runtime Strategy | One strategic Container App kept warm; others scale to zero |
| Database Warm Strategy | Keep warm only if benchmarks justify it |

---

# 22. Decisions Intentionally Deferred

The following will be specified in separate documents.

## Business and Product Specification

- Primary customer types
- Business use cases
- User personas
- Product value proposition
- Feature priorities

## Domain Model

Expected areas include:

- Facilities
- Buildings
- Floors
- Security assets
- Cameras
- Doors
- Gates
- Readers
- Credentials
- Access levels
- Access zones
- People
- Incidents
- Work orders
- Technicians
- Audit events
- Relationships and ownership

## Microservice Boundaries

Final service boundaries will be selected after domain modeling.

## API Contract Design

Endpoints, request/response models, versioning, and error contracts will be defined after service boundaries are finalized.

## Detailed Data Model

PostgreSQL schemas, tables, indexes, relationships, constraints, and migrations will follow the domain model.

## Demo Data Strategy

A realistic seeded demonstration environment will be designed to provide an excellent employer-facing experience.

---

# 23. AI Agent Guardrails

AI agents implementing Vision must treat the decisions in this document as constraints.

Unless another approved specification explicitly supersedes this document, agents should **not**:

- Replace PostgreSQL with another database
- Introduce DynamoDB, MongoDB, or another persistence technology without a documented requirement
- Replace Azure Container Apps with AKS/EKS or another always-on orchestration platform
- Introduce Kafka, EventBridge, or Lambda
- Replace Cognito with another identity provider
- Replace Terraform with another IaC system
- Add additional microservices merely for separation
- Introduce paid infrastructure that risks exceeding the $15/month hard ceiling
- Trade away employer-facing performance solely to minimize cost
- Add architectural patterns where simpler code is sufficient
- Invent domain rules that conflict with future business/domain specifications

If an agent identifies a legitimate reason to deviate from an architectural decision, it should propose the change as an ADR rather than silently implementing the deviation.

---

# 24. Current Status

The core technology architecture for Vision is now substantially defined.

The next specification should focus on:

1. Business use case
2. Target customers
3. User personas
4. Domain model
5. Product capabilities
6. Feature set
7. Key workflows
8. Microservice boundaries

Those decisions will then drive the detailed API, database, and implementation specifications.
