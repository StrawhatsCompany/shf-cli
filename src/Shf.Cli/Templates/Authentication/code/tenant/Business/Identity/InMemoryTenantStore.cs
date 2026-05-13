using System.Collections.Concurrent;
using Domain.Entities.Identity;

namespace Business.Identity;

internal sealed class InMemoryTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<Guid, Tenant> _byId = new();

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tenant> snapshot = _byId.Values
            .Where(t => t.DeletedAt is null)
            .ToList();
        return Task.FromResult(snapshot);
    }

    public Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        _byId.TryGetValue(id, out var tenant);
        return Task.FromResult(tenant is { DeletedAt: null } ? tenant : null);
    }

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        var match = _byId.Values.FirstOrDefault(t =>
            t.DeletedAt is null && string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<Tenant> AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        _byId[tenant.Id] = tenant;
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (!_byId.ContainsKey(tenant.Id) || _byId[tenant.Id].DeletedAt is not null)
        {
            return Task.FromResult<Tenant?>(null);
        }
        _byId[tenant.Id] = tenant;
        return Task.FromResult<Tenant?>(tenant);
    }

    public Task<bool> SoftDeleteAsync(Guid id, Guid? deletedBy, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var tenant) || tenant.DeletedAt is not null)
        {
            return Task.FromResult(false);
        }
        tenant.DeletedAt = DateTime.UtcNow;
        tenant.DeletedBy = deletedBy;
        return Task.FromResult(true);
    }
}
