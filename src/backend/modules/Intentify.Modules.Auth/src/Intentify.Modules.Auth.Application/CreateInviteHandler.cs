using System.Net.Mail;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed record InviteResult(string Token, string Email, string Role, DateTime ExpiresAtUtc);

public sealed class CreateInviteHandler
{
    private readonly IInvitationRepository _invitations;

    public CreateInviteHandler(IInvitationRepository invitations)
    {
        _invitations = invitations;
    }

    public async Task<OperationResult<InviteResult>> HandleAsync(CreateInviteCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command);
        if (errors.HasErrors)
        {
            return OperationResult<InviteResult>.ValidationFailed(errors);
        }

        var actorRole = AuthRoleHierarchy.ResolveActorRole(command.InviterRoles);
        if (!CanCreateInvite(actorRole, command.Role))
        {
            return OperationResult<InviteResult>.Forbidden();
        }

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddDays(7);
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());

        var invite = new Invitation
        {
            TenantId = command.TenantId,
            CreatedByUserId = command.InvitedByUserId,
            Email = command.Email.Trim(),
            Role = command.Role.Trim().ToLowerInvariant(),
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _invitations.InsertAsync(invite, cancellationToken);

        return OperationResult<InviteResult>.Success(new InviteResult(invite.Token, invite.Email, invite.Role, invite.ExpiresAtUtc));
    }

    private static ValidationErrors Validate(CreateInviteCommand command)
    {
        var errors = new ValidationErrors();

        if (!IsValidEmail(command.Email))
        {
            errors.Add("email", "Email is invalid.");
        }

        var normalizedRole = AuthRoleHierarchy.NormalizeRole(command.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            errors.Add("role", "Role is required.");
        }
        else if (!AuthRoleHierarchy.IsSupportedInviteRole(normalizedRole))
        {
            errors.Add("role", "Role must be 'user', 'manager', or 'admin'.");
        }

        return errors;
    }

    private static bool CanCreateInvite(TenantActorRole actorRole, string role)
    {
        return AuthRoleHierarchy.CanInvite(actorRole, role);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmedEmail = email.Trim();
        try
        {
            var mailAddress = new MailAddress(trimmedEmail);
            if (!string.Equals(mailAddress.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var atIndex = trimmedEmail.IndexOf('@');
            if (atIndex <= 0 || atIndex == trimmedEmail.Length - 1)
            {
                return false;
            }

            var domain = trimmedEmail[(atIndex + 1)..];
            return domain.Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
