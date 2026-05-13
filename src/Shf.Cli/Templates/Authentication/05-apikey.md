---
slug: apikey
title: "feat: API key generation + ApiKey authentication scheme"
labels: enhancement,version:minor
depends_on: identity
---

## What

Programmatic access via API keys, parallel to JWT bearer. Mirrors `sh-framework-template` **v3.11.0**.

### Entity
`ApiKey` — `UserId`, `Name`, `Prefix` (8 chars, indexed), `Last4`, `KeyHash` (SHA-256 of full token), `ExpiresAt?`, `LastUsedAt?`, `LastUsedIp?`, `Status`.

### Token format
`shf_<8-char-prefix>_<32-char-secret>` (base62 alphabet). Plaintext returned **once** at creation; DB only ever has the hash + last4.

### Auth scheme
`ApiKeyAuthenticationHandler` — ASP.NET `AuthenticationHandler<ApiKeyOptions>`. Parses `Authorization: ApiKey <token>`, looks up by prefix (indexed), constant-time SHA-256 compare, status + expiry + owner-active checks, debounced `LastUsedAt`/`LastUsedIp` update.

Multi-scheme: endpoints accept either `Bearer <jwt>` or `ApiKey <token>`. Permission policies are scheme-agnostic — both produce a `sub`+`tid` principal that the handler reads.

### Endpoints
- Self-service: `POST/GET /api/v1/api-keys`, `DELETE /api/v1/api-keys/{id}` (gated by `api-keys.read|write`).
- Admin: `GET /api/v1/admin/api-keys[?userId=]`, `DELETE /api/v1/admin/api-keys/{id}` (`admin.api-keys.read|write`).

### Permission policy update
`PermissionPolicyProvider` from {{slug:jwt}} now builds policies accepting **both** `JwtBearer` + `ApiKey` schemes.

## Reference

Port from [`sh-framework-template` v3.11.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.11.0):
- `Business/Authentication/ApiKeys/*.cs`
- `Business/Features/ApiKeys/{CreateMyApiKey,ListMyApiKeys,RevokeMyApiKey}/*.cs`
- `Business/Features/Admin/ApiKeys/*.cs`
- `WebApi/Endpoints/ApiKeys/*.cs` + `WebApi/Endpoints/Admin/ApiKeys/*.cs`

## Acceptance
- [ ] Plaintext returned ONCE on create; subsequent reads only expose `Prefix` + `Last4`.
- [ ] `TryParse` strict: rejects wrong prefix, wrong segment lengths, non-alnum.
- [ ] Constant-time hash compare.
- [ ] LastUsedAt update is debounced by `ApiKeyOptions.LastUsedUpdateThrottle` (default 60s).
- [ ] PermissionPolicyProvider accepts both Bearer + ApiKey.
