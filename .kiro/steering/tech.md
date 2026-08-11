---
inclusion: always
---

# Vision Technology Stack

Use the following technology decisions unless an approved architecture decision explicitly supersedes them.

## Backend

- C#
- Modern .NET
- ASP.NET Core Web APIs
- REST
- OpenAPI / Swagger
- Entity Framework Core
- FluentValidation
- MediatR / CQRS only where they improve clarity
- ILogger
- async/await
- CancellationToken

## Frontend

- React
- TypeScript
- Next.js
- Responsive, mobile-friendly UI

## Architecture

- Approximately 3 MVP microservices
- Docker containers
- Local Kubernetes using k3d or kind
- Helm where useful
- Azure Container Apps for production backend hosting
- Azure Static Web Apps for frontend hosting

## Data

- PostgreSQL
- Neon as the production database provider
- Neon Free initially
- Neon Launch if performance or operational needs justify it
- Azure SQL Database Basic is the fallback if Neon is unsuitable
- Do not add DynamoDB, MongoDB, or another database merely for technology breadth

## Messaging

- Amazon SQS
- Use asynchronous messaging only where the business workflow benefits from it
- Do not introduce Kafka, EventBridge, or Lambda for the MVP

## Authentication

- Amazon Cognito
- OAuth 2.0
- OpenID Connect
- JWT access tokens
- Bearer-token authentication
- ASP.NET Core policy-based authorization

## DevOps

- Git / GitHub
- Public repository
- GitHub Actions
- Terraform
- Docker Compose
- Local Kubernetes

## Observability

- OpenTelemetry
- Structured logging
- Distributed tracing
- Metrics

## Testing

- xUnit
- Unit tests
- Integration tests
- API tests with Bruno and/or Postman

## Cost

- Preferred production infrastructure cost: $5–$10/month
- Hard production infrastructure ceiling: $15/month
- Do not introduce infrastructure likely to break this ceiling without explicit approval

## Performance

- Initial page load under 1 second preferred
- Initial meaningful experience under 2 seconds
- Normal API calls preferably around 300–500 ms or less
- Employer-facing cold starts must not create a visibly poor experience
