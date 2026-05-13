---
slug: identity
title: "feat: User + Role + Permission + DB-persisted catalog"
labels: enhancement,version:minor
depends_on: foundations
---

## What

Identity backbone — User + Role + Permission + join tables + argon2id password hashing + admin CRUD. Mirrors `sh-framework-template` **v3.8.0**.

### Entities (`src/Domain/Entities/Identity/`)
- `User` — `Email` + `Username` (both unique per tenant) + optional `Phone`, `PasswordHash?` (nullable for SSO-only users), `DisplayName`, `EmailVerifiedAt?` / `PhoneVerifiedAt?`, `Status` (PendingVerification/Active/Disabled/Locked).
- `Verification` — in-flight email/phone challenges (UserId + Channel + CodeHash + ExpiresAt + ConsumedAt + Status).
- `Role` — `Name` (unique per tenant), `Description?`, `IsSystem` (system roles can't be deleted/renamed).
- `Permission` — **global** (no `IHasTenant`). Dotted-lowercase `Name` (`admin.users.write`, `orders.read`), `Category` (first segment, denormalised).
- `UserRole`, `RolePermission` — join tables (carry `TenantId` for query efficiency).

### Password hashing
`Konscious.Security.Cryptography.Argon2` 1.3.1. Hash format embeds parameters (`$argon2id$v=19$m=65536,t=3,p=4$salt$hash`). `Verify` constant-time via `CryptographicOperations.FixedTimeEquals`.

### Stores (`src/Business/Identity/`)
`IUserStore` / `IRoleStore` / `IPermissionStore` / `IVerificationStore` with in-memory defaults. `IdentityResultCode` codes 3000-3399 across Tenant/User/Role/Permission domains. `PermissionSeeder` (IHostedService) seeds 9 standard `admin.*` permissions on startup.

### Admin CRUD endpoints (16 routes)
- Users: List/Get/Create/Update/Delete/SetRoles
- Roles: List/Get/Create/Update/Delete/SetPermissions
- Permissions: List/Get/Create/Delete

## Reference

Port from [`sh-framework-template` v3.8.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.8.0):
- 8 entity files + 3 status enums under `Domain/Entities/Identity/`
- `Business/Identity/{IUserStore,InMemoryUserStore,IRoleStore,InMemoryRoleStore,IPermissionStore,InMemoryPermissionStore,IVerificationStore,InMemoryVerificationStore,Argon2idPasswordHasher,IPasswordHasher,PermissionSeeder}.cs`
- `Business/Features/Admin/{Users,Roles,Permissions}/**`
- `WebApi/Endpoints/Admin/{Users,Roles,Permissions}/*.cs`

## Out of scope here

Permission **gating** (`[HasPermission]` attribute + policy provider + handler) lands with {{slug:jwt}} — it requires an authenticated `ClaimsPrincipal` which JWT login provides.

## Acceptance
- [ ] All entities + enums + stores in place.
- [ ] Password hashing round-trip + tamper-resistance tested.
- [ ] PermissionSeeder is idempotent across multiple startups.
- [ ] Admin CRUD endpoints tagged `TODO(perms)` until {{slug:jwt}}.
