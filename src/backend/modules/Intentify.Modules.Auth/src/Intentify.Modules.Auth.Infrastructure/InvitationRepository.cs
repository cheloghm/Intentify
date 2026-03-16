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
