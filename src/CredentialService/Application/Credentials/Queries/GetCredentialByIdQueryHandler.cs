using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed class GetCredentialByIdQueryHandler(CredentialDbContext db)
    : IRequestHandler<GetCredentialByIdQuery, CredentialDetailDto?>
{
    public async Task<CredentialDetailDto?> Handle(
        GetCredentialByIdQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var credential = await db.Credentials
            .AsNoTracking()
            .Include(c => c.Person)
            .Where(c => c.Id == request.Id)
            .Select(c => new CredentialDetailDto(
                c.Id,
                c.CredentialNumber,
                c.AccessLevel.ToString(),
                c.RevokedAt != null
                    ? "Revoked"
                    : c.ExpiresAt <= now
                        ? "Expired"
                        : "Active",
                c.IssuedAt,
                c.ExpiresAt,
                c.RevokedAt == null && c.ExpiresAt > now && c.ExpiresAt <= expiringSoonThreshold,
                c.RevokedAt,
                c.RevocationReason,
                c.CreatedAt,
                c.UpdatedAt,
                new CredentialDetailPersonDto(
                    c.Person.Id,
                    c.Person.FirstName,
                    c.Person.LastName,
                    c.Person.FirstName + " " + c.Person.LastName,
                    c.Person.PersonType.ToString(),
                    c.Person.IsActive,
                    c.Person.EmployeeNumber,
                    c.Person.Email,
                    c.Person.Department,
                    c.Person.JobTitle)))
            .FirstOrDefaultAsync(cancellationToken);

        return credential;
    }
}
