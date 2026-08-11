---
inclusion: always
---

# Vision Product Context

## Product

**Vision** is a Physical Security Operations and Credential Management SaaS application designed specifically for hospitals.

Vision gives hospital security and facilities teams a clear, centralized view of the operational status of physical-security systems across buildings, floors, departments, and restricted areas.

## Primary Users

The MVP supports three application personas:

- Security Manager
- Technician
- Credential Administrator

## MVP Business Capabilities

Vision's one-week MVP includes:

1. Security Operations Dashboard
2. Security Asset Inventory
3. Security Incident Management
4. Work Order Management
5. Credential Management
6. Authentication and Role-Based Authorization

## Core Demo Story

The employer-facing demo should support this flow:

1. A user opens Vision and immediately sees hospital-wide security status.
2. A critical alert identifies an offline camera in Pharmacy Storage.
3. The user opens the affected asset and associated incident.
4. A maintenance work order is created or opened.
5. A technician is assigned.
6. The technician moves the work order through In Progress to Completed and records a repair note.
7. The dashboard reflects the improved operational state.
8. The user opens Credential Management.
9. A lost employee badge is located and revoked.

The complete demo should be understandable in approximately five minutes.

## Product Priorities

When making implementation decisions, prioritize in this order:

1. Excellent employer-facing first impression
2. Correct business behavior
3. Reliability
4. Fast performance
5. Clear maintainable architecture
6. Security
7. Automated testing
8. Low infrastructure cost
9. Additional technical sophistication

## MVP Scope Guardrail

Do not expand the MVP without a clear requirement.

The following are intentionally deferred:

- Visitor management
- Physical badge printing
- Complex access-zone inheritance
- Real-time hardware telemetry integrations
- Floor-plan mapping
- Preventive-maintenance scheduling
- Advanced SLA engines
- Complex technician scheduling
- Advanced analytics
- AI-assisted features
- Multi-hospital tenancy
- Large permission matrices

## North-Star Rule

Vision must feel like a small, real enterprise product designed by an experienced software engineer, not a collection of technologies assembled for a portfolio.
