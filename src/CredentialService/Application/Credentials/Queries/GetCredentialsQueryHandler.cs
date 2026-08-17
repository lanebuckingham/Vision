using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed class GetCredentialsQueryHandler(CredentialDbContext db)
    : IRequestHandler<GetCredentialsQuery, PagedList<CredentialListItemDto>>
{
    public async Task<PagedList<CredentialListItemDto>> Handle(
        GetCredentialsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var query = db.Credentials
            .AsNoTracking()
            .Include(c => c.Person)
            .AsQueryable();

        // Status filter — translate derived status into persisted-field predicates
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<CredentialStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = status switch
            {
                CredentialStatus.Active => query.Where(c => c.RevokedAt == null && c.ExpiresAt > now),
                CredentialStatus.Expired => query.Where(c => c.RevokedAt == null && c.ExpiresAt <= now),
                CredentialStatus.Revoked => query.Where(c => c.RevokedAt != null),
                _ => query
            };
        }

        // Access level filter
        if (!string.IsNullOrWhiteSpace(request.AccessLevel)
            && Enum.TryParse<CredentialAccessLevel>(request.AccessLevel, ignoreCase: true, out var accessLevel))
        {
            query = query.Where(c => c.AccessLevel == accessLevel);
        }

        // Person filter
        if (request.PersonId.HasValue)
        {
            query = query.Where(c => c.PersonId == request.PersonId.Value);
        }

        // Expiring soon filter
        if (request.ExpiringSoon == true)
        {
            query = query.Where(c =>
                c.RevokedAt == null &&
                c.ExpiresAt > now &&
                c.ExpiresAt <= expiringSoonThreshold);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.CredentialNumber, $"%{search}%") ||
                EF.Functions.ILike(c.Person.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.Person.LastName, $"%{search}%") ||
                (c.Person.EmployeeNumber != null && EF.Functions.ILike(c.Person.EmployeeNumber, $"%{search}%")) ||
                (c.Person.Department != null && EF.Functions.ILike(c.Person.Department, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.ExpiresAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CredentialListItemDto(
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
                new CredentialPersonDto(
                    c.Person.Id,
                    c.Person.FirstName + " " + c.Person.LastName,
                    c.Person.PersonType.ToString(),
                    c.Person.IsActive,
                    c.Person.EmployeeNumber,
                    c.Person.Department,
                    c.Person.JobTitle)))
            .ToListAsync(cancellationToken);

        return new PagedList<CredentialListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
