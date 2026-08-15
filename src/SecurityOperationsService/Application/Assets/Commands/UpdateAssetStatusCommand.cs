using MediatR;
using Vision.SecurityOperationsService.Application.Assets.Queries;

namespace Vision.SecurityOperationsService.Application.Assets.Commands;

public sealed record UpdateAssetStatusCommand(
    Guid Id,
    string Status) : IRequest<AssetDetailDto>;
