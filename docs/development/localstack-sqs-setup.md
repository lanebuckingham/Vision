# Vision — Kiro Instructions: Add LocalStack SQS Support

**Status:** Phase 4 implementation instructions  
**Target implementation agent:** Amazon Kiro  
**Target repository:** Vision  
**Target repository location:** `docs/development/localstack-sqs-setup.md`  
**Purpose:** Enable local development and integration testing of the approved Amazon SQS workflow without requiring a live AWS account for every developer run.

---

# 1. Objective

Add LocalStack support so that the existing Vision application can exercise the real Amazon SQS programming model locally.

The local workflow must be:

```text
SecurityOperationsService
        |
        | AWS SDK for .NET
        v
LocalStack SQS
        |
        v
vision-dev-incident-created
        |
        v
WorkOrderService
        |
        v
PostgreSQL
```

The application code should continue to use:

```text
IAmazonSQS
SendMessageAsync
ReceiveMessageAsync
DeleteMessageAsync
```

Do not create a custom in-memory message bus for Development.

The implementation should differ between Local and AWS primarily through configuration.

---

# 2. Existing Repository State

The repository currently contains:

```text
docker-compose.yml
```

with:

```text
PostgreSQL only
```

The current compose service is:

```text
postgres
```

There is not yet a LocalStack service.

There is not yet an AWS SQS package reference in the central package-management file.

Phase 4 is the correct point to add both.

---

# 3. Existing Messaging Contracts To Preserve

These instructions supplement, not replace:

```text
docs/integration-contracts/incident-created-sqs-contract.md
docs/integration-contracts/messaging-retry-expectations.md
docs/integration-contracts/dead-letter-queue-behavior.md
docs/integration-contracts/messaging-correlation-strategy.md
```

The local environment must exercise the same important behavior:

```text
Standard SQS queue
transactional outbox
at-least-once delivery
consumer idempotency
60-second visibility timeout
20-second long polling
max receive count = 5
DLQ
14-day DLQ retention
correlation propagation
```

Do not weaken these semantics only because the environment is local.

---

# 4. Design Principle

LocalStack is infrastructure emulation.

It should not become a business abstraction.

Required architecture:

```text
Application
    |
    v
AWS SDK for .NET
    |
    +--> Development: LocalStack endpoint
    |
    +--> Deployed AWS: Amazon SQS endpoint
```

Do not create:

```text
ILocalQueue
IFakeSqs
InMemoryQueue
LocalMessageBroker
RabbitMQ replacement
```

for this workflow.

---

# 5. Add AWS SDK Package

Add the official AWS SQS SDK package to central package management.

In:

```text
Directory.Packages.props
```

add a centrally managed package entry for:

```text
AWSSDK.SQS
```

Use the current stable AWS SDK for .NET package version compatible with the repository's target framework.

Then reference:

```text
AWSSDK.SQS
```

from:

```text
SecurityOperationsService
WorkOrderService
```

Do not add LocalStack.NET unless there is a concrete implementation need.

The official AWS SDK already supports endpoint overrides and keeps the production path cleaner.

---

# 6. Update docker-compose.yml

Extend the existing compose file with:

```text
localstack
```

Keep PostgreSQL unchanged except where dependencies/health checks require minor integration changes.

Recommended shape:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    container_name: vision-postgres
    environment:
      POSTGRES_DB: vision
      POSTGRES_USER: vision
      POSTGRES_PASSWORD: vision_dev
    ports:
      - "5432:5432"
    volumes:
      - vision-pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U vision -d vision"]
      interval: 5s
      timeout: 3s
      retries: 5

  localstack:
    image: localstack/localstack
    container_name: vision-localstack
    ports:
      - "4566:4566"
    environment:
      SERVICES: sqs
      AWS_DEFAULT_REGION: us-east-1
      SQS_ENDPOINT_STRATEGY: path
    volumes:
      - ./deploy/localstack/init:/etc/localstack/init/ready.d
    healthcheck:
      test:
        [
          "CMD",
          "curl",
          "-f",
          "http://localhost:4566/_localstack/health"
        ]
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  vision-pgdata:
```

Do not enable unrelated LocalStack services.

Vision only needs:

```text
SQS
```

for this phase.

---

# 7. LocalStack Authentication Token

Use the LocalStack setup available to the developer's installed/current LocalStack distribution.

If the LocalStack Docker version being used requires:

```text
LOCALSTACK_AUTH_TOKEN
```

do not commit the token.

Allow it to flow from the developer environment, for example:

```yaml
LOCALSTACK_AUTH_TOKEN: ${LOCALSTACK_AUTH_TOKEN:-}
```

Do not put a real LocalStack token in:

```text
docker-compose.yml
appsettings.json
appsettings.Development.json
source code
```

If the locally used LocalStack version does not require a token for the needed SQS capability, the setup should still work without one.

---

# 8. Do Not Pin `latest` Blindly

Kiro should use a deliberate LocalStack image tag.

Preferred:

```text
a specific tested LocalStack version
```

rather than permanently depending on:

```text
latest
```

Once the version used by Vision works, keep it pinned so local behavior does not unexpectedly change.

Document the chosen version in:

```text
docker-compose.yml
```

or the development README.

---

# 9. Create LocalStack Initialization Directory

Create:

```text
deploy/localstack/init/
```

Add:

```text
01-create-sqs.sh
```

This script must run as a LocalStack READY initialization hook.

Purpose:

```text
create DLQ
create primary queue
configure redrive policy
configure visibility timeout
configure long polling
```

The setup must be idempotent enough that restarting/recreating the LocalStack container does not require manual cleanup.

---

# 10. Queue Names

Use:

```text
vision-dev-incident-created
vision-dev-incident-created-dlq
```

These match the approved environment-scoped naming strategy.

Do not use:

```text
queue1
testqueue
my-sqs
```

---

# 11. DLQ Configuration

Local DLQ:

```text
vision-dev-incident-created-dlq
```

Set:

```text
MessageRetentionPeriod = 1209600 seconds
```

which is:

```text
14 days
```

---

# 12. Primary Queue Configuration

Local primary queue:

```text
vision-dev-incident-created
```

Configure:

```text
VisibilityTimeout = 60
ReceiveMessageWaitTimeSeconds = 20
RedrivePolicy.maxReceiveCount = 5
```

The redrive policy points to the local DLQ ARN.

---

# 13. Initialization Script

Implement something conceptually equivalent to:

```bash
#!/usr/bin/env bash

set -euo pipefail

REGION="us-east-1"
QUEUE_NAME="vision-dev-incident-created"
DLQ_NAME="vision-dev-incident-created-dlq"

echo "Creating Vision LocalStack SQS resources..."

DLQ_URL="$(
  awslocal sqs create-queue \
    --queue-name "${DLQ_NAME}" \
    --attributes MessageRetentionPeriod=1209600 \
    --region "${REGION}" \
    --query 'QueueUrl' \
    --output text
)"

DLQ_ARN="$(
  awslocal sqs get-queue-attributes \
    --queue-url "${DLQ_URL}" \
    --attribute-names QueueArn \
    --region "${REGION}" \
    --query 'Attributes.QueueArn' \
    --output text
)"

QUEUE_URL="$(
  awslocal sqs create-queue \
    --queue-name "${QUEUE_NAME}" \
    --attributes \
      VisibilityTimeout=60 \
      ReceiveMessageWaitTimeSeconds=20 \
    --region "${REGION}" \
    --query 'QueueUrl' \
    --output text
)"

REDRIVE_POLICY="$(
  printf '{"deadLetterTargetArn":"%s","maxReceiveCount":"5"}' "${DLQ_ARN}"
)"

awslocal sqs set-queue-attributes \
  --queue-url "${QUEUE_URL}" \
  --attributes "RedrivePolicy=${REDRIVE_POLICY}" \
  --region "${REGION}"

echo "Vision SQS primary queue: ${QUEUE_URL}"
echo "Vision SQS DLQ:           ${DLQ_URL}"
echo "Vision LocalStack SQS initialization complete."
```

Kiro may adjust CLI syntax if necessary for the selected LocalStack/AWS CLI implementation.

The resulting infrastructure semantics are mandatory.

---

# 14. Shell Script Requirements

The init script should:

```text
use set -euo pipefail
fail visibly
print queue creation results
avoid credentials/secrets
be committed to Git
```

Ensure executable permission is preserved:

```bash
chmod +x deploy/localstack/init/01-create-sqs.sh
```

---

# 15. Why Use READY Initialization

Do not require the developer to run:

```text
aws sqs create-queue ...
```

manually every time.

The expected startup should be:

```bash
docker compose up -d
```

and LocalStack should initialize the required queues automatically.

---

# 16. LocalStack Endpoint

For services running directly on the developer's host machine, configure:

```text
http://localhost:4566
```

Recommended setting:

```text
Messaging:IncidentCreated:ServiceUrl
```

Do not hard-code this inside the messaging classes.

---

# 17. Region

Use:

```text
us-east-1
```

for local SQS.

The exact AWS production region can later be deployment configuration.

Do not make business behavior depend on the Region.

---

# 18. Development Configuration Shape

Add a local messaging section to the relevant:

```text
appsettings.Development.json
```

files.

Recommended shared shape:

```json
{
  "Messaging": {
    "IncidentCreated": {
      "QueueName": "vision-dev-incident-created",
      "DeadLetterQueueName": "vision-dev-incident-created-dlq",
      "Region": "us-east-1",
      "ServiceUrl": "http://localhost:4566",
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

The exact nesting may follow the messaging contracts already implemented by Kiro.

Do not duplicate the same option under unrelated configuration names.

---

# 19. Production Configuration

Production must not specify:

```text
http://localhost:4566
```

Production should normally use:

```text
ServiceUrl absent/null
real AWS Region
real queue URL/name from deployment configuration
AWS default credential chain / workload identity
```

The AWS SDK should resolve the actual AWS endpoint.

---

# 20. Local Credentials

AWS SDK requests generally expect credentials for signed AWS requests.

For LocalStack Development, use deliberately fake local credentials only when needed.

Acceptable local values:

```text
AccessKey = test
SecretKey = test
```

These are not secrets.

Preferred implementation behavior:

```text
if ServiceUrl is configured for LocalStack
    construct client with fake BasicAWSCredentials
else
    construct/register normal AWS client using production credential chain
```

Do not use fake credentials in Production.

---

# 21. SQS Client Registration

Create one clean DI registration path.

Conceptually:

```csharp
builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var options = ...;

    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
    };

    if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
    {
        config.ServiceURL = options.ServiceUrl;

        return new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            config);
    }

    return new AmazonSQSClient(config);
});
```

Adapt to the AWS SDK version actually installed.

The important behavior:

```text
LocalStack => endpoint override
AWS       => normal endpoint/credential chain
```

---

# 22. Keep LocalStack Detection Configuration-Based

Do not write:

```csharp
if (environment.IsDevelopment())
{
    // LocalStack
}
```

as the primary decision.

Prefer:

```text
ServiceUrl present => endpoint override
ServiceUrl absent  => real AWS
```

Why:

```text
Development can optionally test real AWS
CI can use LocalStack
deployed Dev can use real AWS
```

Environment name and infrastructure target are separate concerns.

---

# 23. SecurityOperationsService Changes

The SecurityOperations outbox publisher must receive:

```text
IAmazonSQS
Messaging options
ILogger
DbContext scope
```

Its behavior remains:

```text
poll unpublished outbox
SendMessageAsync
mark PublishedAt only after success
```

For LocalStack, `SendMessageAsync` must target the LocalStack queue.

Do not create special local publisher logic.

---

# 24. WorkOrderService Changes

The WorkOrder SQS consumer must receive:

```text
IAmazonSQS
Messaging options
ILogger
IServiceScopeFactory
```

Its behavior remains:

```text
ReceiveMessageAsync
deserialize
validate
idempotently create WorkOrder
commit DB
DeleteMessageAsync
```

For LocalStack, all those operations hit LocalStack.

No local-only consumer implementation.

---

# 25. Queue URL Resolution

Kiro may resolve the queue URL at startup using:

```text
GetQueueUrlAsync(queueName)
```

and retain it in the hosted service/options state.

This is preferred over hard-coding the LocalStack account ID into configuration.

Production may also use:

```text
queue name -> GetQueueUrlAsync
```

if IAM permits and the chosen deployment design supports it.

Alternatively, production Terraform may inject an exact QueueUrl.

Whichever method is chosen, do not hard-code:

```text
000000000000
```

into application business code.

---

# 26. SQS_ENDPOINT_STRATEGY

Configure LocalStack:

```text
SQS_ENDPOINT_STRATEGY=path
```

This makes queue URLs friendlier when running in local/container environments.

Do not build application logic around LocalStack's internal queue URL formatting.

---

# 27. Host vs Container Networking

Current expected local model:

```text
PostgreSQL + LocalStack run in Docker
.NET services run on host
Next.js runs on host
```

Therefore:

```text
LocalStack ServiceUrl = http://localhost:4566
Postgres host         = localhost:5432
```

Later, when .NET services are themselves Dockerized, the in-container LocalStack endpoint will likely become:

```text
http://localstack:4566
```

That should be an environment/config override, not a source-code change.

---

# 28. LocalStack Health Check

Add a compose health check.

The container should become healthy only when its gateway is responsive.

Use LocalStack's health endpoint:

```text
/_localstack/health
```

Do not create an application-level fake readiness flag.

---

# 29. Do Not Make App Startup Depend Too Aggressively On LocalStack

SecurityOperationsService and WorkOrderService should not crash-loop forever solely because LocalStack/SQS is temporarily unavailable.

Expected:

```text
HTTP service can start
hosted publisher/consumer logs SQS errors
retry behavior remains active
```

A missing queue caused by bad configuration should be logged clearly.

---

# 30. Docker Compose Developer Workflow

Document:

```bash
docker compose up -d
```

Then:

```bash
docker compose ps
```

Expected:

```text
vision-postgres     healthy
vision-localstack   healthy
```

---

# 31. Verify Queues

Developer verification command:

```bash
docker exec vision-localstack \
  awslocal sqs list-queues \
  --region us-east-1
```

Expected output contains:

```text
vision-dev-incident-created
vision-dev-incident-created-dlq
```

---

# 32. Inspect Queue Attributes

Primary:

```bash
docker exec vision-localstack \
  awslocal sqs get-queue-attributes \
  --queue-url <PRIMARY_QUEUE_URL> \
  --attribute-names All \
  --region us-east-1
```

Verify:

```text
VisibilityTimeout = 60
ReceiveMessageWaitTimeSeconds = 20
RedrivePolicy exists
```

DLQ:

```text
MessageRetentionPeriod = 1209600
```

---

# 33. Manual Send Test

Before testing the full outbox workflow, provide a manual smoke-test path.

Example:

```bash
docker exec vision-localstack \
  awslocal sqs send-message \
  --queue-url <QUEUE_URL> \
  --message-body '{"test":"vision"}' \
  --region us-east-1
```

Then:

```bash
docker exec vision-localstack \
  awslocal sqs receive-message \
  --queue-url <QUEUE_URL> \
  --region us-east-1
```

This proves LocalStack SQS itself is functioning.

Do not use this dummy message while the real WorkOrder consumer is running unless poison-message behavior is intentionally being tested.

---

# 34. Full Vision Local Test

The meaningful test is not the manual CLI test.

Run:

```text
SecurityOperationsService
WorkOrderService
PostgreSQL
LocalStack
```

Then create:

```text
Critical incident
with SecurityAssetId
```

through:

```text
POST /api/v1/incidents
```

Expected:

```text
incident commits
outbox record created
publisher sends IncidentCreated.v1 to LocalStack
WorkOrder consumer receives it
WorkOrder is created
message is deleted
```

---

# 35. Verify Outbox State

After successful end-to-end processing:

```text
security_operations.outbox_messages
```

should contain the event with:

```text
published_at != null
```

The WorkOrder should contain:

```text
SourceEventId
SecurityIncidentId
CorrelationId
```

matching the event.

---

# 36. Verify Duplicate Safety Locally

Send the same event twice.

Expected:

```text
one WorkOrder
```

The second delivery:

```text
recognized as duplicate/idempotent
deleted successfully
```

Do not treat normal duplicate delivery as poison.

---

# 37. Test Consumer Failure

Stop PostgreSQL or otherwise force WorkOrder persistence failure.

Send a valid IncidentCreated event.

Expected:

```text
consumer cannot commit
message is not deleted
message becomes visible again after visibility timeout
```

Restore PostgreSQL.

Expected:

```text
later delivery succeeds
one WorkOrder created
```

---

# 38. Test Crash-After-Commit Semantics

Where practical through automated test doubles or controlled failure:

```text
commit WorkOrder
fail before DeleteMessage
```

Redelivery must result in:

```text
one WorkOrder
idempotent acknowledgement
```

LocalStack should be usable for integration validation around this workflow.

---

# 39. Test DLQ Behavior

Create a deliberately permanent-invalid message such as:

```json
{
  "eventType": "vision.security-operations.incident-created.v2"
}
```

or malformed JSON.

Allow the WorkOrder consumer to receive it repeatedly without deleting it.

After:

```text
5 failed receives
```

verify it moves to:

```text
vision-dev-incident-created-dlq
```

Do not build code that manually sends it to the DLQ.

Let the queue's redrive policy do the work.

---

# 40. Speeding Up DLQ Tests

Normal local visibility timeout is:

```text
60 seconds
```

Do not change the committed default just to make manual testing fast.

If automated tests need faster failure cycles, they may provision an isolated test queue with a shorter visibility timeout.

Do not alter production-like development semantics globally.

---

# 41. Inspect DLQ

List/get DLQ URL:

```bash
docker exec vision-localstack \
  awslocal sqs get-queue-url \
  --queue-name vision-dev-incident-created-dlq \
  --region us-east-1
```

Receive:

```bash
docker exec vision-localstack \
  awslocal sqs receive-message \
  --queue-url <DLQ_URL> \
  --attribute-names All \
  --message-attribute-names All \
  --region us-east-1
```

Confirm original message body is present.

---

# 42. LocalStack Developer Endpoint

LocalStack provides SQS developer endpoints that can inspect queue messages without consuming them.

Kiro may document this as an optional debugging aid.

Do not make application correctness or tests depend on LocalStack-only endpoints.

Core integration tests should use normal SQS semantics where practical.

---

# 43. Application Logging

When running locally, logs should make the distributed workflow visible.

SecurityOperations:

```text
Outbox message found
EventId
IncidentId
CorrelationId
SQS send success/failure
```

WorkOrder:

```text
SQS receive
EventId
IncidentId
CorrelationId
WorkOrder creation
duplicate detection
DeleteMessage success
```

Do not log secrets.

---

# 44. Configuration Validation

At startup validate messaging settings.

Examples:

```text
QueueName required when messaging enabled
Region required
WaitTimeSeconds 0..20
VisibilityTimeoutSeconds > 0
MaxNumberOfMessages 1..10
Outbox PollInterval > 0
BatchSize > 0
```

Do not silently substitute a different queue name when configuration is invalid.

---

# 45. Messaging Enable/Disable Switch

It is acceptable to have:

```text
Messaging:Enabled
```

or a narrowly scoped equivalent.

Recommended:

```text
Enabled = true
```

for normal Development with LocalStack.

If disabled:

```text
publisher/consumer hosted services do not start
```

Do not simulate successful publication while disabled.

A qualifying incident should still create its durable outbox record if the domain rules require one.

---

# 46. No Automatic Queue Creation From Application Code

The .NET application should not create infrastructure queues during startup.

Queue creation belongs to:

```text
Local: LocalStack init hook
AWS: Terraform
```

The application consumes configured infrastructure.

This keeps responsibilities clean.

---

# 47. Production Terraform Alignment

When Phase 7 Terraform is implemented, it should create the same logical resources:

```text
AWS Standard primary queue
AWS Standard DLQ
redrive policy
visibility timeout
long polling
retention
IAM
```

LocalStack initialization is the local equivalent, not the production provisioning mechanism.

---

# 48. Git Ignore / Secrets

Do not commit:

```text
LOCALSTACK_AUTH_TOKEN
AWS_ACCESS_KEY_ID from a real AWS account
AWS_SECRET_ACCESS_KEY from a real AWS account
AWS_SESSION_TOKEN
real queue credentials
```

Fake LocalStack values such as:

```text
test/test
```

are not secrets.

---

# 49. README / Development Documentation

Update the main README or create a local-development section.

Minimum developer instructions:

```text
Prerequisite: Docker Desktop

1. docker compose up -d
2. verify postgres/localstack healthy
3. run SecurityOperationsService
4. run WorkOrderService
5. run frontend
6. create Critical asset incident
7. verify WorkOrder appears
```

Also document:

```text
LocalStack endpoint: localhost:4566
primary queue name
DLQ name
```

---

# 50. Optional Make/Script Convenience

If the repository already uses scripts, Kiro may add a convenience command such as:

```text
scripts/local-infra-up.sh
```

but this is optional.

Do not add a build system solely to wrap:

```bash
docker compose up -d
```

---

# 51. Integration Test Strategy

Core business handler tests should not require LocalStack.

Separate:

```text
consumer application/idempotency tests
```

from:

```text
SQS infrastructure integration tests
```

LocalStack is appropriate for tests of:

```text
SendMessage
ReceiveMessage
DeleteMessage
visibility/redelivery
DLQ/redrive
message attributes
queue configuration
```

---

# 52. CI Future Path

LocalStack may later run in GitHub Actions using Docker.

Do not require a real AWS account for ordinary CI integration testing.

A later deployed smoke test should still exercise real Amazon SQS.

---

# 53. Do Not Add LocalStack To Production

LocalStack configuration must remain local/CI infrastructure.

Production deployment must use:

```text
Amazon SQS
```

not a LocalStack container deployed to Azure.

---

# 54. Package / Architecture Constraints

Do not add:

```text
MassTransit
NServiceBus
CAP
Rebus
RabbitMQ
Kafka
```

for this MVP.

The point is to demonstrate:

```text
direct AWS SQS integration
transactional outbox
at-least-once delivery
idempotent consumer
DLQ handling
```

---

# 55. Required Files — Expected Changes

Kiro should expect to touch/create approximately:

```text
Directory.Packages.props

docker-compose.yml

deploy/localstack/init/
    01-create-sqs.sh

src/SecurityOperationsService/
    Vision.SecurityOperationsService.csproj
    appsettings.Development.json
    API/Program.cs
    Infrastructure/Messaging/...

src/WorkOrderService/
    Vision.WorkOrderService.csproj
    appsettings.Development.json
    API/Program.cs
    Infrastructure/Messaging/...

README.md
or
docs/development/localstack-sqs-setup.md
```

Exact messaging filenames follow the Phase 4 implementation structure.

---

# 56. LocalStack-Specific Code Should Be Minimal

The expected LocalStack-specific application code should be essentially limited to:

```text
endpoint override
fake local credentials when endpoint override is present
```

Everything else should remain ordinary AWS SQS logic.

---

# 57. Acceptance Criteria — Infrastructure

```text
[ ] docker compose starts PostgreSQL and LocalStack
[ ] LocalStack becomes healthy
[ ] primary SQS queue auto-created
[ ] DLQ auto-created
[ ] primary queue is Standard
[ ] visibility timeout = 60
[ ] long poll wait = 20
[ ] maxReceiveCount = 5
[ ] DLQ retention = 14 days
[ ] no manual queue setup required
```

---

# 58. Acceptance Criteria — Configuration

```text
[ ] Development ServiceUrl points to localhost:4566
[ ] production does not contain LocalStack ServiceUrl
[ ] region configurable
[ ] queue name configurable
[ ] no real AWS secrets committed
[ ] application does not create queues
```

---

# 59. Acceptance Criteria — Producer

```text
[ ] qualifying Critical asset incident creates outbox row
[ ] outbox publisher uses IAmazonSQS
[ ] LocalStack receives event
[ ] successful send marks PublishedAt
[ ] failed send leaves row unpublished
[ ] same EventId preserved on retry
```

---

# 60. Acceptance Criteria — Consumer

```text
[ ] WorkOrderService receives from LocalStack
[ ] valid v1 event creates one WorkOrder
[ ] WorkOrder commit occurs before DeleteMessage
[ ] same EventId delivered twice creates one WorkOrder
[ ] same IncidentId with different EventIds creates one WorkOrder
[ ] transient failure does not delete message
[ ] invalid poison message does not create WorkOrder
```

---

# 61. Acceptance Criteria — DLQ

```text
[ ] permanently invalid message is repeatedly redelivered
[ ] consumer does not manually delete it as success
[ ] after configured receive threshold it reaches DLQ
[ ] original message body can be inspected
[ ] duplicate/idempotent success does not reach DLQ
```

---

# 62. Acceptance Criteria — Correlation

```text
[ ] incident request has CorrelationId
[ ] outbox stores CorrelationId
[ ] SQS event carries same CorrelationId
[ ] WorkOrder stores same CorrelationId
[ ] SourceEventId equals event EventId
[ ] SQS MessageId is not used as EventId
```

---

# 63. End-to-End Demo Acceptance

With:

```bash
docker compose up -d
```

and the .NET services running locally:

1. open Vision,
2. create a Critical incident for the Pharmacy Storage camera,
3. incident persists,
4. outbox publisher sends to LocalStack SQS,
5. WorkOrderService consumes message,
6. a new Critical WorkOrder appears,
7. logs show the same correlation context,
8. no AWS cloud account is required for this local execution.

---

# 64. Failure Reporting

If LocalStack is unavailable, the developer should see useful errors such as:

```text
Unable to publish IncidentCreated event
SQS endpoint http://localhost:4566 unavailable
EventId ...
CorrelationId ...
```

Do not swallow failures.

Do not print AWS credentials.

---

# 65. Scope Guard

Do not expand this task into:

```text
LocalStack persistence architecture
Terraform-on-LocalStack
LocalStack Web UI
Cloud Pods
IAM enforcement emulation
SNS
Lambda
EventBridge
S3
DynamoDB
```

unless a later Vision requirement explicitly needs them.

The goal is simply:

> **Run and test the approved SQS workflow locally with production-shaped AWS SDK code.**

---

# 66. Kiro Completion Report

When finished, Kiro should report:

```text
files changed
LocalStack version selected
AWS SDK package version selected
queue names
how ServiceUrl is configured
commands used to verify queues
result of local end-to-end Incident -> SQS -> WorkOrder test
result of duplicate test
result of poison/DLQ test if performed
build/test results
```

If any contract above could not be implemented, Kiro should explain the exact limitation instead of silently changing the behavior.

---

# 67. Governing Principle

> **Local development should emulate Amazon SQS closely enough to exercise Vision's real messaging design, while production continues to use the same AWS SDK code against actual AWS infrastructure.**

The LocalStack implementation is successful when the application does not need to know whether `IAmazonSQS` points at:

```text
localhost:4566
```

or:

```text
Amazon SQS
```

beyond environment/configuration-level client setup.
