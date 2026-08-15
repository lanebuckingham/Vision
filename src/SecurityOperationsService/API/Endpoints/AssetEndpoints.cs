using MediatR;
using Vision.SecurityOperationsService.Application.Assets.Commands;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/assets")
            .WithTags("Assets");

        group.MapGet("/", GetAssets)
            .WithName("GetAssets")
            .Produces<PagedList<AssetListItemDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetAssetById)
            .WithName("GetAssetById")
            .Produces<AssetDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", UpdateAssetStatus)
            .WithName("UpdateAssetStatus")
            .Produces<AssetDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetAssets(
        ISender mediator,
        string? status,
        string? type,
        Guid? buildingId,
        Guid? locationId,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetAssetsQuery(
            Status: status,
            Type: type,
            BuildingId: buildingId,
            LocationId: locationId,
            Search: search,
            Page: page ?? 1,
            PageSize: pageSize ?? 25);

        var result = await mediator.Send(query, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAssetById(
        Guid id,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAssetByIdQuery(id), cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> UpdateAssetStatus(
        Guid id,
        UpdateAssetStatusRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAssetStatusCommand(id, request.Status);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for PATCH /api/v1/assets/{id}/status.
/// </summary>
public sealed record UpdateAssetStatusRequest(string Status);
