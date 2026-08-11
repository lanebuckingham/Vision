using MediatR;
using Vision.SecurityOperationsService.Application.Dashboard.Queries;

namespace Vision.SecurityOperationsService.API.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Dashboard");

        group.MapGet("/dashboard", GetDashboard)
            .WithName("GetDashboard")
            .Produces<SecurityDashboardDto>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> GetDashboard(
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSecurityDashboardQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
