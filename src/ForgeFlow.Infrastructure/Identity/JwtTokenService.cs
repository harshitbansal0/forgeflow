using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ForgeFlow.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key; at least 32 characters (256 bits).</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int ExpiryMinutes { get; set; } = 120;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateToken(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.ExpiryMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                [ForgeFlowClaims.Subject] = user.Id.ToString(CultureInfo.InvariantCulture),
                [ForgeFlowClaims.Email] = user.Email,
                [ForgeFlowClaims.Name] = user.DisplayName,
                [ForgeFlowClaims.Role] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            },
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expires);
    }
}

/// <summary>PBKDF2 hashing from ASP.NET Core Identity, without the rest of the Identity stack.</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly User HashContext = new();
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashContext, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(HashContext, passwordHash, password) != PasswordVerificationResult.Failed;
}
