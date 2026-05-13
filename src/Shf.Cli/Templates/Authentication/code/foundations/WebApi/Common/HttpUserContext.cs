using System.Security.Claims;
using Business.Common;

namespace WebApi.Common;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid? UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
