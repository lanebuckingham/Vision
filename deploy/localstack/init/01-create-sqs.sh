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
      VisibilityTimeout=60,ReceiveMessageWaitTimeSeconds=20 \
    --region "${REGION}" \
    --query 'QueueUrl' \
    --output text
)"

# The AWS CLI's default --attributes shorthand ("Key=Value,Key2=Value2")
# cannot represent RedrivePolicy, whose value is itself a JSON document
# containing commas and quotes. Pass --attributes as a JSON document instead,
# with the inner RedrivePolicy JSON escaped as a string value.
REDRIVE_POLICY_ESCAPED="$(
  printf '{\"deadLetterTargetArn\":\"%s\",\"maxReceiveCount\":\"5\"}' "${DLQ_ARN}" \
    | sed 's/"/\\"/g'
)"

awslocal sqs set-queue-attributes \
  --queue-url "${QUEUE_URL}" \
  --attributes "{\"RedrivePolicy\":\"${REDRIVE_POLICY_ESCAPED}\"}" \
  --region "${REGION}"

echo "Vision SQS primary queue: ${QUEUE_URL}"
echo "Vision SQS DLQ:           ${DLQ_URL}"
echo "Vision LocalStack SQS initialization complete."
