using System.Security.Claims;

namespace Tds.Api.Infrastructure;

public interface ICurrentUser
{
    /// <summary>null = no signed-in user (system action).</summary>
    Guid? UserId { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
