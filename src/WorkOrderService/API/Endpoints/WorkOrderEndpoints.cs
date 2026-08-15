using MediatR;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Application.WorkOrders.Commands;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.API.Endpoints;

public static class WorkOrderEndpoints
{
    public static RouteGroupBuilder MapWorkOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/work-orders")
            .WithTags("Work Orders");

        group.MapGet("/", GetWorkOrders)
            .WithName("GetWorkOrders")
            .Produces<PagedList<WorkOrderListItemDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetWorkOrderById)
            .WithName("GetWorkOrderById")
            .Produces<WorkOrderDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateWorkOrder)
            .WithName("CreateWorkOrder")
            .Produces<WorkOrderDetailDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/assignment", AssignTechnician)
            .WithName("AssignTechnician")
            .Produces<WorkOrderDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/start", StartWork)
            .WithName("StartWork")
            .Produces<WorkOrderDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/notes", AddTechnicianNote)
            .WithName("AddTechnicianNote")
            .Produces<TechnicianNoteDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", CompleteWorkOrder)
            .WithName("CompleteWorkOrder")
            .Produces<WorkOrderDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/summary", GetWorkOrderSummary)
            .WithName("GetWorkOrderSummary")
            .Produces<WorkOrderSummaryDto>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> GetWorkOrders(
        ISender mediator,
        string? status,
        string? priority,
        Guid? technicianId,
        Guid? assetId,
        Guid? incidentId,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetWorkOrdersQuery(
            Status: status,
            Priority: priority,
            TechnicianId: technicianId,
            AssetId: assetId,
            IncidentId: incidentId,
            Search: search,
            Page: page ?? 1,
            PageSize: pageSize ?? 25);

        var result = await mediator.Send(query, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetWorkOrderById(
        Guid id,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkOrderByIdQuery(id), cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateWorkOrder(
        CreateWorkOrderCommand command,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return Results.Created($"/api/v1/work-orders/{result.Id}", result);
    }

    private static async Task<IResult> AssignTechnician(
        Guid id,
        AssignTechnicianRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new AssignTechnicianCommand(id, request.TechnicianId);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> StartWork(
        Guid id,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new StartWorkCommand(id);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> AddTechnicianNote(
        Guid id,
        AddTechnicianNoteRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddTechnicianNoteCommand(id, request.Content);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Created($"/api/v1/work-orders/{id}", result);
    }

    private static async Task<IResult> CompleteWorkOrder(
        Guid id,
        CompleteWorkOrderRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new CompleteWorkOrderCommand(id, request.CompletionSummary);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetWorkOrderSummary(
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkOrderSummaryQuery(), cancellationToken);

        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for POST /api/v1/work-orders/{id}/assignment.
/// Separated from the command to bind the route ID separately.
/// </summary>
public sealed record AssignTechnicianRequest(Guid TechnicianId);

/// <summary>
/// Request body for POST /api/v1/work-orders/{id}/notes.
/// </summary>
public sealed record AddTechnicianNoteRequest(string Content);

/// <summary>
/// Request body for POST /api/v1/work-orders/{id}/complete.
/// </summary>
public sealed record CompleteWorkOrderRequest(string? CompletionSummary);
