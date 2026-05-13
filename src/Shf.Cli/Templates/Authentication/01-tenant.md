---
slug: tenant
title: "feat: Tenant entity + ITenantContext resolution"
labels: enhancement,version:minor
depends_on: foundations
---

## What

Multi-tenancy backbone. Soft tenancy — every owned entity carries `TenantId`. Mirrors `sh-framework-template` **v3.7.0**.

### Entity (`src/Domain/Entities/Identity/`)
- `TenantStatus` enum (Active=1, Suspended=2)
- `Tenant` — implements `IPrimaryKey<Guid>`, `IHasCreatedColumns`, `IHasAuditColumns`, `ISoftDeletable`, `IHasStatus<TenantStatus>`. **Does NOT implement `IHasTenant`** — it IS the tenant.

### Store (`src/Business/Identity/`)
- `ITenantStore` + `InMemoryTenantStore` — `TryAddSingleton`; persistence-backed impls override.

### Resolution chain (already shipped in {{slug:foundations}})
1. JWT `tid` claim (primary, post-auth)
2. `X-Tenant-Id` header (fallback for pre-auth flows: login/register/SSO callback)

### Admin CRUD (`/api/v1/admin/tenants/*`)
5 endpoints: List (status filter), Get, Create (slug validation `^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$` + uniqueness), Update (PATCH semantics), Delete (soft).

Tagged `TODO(perms): admin.tenants.*` until {{slug:identity}} ships the permission catalog.

## Reference

Port from [`sh-framework-template` v3.7.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.7.0):
- `src/Domain/Entities/Identity/Tenant.cs`, `TenantStatus.cs`
- `src/Business/Identity/{ITenantStore,InMemoryTenantStore,IdentityResultCode,RegisterIdentity}.cs`
- `src/Business/Features/Admin/Tenants/**`
- `src/WebApi/Endpoints/Admin/Tenants/*.cs`

## Acceptance
- [ ] Tenant entity + status enum.
- [ ] In-memory store with case-insensitive slug lookup, tenant-scoped reads, soft delete.
- [ ] All 5 admin endpoints with consistent OpenAPI metadata (`.Produces<Result<T>>`).
- [ ] Slug validation theory test covers too-short / leading-hyphen / trailing-hyphen / underscore / space.
