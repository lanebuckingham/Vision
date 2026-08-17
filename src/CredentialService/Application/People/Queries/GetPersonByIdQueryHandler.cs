using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Queries;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.People.Queries;

public sealed class GetPersonByIdQueryHandler(CredentialDbContext db)
    : IRequestHandler<GetPersonByIdQuery, PersonDetailDto?>
{
    public async Task<PersonDetailDto?> Handle(
        GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var person = await db.People
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new PersonDetailDto(
                p.Id,
                p.FirstName,
                p.LastName,
                p.FirstName + " " + p.LastName,
                p.PersonType.ToString(),
                p.IsActive,
                p.EmployeeNumber,
                p.Email,
                p.Department,
                p.JobTitle,
                p.CreatedAt,
                p.UpdatedAt,
                p.Credentials
                    .OrderByDescending(c => c.IssuedAt)
                    .Select(c => new PersonCredentialDto(
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
                        c.RevocationReason))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return person;
    }
}
