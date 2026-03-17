using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

internal enum TenantActorRole
{
    None = 0,
    User = 1,
    Manager = 2,
    Admin = 3,
    SuperAdmin = 4
}

internal static class AuthRoleHierarchy
{
    private static readonly string[] SupportedRoles = [
        AuthRoles.User,
        AuthRoles.Manager,
        AuthRoles.Admin,
        AuthRoles.SuperAdmin
    ];

    private static readonly string[] SupportedInviteRoles = [
        AuthRoles.User,
        AuthRoles.Manager,
        AuthRoles.Admin
    ];

    public static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var normalized = role.Trim().ToLowerInvariant();
        return SupportedRoles.Contains(normalized) ? normalized : null;
    }

    public static TenantActorRole ResolveActorRole(IReadOnlyCollection<string> roles)
    {
        var actorRole = TenantActorRole.None;

        foreach (var role in roles)
        {
            var normalized = NormalizeRole(role);
            if (normalized is null)
            {
                continue;
            }

            if (string.Equals(normalized, AuthRoles.SuperAdmin, StringComparison.Ordinal))
            {
                return TenantActorRole.SuperAdmin;
            }

            if (string.Equals(normalized, AuthRoles.Admin, StringComparison.Ordinal))
            {
                actorRole = TenantActorRole.Admin;
                continue;
            }

            if (string.Equals(normalized, AuthRoles.Manager, StringComparison.Ordinal) && actorRole < TenantActorRole.Manager)
            {
                actorRole = TenantActorRole.Manager;
                continue;
            }

            if (string.Equals(normalized, AuthRoles.User, StringComparison.Ordinal) && actorRole < TenantActorRole.User)
            {
                actorRole = TenantActorRole.User;
            }
        }

        return actorRole;
    }

    public static bool IsSupportedInviteRole(string? role)
    {
        var normalized = NormalizeRole(role);
        return normalized is not null && SupportedInviteRoles.Contains(normalized);
    }

    public static bool CanListUsers(TenantActorRole actorRole)
        => actorRole is TenantActorRole.SuperAdmin or TenantActorRole.Admin or TenantActorRole.Manager;

    public static bool CanInvite(TenantActorRole actorRole, string targetRole)
    {
        var normalized = NormalizeRole(targetRole);
        if (normalized is null || !SupportedInviteRoles.Contains(normalized))
        {
            return false;
        }

        return actorRole switch
        {
            TenantActorRole.SuperAdmin => normalized is AuthRoles.Admin or AuthRoles.Manager or AuthRoles.User,
            TenantActorRole.Admin => normalized is AuthRoles.Manager or AuthRoles.User,
            TenantActorRole.Manager => normalized == AuthRoles.User,
            _ => false
        };
    }

    public static bool CanManage(TenantActorRole actorRole, string targetRole)
    {
        var normalized = NormalizeRole(targetRole);
        if (normalized is null)
        {
            return false;
        }

        if (normalized == AuthRoles.SuperAdmin)
        {
            return false;
        }

        return actorRole switch
        {
            TenantActorRole.SuperAdmin => normalized is AuthRoles.Admin or AuthRoles.Manager or AuthRoles.User,
            TenantActorRole.Admin => normalized is AuthRoles.Manager or AuthRoles.User,
            TenantActorRole.Manager => normalized == AuthRoles.User,
            _ => false
        };
    }
}
