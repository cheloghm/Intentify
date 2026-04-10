using Intentify.Modules.Auth.Application;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Infrastructure;

public sealed class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly Task _ensureIndexes;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>(AuthMongoCollections.Users);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _users.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _users.Find(user => user.Email == email).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TenantUserListItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        return await _users.Find(user => user.TenantId == tenantId)
            .SortBy(user => user.Email)
            .Project(user => new TenantUserListItem(
                user.Id,
                user.TenantId,
                user.Email,
                user.DisplayName,
                user.Roles,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(User user, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    public async Task UpdateDisplayNameAsync(Guid id, string displayName, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<User>.Filter.Eq(user => user.Id, id);
        var update = Builders<User>.Update
            .Set(user => user.DisplayName, displayName)
            .Set(user => user.UpdatedAt, updatedAt);

        await _users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateRolesAsync(Guid id, IReadOnlyCollection<string> roles, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<User>.Filter.Eq(user => user.Id, id);
        var update = Builders<User>.Update
            .AddToSetEach(user => user.Roles, roles)
            .Set(user => user.UpdatedAt, updatedAt);

        await _users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<User>.Filter.Eq(user => user.Id, id);
        var update = Builders<User>.Update
            .Set(user => user.IsActive, false)
            .Set(user => user.UpdatedAt, updatedAt);

        await _users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<int> CountActiveByTenantAndRoleAsync(Guid tenantId, string role, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var normalizedRole = role.Trim().ToLowerInvariant();
        var count = await _users.CountDocumentsAsync(user =>
            user.TenantId == tenantId
            && user.IsActive
            && user.Roles.Any(candidate => candidate == normalizedRole),
            cancellationToken: cancellationToken);

        return (int)count;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(user => user.Email),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_users, indexes);
    }
}
