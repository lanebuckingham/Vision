# Vision

**Physical Security Operations and Credential Management for Hospitals**

Vision is a cloud-native SaaS application designed specifically for hospitals, giving security and facilities teams a centralized view of the hospital's physical security posture across buildings, floors, departments, and restricted areas. The platform helps users monitor security assets such as cameras, access-controlled doors, gates, and badge readers; manage security incidents and maintenance work orders; and administer employee and contractor credentials, including issuance, expiration, access level, and revocation. Vision is designed to provide a clear, fast, operational view of whether critical areas of a hospital are secure and whether important security systems are functioning properly.

> **Status:** MVP specification / initial development  
> **Primary goal:** Build a polished, production-shaped portfolio application that demonstrates senior-level software engineering judgment, cloud-native architecture, business understanding, and technical execution.

---

## Why Vision Exists

Hospitals contain a large number of physical-security systems spread across complex facilities. Security teams need to understand, quickly:

- Which critical security assets are functioning?
- Which cameras, doors, gates, or readers are currently degraded or offline?
- Are there active security incidents requiring attention?
- Has maintenance been assigned for failed equipment?
- Which credentials are active, expiring, or revoked?
- Are restricted areas adequately protected?

Vision brings those concerns into a single operational application.

The project intentionally focuses on a believable business problem rather than serving as a technology demonstration disguised as an application. Technology choices are expected to support the business use case and should be defensible from both an engineering and cost perspective.

---

# MVP Scope

Vision's initial version is intentionally small enough to build rapidly while still demonstrating meaningful architecture and business capability.

The MVP contains six primary capabilities.

## 1. Security Operations Dashboard

Provide an immediate hospital-wide view of security health.

The dashboard should show:

- Overall security status
- Security assets online vs. offline
- Active critical incidents
- Open work orders
- Credentials approaching expiration
- Recent security activity or alerts

The dashboard is the most important employer-facing screen and should create a strong first impression.

## 2. Security Asset Inventory

Users can view and inspect the hospital's physical-security equipment.

Initial asset types include:

- Security cameras
- Access-controlled doors
- Badge/card readers
- Security gates

Users should be able to:

- Browse assets
- Search and filter assets
- View asset type
- View location
- View current operational status
- View last service date
- Open an asset detail view
- See basic incident/service history for an asset

## 3. Security Incident Management

Security personnel can create and manage incidents involving hospital security assets or locations.

An incident includes:

- Location
- Associated security asset
- Severity
- Description
- Status
- Creation timestamp
- Resolution information

Initial statuses:

- Open
- Investigating
- Resolved

Incidents should provide a clear operational history and may lead to maintenance work orders.

## 4. Work Order Management

Security-equipment failures can be tracked through a basic maintenance workflow.

Users can:

- Create a work order
- Associate a work order with an incident
- Associate a work order with an asset
- Assign a technician
- Update work-order status
- Add technician notes
- Complete a repair

Initial statuses:

- New
- Assigned
- In Progress
- Completed

This workflow will provide one of the primary demonstrations of asynchronous messaging between services.

## 5. Credential Management

Authorized users can manage hospital employee and contractor access credentials.

Users can:

- View personnel and their credentials
- Issue a credential
- View expiration dates
- Assign a simple access level
- Revoke a credential
- View whether a credential is active, expired, or revoked

Initial access levels:

- General
- Clinical
- Restricted
- Security

The MVP does **not** attempt to model every possible hospital access-control rule.

## 6. Authentication and Role-Based Access

Vision uses Amazon Cognito for authentication.

Initial application roles should include:

- **Security Manager**
- **Technician**
- **Credential Administrator**

The system should demonstrate that different user roles are authorized to perform different actions.

Authorization should use OAuth/OIDC concepts, JWT access tokens, bearer authentication, and ASP.NET Core authorization policies.

---

# Demo Story

The MVP should support one simple, compelling end-to-end demonstration.

A prospective employer opens Vision and immediately sees a hospital security dashboard.

The dashboard indicates:

> **Hospital Security: 97% Operational**

A critical alert identifies:

> **Camera Offline — Pharmacy Storage**

The user then:

1. Opens the affected security asset.
2. Reviews the associated incident.
3. Creates or opens a maintenance work order.
4. Assigns a technician.
5. Updates the work order to **In Progress**.
6. Adds a repair note.
7. Completes the work order.
8. Returns to the dashboard and sees the operational state updated.
9. Opens Credential Management.
10. Finds an employee with a reported lost badge.
11. Revokes the credential.

This workflow should demonstrate the product in roughly five minutes without requiring training or setup.

---

# Architecture Overview

Vision uses a small microservice architecture.

The MVP currently targets three backend services.

## Security Operations Service

Responsible for:

- Security assets
- Asset status
- Security incidents
- Dashboard operational data

## Work Order Service

Responsible for:

- Work orders
- Technician assignment
- Work-order lifecycle
- Technician notes

## Credential Service

Responsible for:

- People relevant to physical access
- Credentials
- Credential status
- Access levels
- Credential issuance and revocation

Authentication is provided by Amazon Cognito rather than a custom identity microservice.

Final service boundaries may evolve slightly during implementation, but additional microservices should not be added without a clear business or architectural reason.

---

# High-Level Architecture

```text
                              GitHub
                        source + public repo
                                |
                                v
                         GitHub Actions
                  build / test / scan / deploy
                                |
               +----------------+----------------+
               |                                 |
               v                                 v
      Azure Static Web Apps              Azure Container Apps
                                                
      Next.js / React                    ASP.NET Core APIs
      TypeScript                         Microservices
      Responsive UI
                                          |
                              +-----------+-----------+
                              |           |           |
                              v           v           v
                           Security     Work       Credential
                           Operations   Orders       Service
                              |           |           |
                              +-----------+-----------+
                                          |
                                          v
                                  Neon PostgreSQL

                           Selected async workflows
                                          |
                                          v
                                      Amazon SQS
```

Authentication:

```text
Browser
   |
   v
Amazon Cognito
   |
   | OAuth / OIDC
   v
JWT access token
   |
   | Bearer token
   v
ASP.NET Core APIs
```

Observability:

```text
Frontend / API Request
          |
          v
ASP.NET Core Service
          |
          +--> PostgreSQL
          |
          +--> SQS
                  |
                  v
             Consumer Service

          OpenTelemetry
   traces / logs / metrics
```

---

# Technology Stack

| Area | Technology |
|---|---|
| Backend | C# / modern .NET / ASP.NET Core |
| Frontend | React / TypeScript / Next.js |
| UI | Responsive / mobile-friendly |
| APIs | REST / OpenAPI |
| Architecture | Microservices |
| Containers | Docker |
| Local Orchestration | Kubernetes using k3d or kind |
| Kubernetes Packaging | Helm where useful |
| Production Containers | Azure Container Apps |
| Frontend Hosting | Azure Static Web Apps |
| Messaging | Amazon SQS |
| Database | PostgreSQL |
| Database Provider | Neon |
| ORM | Entity Framework Core |
| Authentication | Amazon Cognito |
| Auth | OAuth / OIDC / JWT / Bearer tokens |
| Application Patterns | CQRS / MediatR where appropriate |
| Validation | FluentValidation |
| Observability | OpenTelemetry |
| Testing | xUnit / integration tests |
| API Testing | Bruno and/or Postman |
| CI/CD | GitHub Actions |
| Infrastructure as Code | Terraform |
| Source Control | Git / GitHub |

---

# Database Strategy

Vision uses **PostgreSQL hosted by Neon** as its primary database.

Initial deployment will use **Neon Free** during development and early demonstration usage.

The application will benchmark:

- Warm query performance
- Cold database startup behavior
- Connection-establishment time
- API latency
- End-to-end first-request latency

If performance, resource limits, or operational confidence require it, Vision will move to **Neon Launch** while retaining usage-based billing.

A second database technology will **not** be introduced merely for portfolio breadth.

If Neon ultimately creates unacceptable employer-facing cold-start or reliability behavior, the fallback database is **Azure SQL Database Basic**.

---

# Performance Goals

First impressions are a product requirement.

Vision should target:

- Initial page load under **1 second** when practical
- Initial meaningful experience under **2 seconds**
- Normal API calls preferably around **300–500 ms or less**

The production demo should avoid:

- Long wake-up delays
- Broken first requests
- Database-paused errors
- Visible retry behavior
- Infrastructure error messages
- Empty or partially initialized demo states

---

# Runtime and Cost Strategy

Vision has a hard production-infrastructure budget of:

> **$15/month maximum**

Preferred normal cost:

> **$5–$10/month**

The project does **not** optimize for free services at the expense of demo quality.

Priority order:

1. Excellent first impression
2. Reliability
3. Performance
4. Clean operation
5. Low cost

## Warm-Service Strategy

One strategically important Azure Container App should remain warm if doing so materially improves the first employer-facing experience.

The remaining services should scale to zero whenever practical.

The Neon database may also be kept warm or moved to a paid usage tier if benchmarks demonstrate that doing so materially improves the demo.

---

# Local Development

Vision should support both a simple local environment and a Kubernetes learning environment.

## Docker Compose

The normal local-development path should allow the application to run through Docker Compose.

```text
Developer
   |
   v
Docker Compose
   |
   +-- Frontend
   +-- Security Operations Service
   +-- Work Order Service
   +-- Credential Service
   +-- PostgreSQL
```

## Kubernetes

The application should also support local Kubernetes deployment using **k3d** or **kind**.

This exists to demonstrate and practice:

- Deployments
- Services
- ConfigMaps
- Secrets
- Ingress
- Health probes
- Resource requests and limits
- Namespaces
- Helm
- Rolling deployments

Vision intentionally does **not** use a permanent managed Kubernetes cluster in production because its fixed cost is not justified for the expected portfolio workload.

---

# Messaging

Vision uses **Amazon SQS** for selected asynchronous workflows.

The MVP needs only one strong, clearly justified messaging workflow.

Candidate example:

```text
Security incident requires maintenance
             |
             v
Security Operations Service
             |
             v
          Amazon SQS
             |
             v
      Work Order Service
```

Messaging implementation should demonstrate appropriate distributed-system concerns where relevant:

- Idempotency
- Duplicate delivery handling
- Retry behavior
- Dead-letter queues
- Correlation IDs
- Failure isolation
- Eventual consistency
- Trace propagation

---

# Engineering Principles

## Build a Real Product, Not a Technology Checklist

Every significant technology should solve a recognizable problem.

Do not add technologies merely because they look impressive in a README.

## Keep the Architecture Small

The MVP should use approximately three genuine microservices.

Do not split functionality into additional services unless there is a defensible reason.

## Keep Controllers Thin

Business logic should reside in application/domain layers rather than ASP.NET Core controllers.

## Use CQRS and MediatR Selectively

CQRS and MediatR should be applied where they improve clarity or separation.

They should not be forced into trivial functionality.

## Treat Security Seriously

Vision's domain makes secure implementation especially important.

The project should demonstrate:

- Authentication
- Policy-based authorization
- Least privilege
- Input validation
- Secure secret handling
- Appropriate logging
- Auditability where useful

## Treat Failure as Normal

Distributed workflows should assume:

- Services can be temporarily unavailable
- Messages may be delivered more than once
- Requests can fail
- Retries can occur

## Treat Performance as a Feature

Infrastructure savings are not valuable if they create an embarrassing portfolio experience.

## Treat Cost as an Architectural Constraint

Architecture decisions should account for both engineering value and operational expense.

---

# Testing Strategy

Vision should include:

- Unit tests using xUnit
- Integration tests for important application paths
- API-level tests
- Validation tests
- Authorization tests
- Persistence tests where useful

For the MVP, test effort should prioritize important business behavior over achieving an arbitrary coverage percentage.

---

# CI/CD

GitHub Actions should provide a production-shaped pipeline.

Expected stages include:

1. Restore dependencies
2. Build frontend
3. Build backend services
4. Run automated tests
5. Perform static analysis
6. Build Docker images
7. Perform container/security scanning where practical
8. Publish deployment artifacts/images
9. Deploy
10. Perform basic post-deployment verification

---

# Infrastructure as Code

Terraform should define cloud resources wherever practical.

Likely Terraform-managed infrastructure includes:

- Azure Container Apps
- Supporting Azure infrastructure
- Amazon Cognito
- Amazon SQS
- IAM resources
- Budget alerts
- Cost-control resources
- Configuration needed for deployment

Undocumented manual cloud configuration should be minimized.

---

# Repository Direction

Initial target structure:

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
|       |-- 001-microservices.md
|       |-- 002-messaging.md
|       |-- 003-database.md
|       |-- 004-container-apps.md
|       `-- ...
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
`-- .github/
    `-- workflows/
```

This structure is provisional until implementation begins.

---

# Portfolio Goals

Vision should demonstrate more than familiarity with frameworks.

A prospective employer should be able to see evidence of:

- Solid technical acumen
- Senior-level engineering judgment
- Clean modern implementation
- Distributed-system fundamentals
- Cloud-native architecture
- Business-domain understanding
- Cost awareness
- Security awareness
- Reliability thinking
- Performance awareness
- Testing discipline
- CI/CD
- Infrastructure automation
- Observability
- Clear technical communication

The repository itself is part of the portfolio.

Architecture diagrams, ADRs, tradeoff explanations, performance decisions, cost decisions, and implementation documentation should help explain **why** Vision was built the way it was.

---

# Out of Scope for the MVP

The following ideas may be valuable later but should not block the initial build:

- Complex access approval workflows
- Visitor management
- Physical badge printing
- Detailed access-zone inheritance
- Real-time hardware telemetry integrations
- Floor-plan mapping
- Preventive-maintenance scheduling
- Advanced SLA engines
- Complex technician scheduling
- Advanced analytics
- AI-assisted analysis
- Recurring-failure intelligence
- Full audit-reporting suites
- Native mobile applications
- Multi-hospital tenancy
- Large permission matrices
- Kafka
- AWS Lambda
- Amazon EventBridge
- Managed cloud Kubernetes

---

# Future Possibilities

After the MVP is complete, Vision could evolve toward capabilities such as:

- Advanced access-request approvals
- Automated credential expiration
- Visitor/contractor access
- Recurring equipment-failure detection
- Asset reliability analytics
- Rich audit history
- Hospital floor/security-zone visualization
- SLA tracking
- Preventive maintenance
- AI-generated incident summaries
- AI-generated equipment service-history summaries
- Suggested related historical incidents

AI features should remain advisory and should not autonomously grant access or perform sensitive security decisions.

---

# Current Development Priority

The MVP should prioritize, in this order:

1. Polished Security Operations Dashboard
2. Security Asset Inventory
3. Incident Management
4. Work Order Workflow
5. Credential Issue/Revocation
6. Cognito Authentication and Authorization
7. One meaningful SQS workflow
8. OpenTelemetry
9. Terraform
10. Local Kubernetes support

If time becomes constrained, employer-visible product quality takes precedence over adding invisible infrastructure complexity.

---

# North-Star Rule

> **Vision should look like a small real enterprise product designed by an experienced engineer—not a collection of technologies assembled for a portfolio.**

When implementation decisions are unclear, choose the option that best preserves:

- The hospital-security business story
- The polished demo experience
- Architectural clarity
- Senior-level engineering judgment
- The MVP scope
- The $15/month infrastructure ceiling
