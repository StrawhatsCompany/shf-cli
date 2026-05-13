using Domain.Entities.Identity;

namespace Business.Identity;

public interface ITenantStore
{
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default);
    Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant> AddAsync(Tenant tenant, CancellationToken ct = default);
    Task<Tenant?> UpdateAsync(Tenant tenant, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid? deletedBy, CancellationToken ct = default);
}
