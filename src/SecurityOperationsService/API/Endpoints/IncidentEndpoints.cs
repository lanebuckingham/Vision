using MediatR;
using Vision.SecurityOperationsService.Application.Incidents.Commands;
using Vision.SecurityOperationsService.Application.Incidents.Queries;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class IncidentEndpoints
{
    public static RouteGroupBuilder MapIncidentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/incidents")
            .WithTags("Incidents");

        group.MapGet("/", GetIncidents)
            .WithName("GetIncidents")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIncidentById)
            .WithName("GetIncidentById")
            .Produces<IncidentDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateIncident)
            .WithName("CreateIncident")
            .Produces<IncidentDetailDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPatch("/{id:guid}", UpdateIncidentStatus)
            .WithName("UpdateIncidentStatus")
            .Produces<IncidentDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> GetIncidents(
        ISender mediator,
        string? status,
        string? severity,
        Guid? assetId,
        Guid? buildingId,
        Guid? locationId,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetIncidentsQuery(
            Status: status,
            Severity: severity,
            AssetId: assetId,
            BuildingId: buildingId,
            LocationId: locationId,
            Search: search,
            Page: page ?? 1,
            PageSize: pageSize ?? 25);

        var result = await mediator.Send(query, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetIncidentById(
        Guid id,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetIncidentByIdQuery(id), cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateIncident(
        CreateIncidentCommand command,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return Results.Created($"/api/v1/incidents/{result.Id}", result);
    }

    private static async Task<IResult> UpdateIncidentStatus(
        Guid id,
        UpdateIncidentStatusRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateIncidentStatusCommand(id, request.Status, request.ResolutionSummary);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for PATCH /api/v1/incidents/{id}.
/// Separated from the command to bind the route ID separately.
/// </summary>
public sealed record UpdateIncidentStatusRequest(string Status, string? ResolutionSummary);
