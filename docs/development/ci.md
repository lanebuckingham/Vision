# Vision — Continuous Integration

GitHub Actions runs a continuous-integration workflow only. It does not
deploy, publish images, or apply infrastructure.

The workflow file is [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml).

## When it runs

- Pull requests targeting `main`
- Pushes to `main`
- Manual `workflow_dispatch`

The workflow uses `contents: read` permissions and does not require
repository or production secrets.

## Jobs

Three jobs run in parallel on `ubuntu-latest`:

| Job | What it proves |
|---|---|
| **Backend build & tests** | Restore, Release build, and tests against Compose PostgreSQL + LocalStack |
| **Frontend lint, tests & build** | `npm ci`, ESLint, Vitest, and the Next.js production build |
| **Docker image builds** | `docker compose build` for the three backend services and the frontend |

A new commit on the same branch or pull request cancels an in-progress run
for that ref.

## Local equivalents

Backend (from the repository root; tests need published localhost ports):

```bash
docker compose up -d postgres localstack
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Frontend:

```bash
cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

Docker images:

```bash
docker compose build
```

Backend tests talk to Compose-published ports (`localhost:5432` and
`localhost:4566`), the same way a host-run developer workflow does. Wait
until `vision-postgres` and `vision-localstack` are healthy before running
tests. CI starts only those two Compose services and shuts them down with
`docker compose down -v` after the backend job.
