using Microsoft.AspNetCore.Authorization;

namespace FundManager.BuildingBlocks.Auth;

/// <summary>
/// Authorization policy definitions for fund-level RBAC.
/// Spec Section 4 Role Matrix:
/// - PlatformAdmin: Can create/manage all funds
/// - FundAdmin: Full control within assigned funds
/// - FundEditor: Record payments, request loans within assigned funds
/// - FundGuest: Read-only access to assigned funds
/// </summary>
public static class FundAuthorizationPolicies
{
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string FundAdmin = nameof(FundAdmin);
    public const string FundEditor = nameof(FundEditor);
    public const string FundGuest = nameof(FundGuest);

    /// <summary>
    /// Fund Admin or higher — can manage membership, approve loans, etc.
    /// </summary>
    public const string FundAdminOrAbove = nameof(FundAdminOrAbove);

    /// <summary>
    /// Fund Editor or higher — can record payments, request loans.
    /// </summary>
    public const string FundEditorOrAbove = nameof(FundEditorOrAbove);

    /// <summary>
    /// Any fund member — at least Guest role.
    /// </summary>
    public const string FundMember = nameof(FundMember);

    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(PlatformAdmin, policy =>
            policy.RequireClaim("role", "PlatformAdmin"));

        options.AddPolicy(FundAdmin, policy =>
            policy.RequireClaim("fund_role", "Admin"));

        options.AddPolicy(FundEditor, policy =>
            policy.RequireClaim("fund_role", "Editor"));

        options.AddPolicy(FundGuest, policy =>
            policy.RequireClaim("fund_role", "Guest"));

        options.AddPolicy(FundAdminOrAbove, policy =>
            policy.RequireClaim("fund_role", "Admin", "PlatformAdmin"));

        options.AddPolicy(FundEditorOrAbove, policy =>
            policy.RequireClaim("fund_role", "Admin", "Editor"));

        options.AddPolicy(FundMember, policy =>
            policy.RequireClaim("fund_role", "Admin", "Editor", "Guest"));
    }
}
