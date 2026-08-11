---
inclusion: always
---

# Vision Project Constraints

These constraints are mandatory unless the project owner explicitly approves a change.

## Schedule

The MVP is intended to be buildable in approximately one week.

Protect the schedule aggressively.

If scope pressure arises, prioritize:

1. Dashboard
2. Asset inventory
3. Incident workflow
4. Work-order workflow
5. Credential issue/revoke
6. Authentication/authorization
7. One SQS workflow
8. OpenTelemetry
9. Terraform
10. Local Kubernetes

A polished partial product is more valuable than a broad but unreliable implementation.

## Cost

Production infrastructure hard ceiling:

> **$15/month**

Preferred:

> **$5–$10/month**

Do not add paid infrastructure that risks exceeding the hard ceiling without explicit approval.

## Performance

Employer-facing experience must be fast.

Targets:

- Initial page under 1 second preferred
- Initial meaningful experience under 2 seconds
- Normal API interactions preferably 300–500 ms or less

Do not optimize for free infrastructure if doing so creates poor visible cold-start behavior.

## Production Runtime

- Azure Static Web Apps for frontend
- Azure Container Apps for backend
- Keep one strategic backend service warm if needed
- Other services should scale to zero where practical
- Database warmth should be decided from benchmarks

## Database

- PostgreSQL
- Neon Free first
- Neon Launch if justified
- Azure SQL Basic fallback
- No additional database technology without a real requirement

## Prohibited MVP Architecture Drift

Do not introduce without explicit approval:

- AKS
- EKS
- Permanent cloud Kubernetes
- Kafka / MSK
- AWS Lambda
- Amazon EventBridge
- DynamoDB
- MongoDB
- Redis
- OpenSearch / Elasticsearch
- Additional microservices
- Native mobile application
- Multi-tenant architecture
- Complex AI functionality

## Product Quality

Do not expose:

- Non-functional buttons
- Placeholder workflows presented as complete
- Broken navigation
- Unhandled error screens
- Infrastructure wake-up messages
- Raw exception details

Seed enough realistic data that the demo appears alive immediately.

## Security

Never commit:

- Passwords
- API keys
- Database credentials
- Cognito secrets
- Access tokens
- Private connection strings

Use environment variables, secret stores, or appropriate deployment configuration.

## Architecture Discipline

When a deviation seems warranted:

1. Stop before implementing the deviation.
2. Explain the problem.
3. Propose the alternative.
4. Describe tradeoffs.
5. Describe cost/performance/security impact.
6. Obtain approval or create an ADR before proceeding.

Do not silently rewrite project architecture.
