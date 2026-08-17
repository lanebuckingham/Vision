using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Queries;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed class RevokeCredentialCommandHandler(
    CredentialDbContext db,
    ILogger<RevokeCredentialCommandHandler> logger)
    : IRequestHandler<RevokeCredentialCommand, CredentialDetailDto>
{
    public async Task<CredentialDetailDto> Handle(
        RevokeCredentialCommand request, CancellationToken cancellationToken)
    {
        var credential = await db.Credentials
            .Include(c => c.Person)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Credential {request.Id} not found.");

        var wasAlreadyRevoked = credential.RevokedAt is not null;

        credential.Revoke(request.Reason);
        await db.SaveChangesAsync(cancellationToken);

        if (!wasAlreadyRevoked)
        {
            logger.LogInformation(
                "Credential {CredentialId} revoked for person {PersonId}",
                credential.Id,
                credential.PersonId);
        }
        else
        {
            logger.LogInformation(
                "Credential {CredentialId} already revoked; idempotent request accepted",
                credential.Id);
        }

        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var person = credential.Person;

        return new CredentialDetailDto(
            credential.Id,
            credential.CredentialNumber,
            credential.AccessLevel.ToString(),
            credential.RevokedAt != null
                ? "Revoked"
                : credential.ExpiresAt <= now
                    ? "Expired"
                    : "Active",
            credential.IssuedAt,
            credential.ExpiresAt,
            credential.RevokedAt == null && credential.ExpiresAt > now && credential.ExpiresAt <= expiringSoonThreshold,
            credential.RevokedAt,
            credential.RevocationReason,
            credential.CreatedAt,
            credential.UpdatedAt,
            new CredentialDetailPersonDto(
                person.Id,
                person.FirstName,
                person.LastName,
                person.FirstName + " " + person.LastName,
                person.PersonType.ToString(),
                person.IsActive,
                person.EmployeeNumber,
                person.Email,
                person.Department,
                person.JobTitle));
    }
}
