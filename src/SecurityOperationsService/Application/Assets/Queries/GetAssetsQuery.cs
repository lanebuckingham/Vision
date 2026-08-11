using MediatR;
using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.Application.Assets.Queries;

public sealed record GetAssetsQuery(
    string? Status,
    string? Type,
    Guid? BuildingId,
    Guid? LocationId,
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<AssetListItemDto>>;
