using MediatR;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Application.Technicians.Queries;

namespace Vision.WorkOrderService.API.Endpoints;

public static class TechnicianEndpoints
{
    public static RouteGroupBuilder MapTechnicianEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/technicians")
            .WithTags("Technicians");

        group.MapGet("/", GetTechnicians)
            .WithName("GetTechnicians")
            .Produces<PagedList<TechnicianListItemDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetTechnicianById)
            .WithName("GetTechnicianById")
            .Produces<TechnicianDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetTechnicians(
        ISender mediator,
        bool? activeOnly,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetTechniciansQuery(
            ActiveOnly: activeOnly ?? true,
            Search: search,
            Page: page ?? 1,
            PageSize: pageSize ?? 25);

        var result = await mediator.Send(query, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTechnicianById(
        Guid id,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTechnicianByIdQuery(id), cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }
}
