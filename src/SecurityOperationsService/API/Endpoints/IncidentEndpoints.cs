using MediatR;
using Vision.SecurityOperationsService.API.Auth;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Application.Incidents.Commands;
using Vision.SecurityOperationsService.Application.Incidents.Queries;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class IncidentEndpoints
{
    public static RouteGroupBuilder MapIncidentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/incidents")
            .WithTags("Incidents")
            .RequireAuthorization(VisionAuthExtensions.Policies.SecurityOperationsManager);

        group.MapGet("/", GetIncidents).WithName("GetIncidents");
        group.MapGet("/{id:guid}", GetIncidentById).WithName("GetIncidentById");
        group.MapPost("/", CreateIncident).WithName("CreateIncident");
        group.MapPatch("/{id:guid}", UpdateIncidentStatus).WithName("UpdateIncidentStatus");

        return group;
    }

    private static async Task<IResult> GetIncidents(
        ISender mediator, string? status, string? severity, Guid? assetId, Guid? buildingId,
        Guid? locationId, string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = new GetIncidentsQuery(status, severity, assetId, buildingId, locationId, search, page ?? 1, pageSize ?? 25);
        return Results.Ok(await mediator.Send(query, cancellationToken));
    }

    private static async Task<IResult> GetIncidentById(Guid id, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetIncidentByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateIncident(
        CreateIncidentCommand command, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/v1/incidents/{result.Id}", result);
    }

    private static async Task<IResult> UpdateIncidentStatus(
        Guid id, UpdateIncidentStatusRequest request, ISender mediator, CancellationToken cancellationToken)
    {
        var command = new UpdateIncidentStatusCommand(id, request.Status, request.ResolutionSummary);
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }
}

public sealed record UpdateIncidentStatusRequest(string Status, string? ResolutionSummary);
