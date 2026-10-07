namespace ForgeFlow.Application.Common.Security;

/// <summary>JWT claim names (inbound claim mapping is disabled, so these stay short).</summary>
public static class ForgeFlowClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}

public static class Policies
{
    public const string CanEdit = nameof(CanEdit);
    public const string CanApprove = nameof(CanApprove);
    public const string CanViewAudit = nameof(CanViewAudit);
    public const string AdminOnly = nameof(AdminOnly);
}
