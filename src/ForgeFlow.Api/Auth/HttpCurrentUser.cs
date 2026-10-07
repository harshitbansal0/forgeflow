using System.Security.Claims;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public int? UserId => int.TryParse(Principal?.FindFirstValue(ForgeFlowClaims.Subject), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ForgeFlowClaims.Email);

    public string? DisplayName => Principal?.FindFirstValue(ForgeFlowClaims.Name);

    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ForgeFlowClaims.Role), out var role) ? role : null;
}
