using MediatR;
using Vision.SecurityOperationsService.Application.Assets.Queries;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/assets")
            .WithTags("Assets");

        group.MapGet("/", GetAssets)
            .WithName("GetAssets")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetAssetById)
            .WithName("GetAssetById")
            .Produces<AssetDetailDto>(StatusCodes.Status200OK)
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
}
