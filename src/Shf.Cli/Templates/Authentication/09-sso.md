---
slug: sso
title: "feat: SSO providers — admin CRUD + dynamic OIDC handler"
labels: enhancement,version:minor
depends_on: refresh
---

## What

Per-tenant SSO providers with admin CRUD + a public OIDC authorization-code flow (PKCE + state + nonce + JWKS signature verification + user auto-provisioning). Mirrors `sh-framework-template` **v3.15.0**.

### Entities
- `SsoProvider` — per-tenant. Endpoints (AuthorizationEndpoint, TokenEndpoint, UserInfoEndpoint?, JwksUri, Issuer), ClientId, encrypted ClientSecretCipher, Scopes, ClaimMappingJson, Status. Soft-deletable.
- `UserSsoIdentity` — join row (TenantId, UserId, SsoProviderId, ExternalSubject). Matches by `(ProviderId, sub)` so IdP email changes don't break the link.

### OIDC handshake
1. `GET /api/v1/auth/sso/{name}/start[?returnUrl=]` — validates returnUrl against `AllowedReturnUrls` (open-redirect defence). Generates state + nonce + PKCE verifier (S256). Sets HttpOnly state cookie. 302-redirects to provider's `authorization_endpoint` with `code_challenge=S256`.
2. IdP redirects back with `code` + `state`.
3. `GET /api/v1/auth/sso/{name}/callback` — decrypts state cookie, validates state matches, exchanges code at TokenEndpoint with PKCE verifier, fetches JWKS, validates id_token signature + iss + aud + exp + nonce. Maps claims via `ClaimMappingJson` (defaults: sub / email / preferred_username / name). Matches existing `UserSsoIdentity` by `(ProviderId, sub)` OR by email. Auto-creates User when `SsoOptions.AutoCreateUser` is true. Mints Session (AuthMethod=Sso) + RefreshToken + JWT.

### Admin
`/api/v1/admin/sso-providers/*` — gated by `admin.sso-providers.read|write`. ClientSecret never returned (DTOs always show `****`). Set `ClientSecret` in PATCH to rotate.

### Public list
`GET /api/v1/auth/sso/providers` — returns `{ id, name, displayName }` for active providers; frontend renders login buttons.

### Options (`Authentication:Sso`)
`AutoCreateUser` (true), `AutoVerifyEmail` (true), `DefaultRoleNames` (csv), `AllowedReturnUrls` (csv prefix allowlist), `StateCookieName` / `StateCookieTtl`, `ClockSkew`.

### Codes
`SsoResultCode` 4400-4408: ProviderNotFound / NameAlreadyExists / Disabled / InvalidStateCookie / CodeExchangeFailed / IdTokenInvalid / UserProvisioningRefused / ReturnUrlNotAllowed / JwksFetchFailed.

## Reference

Port from [`sh-framework-template` v3.15.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.15.0):
- `Domain/Entities/Identity/{SsoProvider,SsoProtocol,SsoProviderStatus,UserSsoIdentity}.cs`
- `Business/Authentication/Sso/**` (stores, Pkce, SsoStateCookie, OidcTokenExchange, SsoOptions, SsoResultCode)
- `Business/Features/Admin/SsoProviders/**`
- `Business/Features/Auth/{SsoStart,SsoCallback,SsoList}/*.cs`
- `WebApi/Endpoints/Admin/SsoProviders/AdminSsoProviderEndpoints.cs`
- `WebApi/Endpoints/Auth/SsoEndpoints.cs`
- `Program.cs` — `AddHttpClient("Sso")`

## Deliberately out of scope for v1
- OIDC **discovery doc auto-populate** (admin sets endpoints explicitly).
- OAuth 2.0 **non-OIDC** providers.
- **Existing-user-links-new-identity** flow (today auto-link by email + auto-provision are the only paths).
- Admin `/test` smoke endpoint.

These are followups when the basic OIDC flow is stable.

## Acceptance
- [ ] PKCE S256 challenge stable and url-safe.
- [ ] Admin CRUD: encrypt on create, masked DTO, dup-name rejection, rotate-secret on PATCH, soft delete.
- [ ] State cookie HttpOnly + Secure + SameSite=Lax, path-scoped.
- [ ] id_token validation: signature against JWKS, ValidIssuer, ValidAudience, ValidLifetime, nonce match.
- [ ] User provisioning: existing user matched by email gets linked; new user auto-created when allowed.
- [ ] (manual) End-to-end handshake against Google/Auth0 sandbox.
