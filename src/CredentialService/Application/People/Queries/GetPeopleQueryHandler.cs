using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.People.Queries;

public sealed class GetPeopleQueryHandler(CredentialDbContext db)
    : IRequestHandler<GetPeopleQuery, PagedList<PersonListItemDto>>
{
    public async Task<PagedList<PersonListItemDto>> Handle(
        GetPeopleQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var query = db.People
            .AsNoTracking()
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(request.PersonType)
            && Enum.TryParse<PersonType>(request.PersonType, ignoreCase: true, out var personType))
        {
            query = query.Where(p => p.PersonType == personType);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            var dept = request.Department.Trim();
            query = query.Where(p => p.Department != null && EF.Functions.ILike(p.Department, dept));
        }

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, $"%{search}%") ||
                EF.Functions.ILike(p.LastName, $"%{search}%") ||
                (p.EmployeeNumber != null && EF.Functions.ILike(p.EmployeeNumber, $"%{search}%")) ||
                (p.Email != null && EF.Functions.ILike(p.Email, $"%{search}%")) ||
                (p.Department != null && EF.Functions.ILike(p.Department, $"%{search}%")) ||
                (p.JobTitle != null && EF.Functions.ILike(p.JobTitle, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PersonListItemDto(
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
                new PersonCredentialSummaryDto(
                    p.Credentials.Count(c => c.RevokedAt == null && c.ExpiresAt > now),
                    p.Credentials.Count(c => c.RevokedAt == null && c.ExpiresAt > now && c.ExpiresAt <= expiringSoonThreshold),
                    p.Credentials.Count(c => c.RevokedAt != null))))
            .ToListAsync(cancellationToken);

        return new PagedList<PersonListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
