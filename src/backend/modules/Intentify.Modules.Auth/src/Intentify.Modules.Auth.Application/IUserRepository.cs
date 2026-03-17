using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public sealed record TenantUserListItem(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TenantUserListItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateDisplayNameAsync(Guid id, string displayName, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task UpdateRolesAsync(Guid id, IReadOnlyCollection<string> roles, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<int> CountActiveByTenantAndRoleAsync(Guid tenantId, string role, CancellationToken cancellationToken = default);
}
