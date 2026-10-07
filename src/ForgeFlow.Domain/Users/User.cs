using ForgeFlow.Domain.Common;

namespace ForgeFlow.Domain.Users;

public enum UserRole
{
    Viewer,
    Engineer,
    Approver,
    Admin
}

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
}
