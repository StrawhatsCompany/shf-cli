---
slug: foundations
title: "feat: entity foundations — interfaces, IUserContext, ITenantContext"
labels: enhancement,version:minor
depends_on:
---

## What

The cross-cutting foundations every auth entity (and every business entity going forward) inherits. Mirrors `StrawhatsCompany/sh-framework-template` **v3.6.0**.

### Interfaces in `src/Domain/Abstractions/`
- `IPrimaryKey<TKey>` — `TKey Id { get; set; }`
- `IHasCreatedColumns` — `CreatedAt`, nullable `UpdatedAt`
- `IHasAuditColumns` — nullable `CreatedBy`/`UpdatedBy`/`DeletedBy` (Guid, nullable for system actions / migrations / seeders)
- `ISoftDeletable` — nullable `DeletedAt`. No automatic query filter — handlers write `.Where(x => x.DeletedAt == null)` explicitly
- `IHasTenant` — `Guid TenantId` *(tenancy: {{tenant-flag}})*
- `IHasStatus<TStatus>` where `TStatus : struct, Enum` — per-entity status enums, no shared lifecycle enum

### Context services in `src/Business/Common/`
- `IUserContext` (Guid? UserId) + `NullUserContext` default
- `ITenantContext` (Guid? TenantId) + `NullTenantContext` default
- Both registered `TryAddScoped` in `AddBusiness`; HTTP-aware implementations in `WebApi/Common/HttpUserContext` (reads `ClaimTypes.NameIdentifier`) and `HttpTenantContext` (reads `tid` claim → falls back to `X-Tenant-Id` header for pre-auth flows).

## Reference

Port from [`sh-framework-template` v3.6.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.6.0):
- `src/Domain/Abstractions/*.cs`
- `src/Business/Common/*.cs`
- `src/WebApi/Common/HttpUserContext.cs` + `HttpTenantContext.cs`
- `Program.cs` — `AddHttpContextAccessor()` + scoped overrides

{{#no-tenant}}
## Tenancy note (DISABLED)

You opted out of multi-tenancy. Keep `IHasTenant` in the abstractions set anyway — it costs nothing to leave it available and lets you opt in later by adding `TenantId` to specific entities without redesigning the layer. Just don't implement it on any of your entities.
{{/no-tenant}}

## Acceptance
- [ ] All six interfaces under `Domain/Abstractions/`.
- [ ] `IUserContext`/`ITenantContext` + null defaults + HTTP impls.
- [ ] `Program.cs` wires `AddHttpContextAccessor()` + scoped `Http*Context` overrides.
- [ ] Unit tests for HTTP context resolution (null context, missing claim, valid claim, header fallback for tenant).