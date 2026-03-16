using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task InsertAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateDisplayNameAsync(Guid id, string displayName, DateTime updatedAt, CancellationToken cancellationToken = default);
}
