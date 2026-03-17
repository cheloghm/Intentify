using Intentify.Modules.Auth.Application;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Infrastructure;

public sealed class InvitationRepository : IInvitationRepository
{
    private readonly IMongoCollection<Invitation> _invitations;
    private readonly Task _ensureIndexes;

    public InvitationRepository(IMongoDatabase database)
    {
        _invitations = database.GetCollection<Invitation>(AuthMongoCollections.Invitations);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _invitations.Find(invitation => invitation.Token == token).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Invitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _invitations.Find(invitation => invitation.Id == invitationId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Invitation>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _invitations
            .Find(invitation => invitation.TenantId == tenantId)
            .SortByDescending(invitation => invitation.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _invitations.InsertOneAsync(invitation, cancellationToken: cancellationToken);
    }

    public async Task<bool> MarkAcceptedAsync(Guid invitationId, DateTime acceptedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<Invitation>.Filter.Eq(invitation => invitation.Id, invitationId)
            & Builders<Invitation>.Filter.Eq(invitation => invitation.AcceptedAtUtc, null)
            & Builders<Invitation>.Filter.Eq(invitation => invitation.RevokedAtUtc, null);

        var update = Builders<Invitation>.Update
            .Set(invitation => invitation.AcceptedAtUtc, acceptedAtUtc)
            .Set(invitation => invitation.UpdatedAtUtc, acceptedAtUtc);

        var result = await _invitations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> MarkRevokedAsync(Guid invitationId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<Invitation>.Filter.Eq(invitation => invitation.Id, invitationId)
            & Builders<Invitation>.Filter.Eq(invitation => invitation.AcceptedAtUtc, null)
            & Builders<Invitation>.Filter.Eq(invitation => invitation.RevokedAtUtc, null)
            & Builders<Invitation>.Filter.Gt(invitation => invitation.ExpiresAtUtc, revokedAtUtc);

        var update = Builders<Invitation>.Update
            .Set(invitation => invitation.RevokedAtUtc, revokedAtUtc)
            .Set(invitation => invitation.UpdatedAtUtc, revokedAtUtc);

        var result = await _invitations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Invitation>(
                Builders<Invitation>.IndexKeys.Ascending(invitation => invitation.Token),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Invitation>(
                Builders<Invitation>.IndexKeys.Ascending(invitation => invitation.TenantId).Ascending(invitation => invitation.Email))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_invitations, indexes);
    }
}
