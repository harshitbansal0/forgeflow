using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<PagedResult<UserDto>> List([FromQuery] UserQuery query, CancellationToken cancellationToken) =>
        userService.ListAsync(query, cancellationToken);

    /// <summary>Active users for owner pickers and filters; available to every signed-in user.</summary>
    [HttpGet("directory")]
    public Task<IReadOnlyList<UserDirectoryEntry>> Directory(CancellationToken cancellationToken) =>
        userService.GetDirectoryAsync(cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), null, user);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<UserDto> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        userService.UpdateAsync(id, request, cancellationToken);
}
