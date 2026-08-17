# Vision — Amazon Cognito Development Setup

## Overview

Vision requires an Amazon Cognito User Pool for authentication. This document explains how to configure a development Cognito environment.

## User Pool Configuration

Create a Cognito User Pool with:

- **Region:** your preferred region (e.g., `us-east-1`)
- **App client:** public client (no client secret) with Authorization Code + PKCE
- **Callback URL:** `http://localhost:3000/auth/callback`
- **Logout URL:** `http://localhost:3000`
- **OAuth scopes:** `openid`
- **Hosted UI:** enabled (provides `/oauth2/authorize` and `/oauth2/token` endpoints)

## Required Cognito Groups

Create three groups in the User Pool:

| Group Name | Description |
|---|---|
| `SecurityManager` | Security operations management, work order supervision, credential administration |
| `Technician` | Assigned work order repair actions |
| `CredentialAdministrator` | Credential management only |

## Demo Users

Create at least three users and assign them to groups:

| Username | Group | Purpose |
|---|---|---|
| `security-manager` | SecurityManager | Primary demo user |
| `technician-marcus` | Technician | Technician demo (map to Marcus Johnson) |
| `credential-admin` | CredentialAdministrator | Credential admin demo |

## Technician CognitoSubject Mapping

The WorkOrderService maps a Cognito user's `sub` claim to `Technician.CognitoSubject`.

After creating Cognito Technician users, update the seeded Technician records to match. **Use SQL — this is the reliable path:**

```sql
UPDATE work_orders.technicians
SET cognito_subject = '<cognito-user-sub-value>'
WHERE id = 'a1a2b3c4-1001-4eee-a101-100000000001'; -- Marcus Johnson
```

Editing the `CognitoSubject` constants in `SeedDataIds.cs` only affects **empty** databases.
`WorkOrderSeeder.SeedAsync` returns immediately when any Technician row already exists, so
changing the constants will not update an already-seeded database. On an existing database
either run the SQL above or drop the `work_orders` schema and let the service re-seed on the
next start.

The seeded placeholder values are:

| Technician | Placeholder CognitoSubject |
|---|---|
| Marcus Johnson | `cognito-tech-marcus-johnson` |
| Sarah Chen | `cognito-tech-sarah-chen` |
| David Park | `cognito-tech-david-park` |
| Lisa Reeves | `cognito-tech-lisa-reeves` |

Replace these with actual Cognito `sub` values from your user pool.

## Backend Configuration

Add to each service's `appsettings.Development.json`:

```json
{
  "Cognito": {
    "UserPoolId": "us-east-1_XXXXXXXXX",
    "Region": "us-east-1",
    "ClientId": "your-app-client-id"
  }
}
```

## Frontend Configuration

Set environment variables (or `.env.local`):

```env
NEXT_PUBLIC_COGNITO_DOMAIN=https://your-domain.auth.us-east-1.amazoncognito.com
NEXT_PUBLIC_COGNITO_CLIENT_ID=your-app-client-id
NEXT_PUBLIC_COGNITO_REDIRECT_URI=http://localhost:3000/auth/callback
NEXT_PUBLIC_COGNITO_LOGOUT_URI=http://localhost:3000
```

## Without Cognito Configuration

If Cognito environment variables are not set:

- **Backend:** All protected API endpoints return 401 (fail closed)
- **Frontend:** Shows "Sign In" prompt but cannot authenticate

This is intentional. No automatic privileged bypass exists.
