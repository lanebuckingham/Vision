using MediatR;
using Vision.SecurityOperationsService.API.Auth;
using Vision.SecurityOperationsService.Application.Assets.Commands;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/assets")
            .WithTags("Assets")
            .RequireAuthorization(VisionAuthExtensions.Policies.SecurityOperationsManager);

        group.MapGet("/", GetAssets).WithName("GetAssets");
        group.MapGet("/{id:guid}", GetAssetById).WithName("GetAssetById");
        group.MapPatch("/{id:guid}/status", UpdateAssetStatus).WithName("UpdateAssetStatus");

        return group;
    }

    private static async Task<IResult> GetAssets(
        ISender mediator, string? status, string? type, Guid? buildingId, Guid? locationId,
        string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = new GetAssetsQuery(status, type, buildingId, locationId, search, page ?? 1, pageSize ?? 25);
        return Results.Ok(await mediator.Send(query, cancellationToken));
    }

    private static async Task<IResult> GetAssetById(Guid id, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAssetByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateAssetStatus(
        Guid id, UpdateAssetStatusRequest request, ISender mediator, CancellationToken cancellationToken)
    {
        var command = new UpdateAssetStatusCommand(id, request.Status);
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }
}

public sealed record UpdateAssetStatusRequest(string Status);
