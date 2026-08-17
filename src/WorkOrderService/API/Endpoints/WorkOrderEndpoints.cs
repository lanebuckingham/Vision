using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.API.Auth;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Application.WorkOrders.Commands;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.API.Endpoints;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this WebApplication app)
    {
        // --- Manager-only routes ---
        var managerGroup = app.MapGroup("/api/v1/work-orders")
            .WithTags("Work Orders");

        managerGroup.MapGet("/summary", GetWorkOrderSummary)
            .WithName("GetWorkOrderSummary")
            .RequireAuthorization(VisionAuthExtensions.Policies.WorkOrderManager);

        managerGroup.MapPost("/", CreateWorkOrder)
            .WithName("CreateWorkOrder")
            .RequireAuthorization(VisionAuthExtensions.Policies.WorkOrderManager);

        managerGroup.MapPost("/{id:guid}/assignment", AssignTechnician)
            .WithName("AssignTechnician")
            .RequireAuthorization(VisionAuthExtensions.Policies.WorkOrderManager);

        // --- Technician-only routes (ownership enforced in handler) ---
        managerGroup.MapPost("/{id:guid}/start", StartWork)
            .WithName("StartWork")
            .RequireAuthorization(VisionAuthExtensions.Policies.TechnicianWork);

        managerGroup.MapPost("/{id:guid}/notes", AddTechnicianNote)
            .WithName("AddTechnicianNote")
            .RequireAuthorization(VisionAuthExtensions.Policies.TechnicianWork);

        managerGroup.MapPost("/{id:guid}/complete", CompleteWorkOrder)
            .WithName("CompleteWorkOrder")
            .RequireAuthorization(VisionAuthExtensions.Policies.TechnicianWork);

        // --- Dual-access routes (Manager sees all, Technician sees own) ---
        managerGroup.MapGet("/", GetWorkOrders)
            .WithName("GetWorkOrders")
            .RequireAuthorization(); // authenticated user; logic handles role-based filtering

        managerGroup.MapGet("/{id:guid}", GetWorkOrderById)
            .WithName("GetWorkOrderById")
            .RequireAuthorization(); // authenticated user; logic handles role-based access
    }

    private static async Task<IResult> GetWorkOrders(
        ISender mediator, HttpContext httpContext, WorkOrderDbContext db,
        string? status, string? priority, Guid? technicianId, Guid? assetId,
        Guid? incidentId, string? search, int? page, int? pageSize,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;

        if (IsManager(user))
        {
            // SecurityManager sees all work orders, can use filters freely
            var query = new GetWorkOrdersQuery(status, priority, technicianId, assetId, incidentId, search, page ?? 1, pageSize ?? 25);
            return Results.Ok(await mediator.Send(query, cancellationToken));
        }

        if (IsTechnician(user))
        {
            var resolvedTechId = await TechnicianIdentityResolver.ResolveTechnicianIdAsync(user, db, cancellationToken);
            if (resolvedTechId is null)
                return Results.Forbid();

            // Force technician's own ID regardless of client-supplied technicianId
            var query = new GetWorkOrdersQuery(status, priority, resolvedTechId, assetId, incidentId, search, page ?? 1, pageSize ?? 25);
            return Results.Ok(await mediator.Send(query, cancellationToken));
        }

        return Results.Forbid();
    }

    private static async Task<IResult> GetWorkOrderById(
        Guid id, ISender mediator, HttpContext httpContext, WorkOrderDbContext db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;

        if (IsManager(user))
        {
            var result = await mediator.Send(new GetWorkOrderByIdQuery(id), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }

        if (IsTechnician(user))
        {
            var resolvedTechId = await TechnicianIdentityResolver.ResolveTechnicianIdAsync(user, db, cancellationToken);
            if (resolvedTechId is null)
                return Results.Forbid();

            var result = await mediator.Send(new GetWorkOrderByIdQuery(id), cancellationToken);
            if (result is null)
                return Results.NotFound();

            if (result.AssignedTechnician?.Id != resolvedTechId.Value)
                return Results.Forbid();

            return Results.Ok(result);
        }

        return Results.Forbid();
    }

    private static async Task<IResult> GetWorkOrderSummary(ISender mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new GetWorkOrderSummaryQuery(), cancellationToken));
    }

    private static async Task<IResult> CreateWorkOrder(
        CreateWorkOrderCommand command, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/v1/work-orders/{result.Id}", result);
    }

    private static async Task<IResult> AssignTechnician(
        Guid id, AssignTechnicianRequest request, ISender mediator, CancellationToken cancellationToken)
    {
        var command = new AssignTechnicianCommand(id, request.TechnicianId);
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }

    private static async Task<IResult> StartWork(
        Guid id, HttpContext httpContext, WorkOrderDbContext db, ISender mediator,
        CancellationToken cancellationToken)
    {
        var techId = await ResolveTechnicianOrForbid(httpContext.User, db, cancellationToken);
        if (techId is null) return Results.Forbid();

        if (!await IsAssignedToTechnician(id, techId.Value, db, cancellationToken))
            return Results.Forbid();

        return Results.Ok(await mediator.Send(new StartWorkCommand(id), cancellationToken));
    }

    private static async Task<IResult> AddTechnicianNote(
        Guid id, AddTechnicianNoteRequest request, HttpContext httpContext, WorkOrderDbContext db,
        ISender mediator, CancellationToken cancellationToken)
    {
        var techId = await ResolveTechnicianOrForbid(httpContext.User, db, cancellationToken);
        if (techId is null) return Results.Forbid();

        if (!await IsAssignedToTechnician(id, techId.Value, db, cancellationToken))
            return Results.Forbid();

        // Use authenticated technician identity, not client-supplied
        var command = new AddTechnicianNoteCommand(id, request.Content);
        return Results.Created($"/api/v1/work-orders/{id}", await mediator.Send(command, cancellationToken));
    }

    private static async Task<IResult> CompleteWorkOrder(
        Guid id, CompleteWorkOrderRequest request, HttpContext httpContext, WorkOrderDbContext db,
        ISender mediator, CancellationToken cancellationToken)
    {
        var techId = await ResolveTechnicianOrForbid(httpContext.User, db, cancellationToken);
        if (techId is null) return Results.Forbid();

        if (!await IsAssignedToTechnician(id, techId.Value, db, cancellationToken))
            return Results.Forbid();

        var command = new CompleteWorkOrderCommand(id, request.CompletionSummary);
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }

    // --- Helpers ---

    private static bool IsManager(ClaimsPrincipal user)
    {
        return user.HasClaim(VisionAuthExtensions.CognitoGroupsClaim, "SecurityManager")
            || user.IsInRole("SecurityManager");
    }

    private static bool IsTechnician(ClaimsPrincipal user)
    {
        return user.HasClaim(VisionAuthExtensions.CognitoGroupsClaim, "Technician")
            || user.IsInRole("Technician");
    }

    private static async Task<Guid?> ResolveTechnicianOrForbid(
        ClaimsPrincipal user, WorkOrderDbContext db, CancellationToken cancellationToken)
    {
        return await TechnicianIdentityResolver.ResolveTechnicianIdAsync(user, db, cancellationToken);
    }

    private static async Task<bool> IsAssignedToTechnician(
        Guid workOrderId, Guid technicianId, WorkOrderDbContext db, CancellationToken cancellationToken)
    {
        return await db.WorkOrders
            .AsNoTracking()
            .AnyAsync(wo => wo.Id == workOrderId && wo.AssignedTechnicianId == technicianId, cancellationToken);
    }
}

public sealed record AssignTechnicianRequest(Guid TechnicianId);
public sealed record AddTechnicianNoteRequest(string Content);
public sealed record CompleteWorkOrderRequest(string? CompletionSummary);
