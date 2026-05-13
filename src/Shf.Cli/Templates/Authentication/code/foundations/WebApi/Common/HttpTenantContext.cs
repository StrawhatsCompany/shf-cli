using Business.Common;

namespace WebApi.Common;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private const string TenantClaim = "tid";
    private const string TenantHeader = "X-Tenant-Id";

    public Guid? TenantId
    {
        get
        {
            var http = accessor.HttpContext;
            if (http is null) return null;

            var claim = http.User?.FindFirst(TenantClaim)?.Value;
            if (Guid.TryParse(claim, out var fromClaim)) return fromClaim;

            if (http.Request.Headers.TryGetValue(TenantHeader, out var header)
                && Guid.TryParse(header, out var fromHeader)) return fromHeader;

            return null;
        }
    }
}
