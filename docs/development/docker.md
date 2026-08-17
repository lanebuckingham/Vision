# Vision — Docker & Docker Compose (Phase 6)

This document explains how to run the full Vision stack locally with Docker
Compose: prerequisites, the commands you'll actually use day to day, what
each service exposes, and how environment/Cognito configuration works when
everything runs in containers instead of on the host.

For the LocalStack/SQS init-script details (queue names, DLQ, redrive
policy), see [`localstack-sqs-setup.md`](./localstack-sqs-setup.md). For
tracing/health-endpoint/logging details, see
[`observability.md`](./observability.md). For Cognito user pool setup, see
[`cognito-setup.md`](./cognito-setup.md).

## Prerequisites

- Docker Desktop (macOS/Windows) or Docker Engine + the Compose plugin
  (Linux), providing `docker` and `docker compose` (v2 syntax — not the
  standalone `docker-compose` v1 binary).
- No local .NET SDK, Node.js, or PostgreSQL installation is required to run
  the containerized stack. They are still required if you want to run
  services on the host instead (see `localstack-sqs-setup.md` §27 for the
  host/container networking distinction).

## What's in the stack

`docker-compose.yml` (repository root) defines six services:

| Service | Container port | Host port | Purpose |
|---|---|---|---|
| `postgres` | 5432 | 5432 | Single PostgreSQL 17 instance, one logical database (`vision`) shared by all three backend services with separate connection strings |
| `localstack` | 4566 | 4566 | Emulates Amazon SQS for the `IncidentCreated.v1` workflow |
| `security-operations-service` | 8080 | 5163 | Dashboard, assets, incidents |
| `work-order-service` | 8080 | 5250 | Work orders, technicians |
| `credential-service` | 8080 | 5223 | People, credentials |
| `frontend` | 3000 | 3000 | Next.js UI (standalone build) |

The three backend application images use the repository root build context
(`docker-compose.yml` `build:` blocks), since the backend Dockerfiles need
`Directory.Build.props` / `Directory.Packages.props` from the repo root. The
frontend build context is scoped to `src/frontend`.

## Everyday commands

Start the full stack, building images as needed:

```bash
docker compose up --build
```

Run detached (background) instead:

```bash
docker compose up -d --build
```

Check status:

```bash
docker compose ps
```

Stop containers without deleting data (Postgres volume is preserved; next
`up` restarts from the same seeded data plus anything you created):

```bash
docker compose stop
# or, to also remove the stopped containers (volume still preserved):
docker compose down
```

Full reset — stop everything **and delete the Postgres volume**, so the next
`up` re-seeds from scratch:

```bash
docker compose down -v
```

Rebuild images from scratch (e.g. after a dependency bump), ignoring the
Docker layer cache:

```bash
docker compose build --no-cache
```

## Infrastructure-only workflow

If you want to run the three backend services and/or the frontend directly
on the host (e.g. for faster debug-cycle iteration in an IDE) while still
using containerized Postgres and LocalStack, start only those two services
by name:

```bash
docker compose up postgres localstack
```

This is the same workflow `localstack-sqs-setup.md` describes; nothing about
it changed with the addition of the four application services. When running
services on the host this way, use `localhost` endpoints
(`Host=localhost;...`, `http://localhost:4566`) as already documented in
each service's `appsettings.Development.json`, not the in-container service
DNS names (`postgres`, `localstack`) used in `docker-compose.yml`.

## Verifying things are healthy

```bash
docker compose ps
```

All six services should show a `healthy` (or `running`, for `frontend`,
which has no healthcheck configured) status within roughly a minute of a
fresh `up --build`. `security-operations-service`, `work-order-service`, and
`credential-service` wait on `postgres` and (where relevant) `localstack`
reaching `service_healthy` before they even start.

Backend health endpoints (see `observability.md` for what each one checks):

```bash
curl http://localhost:5163/health/live
curl http://localhost:5163/health/ready
curl http://localhost:5250/health/ready
curl http://localhost:5223/health/ready
```

Frontend:

```bash
curl -I http://localhost:3000
```

LocalStack queues (created automatically by the `deploy/localstack/init`
ready-hook script — no manual setup required):

```bash
docker exec vision-localstack awslocal sqs list-queues --region us-east-1
```

Expect `vision-dev-incident-created` and `vision-dev-incident-created-dlq`.

## Logs

```bash
docker compose logs -f security-operations-service
docker compose logs -f work-order-service
docker compose logs -f credential-service
docker compose logs -f frontend
```

## Environment variables and `.env`

Copy `.env.example` to `.env` (git-ignored) at the repository root if you
want to exercise real Cognito login or export telemetry to a real OTLP
collector against the containerized stack. `docker compose` automatically
reads a root-level `.env` file.

Nothing in `.env.example` is required for `docker compose up --build` to
work:

- **Cognito unset** — all three backends stay in their existing fail-closed
  behavior: every protected endpoint returns `401`. This is the same
  behavior as running the services on the host without Cognito configured
  (see `cognito-setup.md`) — Docker does not introduce a bypass.
- **OTLP endpoint unset** — the OpenTelemetry OTLP exporter is registered
  but has nothing to export to; it fails safely in the background per
  `observability.md` and never blocks startup or requests.
- **`LOCALSTACK_AUTH_TOKEN` unset** — only needed for LocalStack Pro; the
  free/community image used here (`localstack/localstack:4.4`) doesn't
  require it for SQS.

Two things to know about how Cognito values reach each part of the stack:

1. **Backend** (`Cognito__UserPoolId`, `Cognito__Region`, `Cognito__ClientId`)
   are ordinary container environment variables — set them in `.env` and
   `docker compose up` (no rebuild needed) picks them up on the next
   container start, same as any ASP.NET Core configuration value.
2. **Frontend** (`NEXT_PUBLIC_COGNITO_*`) are Next.js build-time public
   env vars. They get inlined into the browser JavaScript bundle when the
   image is *built*, not read at container start. If you change them in
   `.env`, you must rebuild the frontend image:
   ```bash
   docker compose build frontend
   docker compose up -d frontend
   ```

None of the `COGNITO_*` / `NEXT_PUBLIC_COGNITO_*` values are secrets — a
user pool ID, region, and public app-client ID are not sensitive. Vision's
browser Cognito app client is a public OAuth client (Authorization Code +
PKCE) and must never be issued a client secret, so there is no Cognito
secret value to put here in the first place.

## Frontend API URLs are host-reachable, not service DNS

The frontend's `NEXT_PUBLIC_API_URL` / `NEXT_PUBLIC_WORK_ORDER_API_URL` /
`NEXT_PUBLIC_CREDENTIAL_API_URL` build args in `docker-compose.yml` point at
`http://localhost:5163` / `:5250` / `:5223`. This is intentional and not a
mistake: these values are baked into JavaScript that runs in your **browser**,
which is not part of the Docker network and cannot resolve container service
names like `security-operations-service`. The browser always talks to the
published host ports. Backend-to-backend calls, by contrast, are not
currently part of any workflow — the only cross-service integration is the
asynchronous SQS path, which uses the `localstack` service DNS name from
inside the containers.

## Demo path in containers

With the full stack up, the same end-to-end flow documented in
`localstack-sqs-setup.md` §63 works entirely in containers: create a
Critical incident through `security-operations-service`, watch the outbox
publisher send `IncidentCreated.v1` to the containerized LocalStack, and
watch `work-order-service` consume it and create a `WorkOrder`. Container
logs show the same `CorrelationId` end to end (`docker compose logs -f
security-operations-service work-order-service`).

## Known limitations

- The `frontend` service has no Compose healthcheck (Next.js's standalone
  `server.js` has no lightweight built-in readiness endpoint); Compose shows
  it as `running`, not `healthy`. `depends_on: condition: service_started`
  is used for the frontend's dependency on the three backends accordingly.
- `credential-service` does not depend on `localstack` (it has no messaging
  integration per the architecture's service boundaries), so it only waits
  on `postgres`.
- Rebuilding the frontend image is required after changing any
  `NEXT_PUBLIC_*` value, even though the backend services pick up their
  configuration changes on a plain container restart.
