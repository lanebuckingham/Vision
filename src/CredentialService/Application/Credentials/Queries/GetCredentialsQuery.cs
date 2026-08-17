using MediatR;
using Vision.CredentialService.Application.Common;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed record GetCredentialsQuery(
    string? Status,
    string? AccessLevel,
    Guid? PersonId,
    bool? ExpiringSoon,
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<CredentialListItemDto>>;
