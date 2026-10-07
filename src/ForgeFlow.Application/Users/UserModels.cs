using System.ComponentModel.DataAnnotations;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Application.Users;

public sealed class UserQuery : PagedRequest
{
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record UserDto(
    int Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc);

public sealed record UserDirectoryEntry(int Id, string DisplayName, UserRole Role);

public sealed class CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; } = UserRole.Viewer;

    [Required, StringLength(128, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")]
    public string Password { get; init; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; }

    public bool IsActive { get; init; } = true;
}
