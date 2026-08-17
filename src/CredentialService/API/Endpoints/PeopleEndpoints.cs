using MediatR;
using Vision.CredentialService.API.Auth;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.People.Queries;

namespace Vision.CredentialService.API.Endpoints;

public static class PeopleEndpoints
{
    public static RouteGroupBuilder MapPeopleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/people")
            .WithTags("People")
            .RequireAuthorization(VisionAuthExtensions.Policies.CredentialAdmin);

        group.MapGet("/", GetPeople).WithName("GetPeople");
        group.MapGet("/{id:guid}", GetPersonById).WithName("GetPersonById");

        return group;
    }

    private static async Task<IResult> GetPeople(
        ISender mediator, string? personType, bool? isActive, string? department,
        string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = new GetPeopleQuery(personType, isActive, department, search, page ?? 1, pageSize ?? 25);
        return Results.Ok(await mediator.Send(query, cancellationToken));
    }

    private static async Task<IResult> GetPersonById(Guid id, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPersonByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
