using System.ComponentModel.DataAnnotations;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Application.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record UserInfo(int Id, string Email, string DisplayName, UserRole Role);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserInfo User);
