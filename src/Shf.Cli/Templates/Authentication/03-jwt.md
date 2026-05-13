---
slug: jwt
title: "feat: JWT login + password authentication + permission policy gating"
labels: enhancement,version:minor
depends_on: identity
---

## What

JWT bearer authentication on top of {{slug:identity}}, plus the `[HasPermission]` policy machinery that gates every admin endpoint. Mirrors `sh-framework-template` **v3.9.0**.

### JWT issuer (`src/Business/Authentication/Jwt/`)
HMAC-SHA256. Claims: `sub` / `tid` / `email` / `preferred_username` / `name` / `jti` (+ `sid` when refresh/sessions land via {{slug:refresh}}). **No `permissions` claim** — authz resolves against the DB at request time so role changes apply without waiting for token expiry. Signing key from `Authentication:Jwt:SigningKey` (user-secrets / env). Hard-fail if < 32 UTF-8 bytes.

### Login slice (`POST /api/v1/auth/login`)
Body: `{ tenantId | tenantSlug, identifier (email OR username), password }`. Status checks (Disabled / Locked / PendingVerification) run **before** password compare so distinct codes surface. Wrong-password attempts increment `User.FailedLoginAttempts`; status flips to `Locked` at `Authentication:Login:MaxFailedAttempts` (default 5). Success resets the counter + stamps `LastLoginAt`.

### Permission policy machinery (`src/Business/Authentication/Authorization/`)
- `HasPermissionAttribute` — encodes policy as `perm:<name>`.
- `PermissionPolicyProvider` — dynamic, builds policies on demand. **Accepts JwtBearer scheme** (and ApiKey later via {{slug:apikey}}).
- `PermissionAuthorizationHandler` — walks user → roles → permissions, plus catalog-existence check.
- `.RequirePermission("orders.read")` minimal-API extension.

### Apply gating
Every admin endpoint from {{slug:tenant}} / {{slug:identity}} gets `.RequirePermission("admin.*")`. The `TODO(perms)` markers go away.

### User entity additions
`FailedLoginAttempts` (int), `LastFailedLoginAt` (DateTime?), `LastLoginAt` (DateTime?).

## Configuration
`appsettings.json` → `Authentication.Jwt.{Issuer,Audience,AccessTokenLifetime,ClockSkew}` + `Authentication.Login.MaxFailedAttempts`. **Signing key in user-secrets only.**

## Reference

Port from [`sh-framework-template` v3.9.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.9.0):
- `Business/Authentication/Jwt/*.cs`
- `Business/Authentication/Authorization/*.cs`
- `Business/Authentication/{AuthResultCode,LoginOptions,RegisterAuthentication}.cs`
- `Business/Features/Auth/Login/*.cs`
- `WebApi/Endpoints/Auth/AuthEndpoints.cs` (login + logout placeholder)
- `Program.cs` — `AddAuthentication` + `AddJwtBearer` + `AddSHAuthentication` + `UseAuthentication`/`UseAuthorization`

## Acceptance
- [ ] JWT issuer with all required claims; signing-key guard tested.
- [ ] Lockout escalation flips Status to `Locked` at `MaxFailedAttempts`.
- [ ] Status checks order is Disabled → Locked → PendingVerification → password.
- [ ] All admin endpoints from {{slug:tenant}} + {{slug:identity}} gated by `.RequirePermission(...)`.
- [ ] `docs/SECRETS.md` lists `Authentication:Jwt:SigningKey`.
