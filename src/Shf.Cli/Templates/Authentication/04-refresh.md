---
slug: refresh
title: "feat: Session + RefreshToken (rotation + family invalidation)"
labels: enhancement,version:minor
depends_on: jwt
---

## What

Long-lived sessions + refresh-token rotation with family invalidation on reuse. Mirrors `sh-framework-template` **v3.10.0**.

### Entities (`src/Domain/Entities/Identity/`)
- `Session` — one per logged-in device. Tracks `UserId`, `AuthMethod` (Password/ApiKey/Sso), `DeviceLabel?`, `IpFirst/IpLast`, `LastSeenAt`, absolute `ExpiresAt`, `RevokedAt?`/`RevokedReason?`, `Status` (Active/Revoked/Expired).
- `RefreshToken` — SHA-256 hashed, linked to a Session. `ReplacedById` forms the rotation chain — that's what lets us detect reuse and invalidate the whole family.

### Stores
- `ISessionStore` + `InMemorySessionStore`
- `IRefreshTokenStore` + `InMemoryRefreshTokenStore` — `RevokeFamilyAsync` walks the chain in BOTH directions
- `IRefreshTokenFactory` — 256-bit cryptographic random plaintext (url-safe base64), SHA-256 hash for storage

### Endpoint changes
- `POST /api/v1/auth/login` (from {{slug:jwt}}) — UPDATED to also return `{ refreshToken, refreshTokenExpiresAt, sessionId }`.
- `POST /api/v1/auth/refresh` — NEW. Rotates the chain on every call. **Replay detection**: presenting an already-consumed token revokes the entire family AND the session (`RevokedReason = "family-invalidation"`). RFC 6749 §10.4 / OAuth 2.0 BCP.
- `POST /api/v1/auth/logout` — UPDATED. Reads `sid` from access token, revokes session + all attached refresh tokens.

### Self-service + admin
- `GET /api/v1/auth/sessions`, `DELETE /api/v1/auth/sessions/{id}`
- `GET /api/v1/admin/users/{userId}/sessions`, `DELETE /{sessionId}`, bulk `DELETE`

Gated by new permissions `admin.users.sessions.read|write`.

### JWT claim
`IJwtTokenIssuer.Issue` accepts optional `sessionId`; emits `sid` claim. Login + Refresh handlers pass it through.

### Options
`Authentication:Jwt:RefreshTokenLifetime` (default 30 days — also the session absolute deadline).

## Reference

Port from [`sh-framework-template` v3.10.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.10.0):
- `Business/Authentication/Sessions/*.cs`
- `Business/Features/Auth/{Refresh,Logout,Sessions}/*.cs`
- `Business/Features/Admin/UserSessions/*.cs`
- `WebApi/Endpoints/Auth/AuthEndpoints.cs` (login/refresh/logout/sessions)
- `WebApi/Endpoints/Admin/UserSessions/*.cs`

## Acceptance
- [ ] Replay detection triggers `RevokeFamilyAsync` on the entire chain AND revokes the session.
- [ ] Family-walk works from the middle of the chain in both directions.
- [ ] Inactive user during refresh revokes the session and returns InvalidCredentials.
- [ ] `sid` claim flows through login → JWT → logout endpoint cleanly.
