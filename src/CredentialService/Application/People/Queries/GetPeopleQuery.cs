using MediatR;
using Vision.CredentialService.Application.Common;

namespace Vision.CredentialService.Application.People.Queries;

public sealed record GetPeopleQuery(
    string? PersonType,
    bool? IsActive,
    string? Department,
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<PersonListItemDto>>;
