using MediatR;
using Vision.WorkOrderService.API.Auth;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Application.Technicians.Queries;

namespace Vision.WorkOrderService.API.Endpoints;

public static class TechnicianEndpoints
{
    public static RouteGroupBuilder MapTechnicianEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/technicians")
            .WithTags("Technicians")
            .RequireAuthorization(VisionAuthExtensions.Policies.WorkOrderManager);

        group.MapGet("/", GetTechnicians).WithName("GetTechnicians");
        group.MapGet("/{id:guid}", GetTechnicianById).WithName("GetTechnicianById");

        return group;
    }

    private static async Task<IResult> GetTechnicians(
        ISender mediator, bool? activeOnly, string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = new GetTechniciansQuery(activeOnly ?? true, search, page ?? 1, pageSize ?? 25);
        return Results.Ok(await mediator.Send(query, cancellationToken));
    }

    private static async Task<IResult> GetTechnicianById(Guid id, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTechnicianByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
