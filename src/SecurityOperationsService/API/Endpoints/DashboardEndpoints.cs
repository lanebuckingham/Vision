using MediatR;
using Vision.SecurityOperationsService.API.Auth;
using Vision.SecurityOperationsService.Application.Dashboard.Queries;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Dashboard")
            .RequireAuthorization(VisionAuthExtensions.Policies.SecurityOperationsManager);

        group.MapGet("/dashboard", GetDashboard).WithName("GetDashboard");

        return group;
    }

    private static async Task<IResult> GetDashboard(ISender mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new GetSecurityDashboardQuery(), cancellationToken));
    }
}
