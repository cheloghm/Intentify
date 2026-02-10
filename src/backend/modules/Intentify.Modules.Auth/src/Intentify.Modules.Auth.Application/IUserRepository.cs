using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task InsertAsync(User user, CancellationToken cancellationToken = default);
}
