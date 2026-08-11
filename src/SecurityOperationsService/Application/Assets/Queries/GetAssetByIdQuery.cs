using MediatR;

namespace Vision.SecurityOperationsService.Application.Assets.Queries;

public sealed record GetAssetByIdQuery(Guid Id) : IRequest<AssetDetailDto?>;
