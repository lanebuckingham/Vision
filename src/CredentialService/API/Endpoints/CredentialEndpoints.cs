using MediatR;
using Vision.CredentialService.API.Auth;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Commands;
using Vision.CredentialService.Application.Credentials.Queries;

namespace Vision.CredentialService.API.Endpoints;

public static class CredentialEndpoints
{
    public static RouteGroupBuilder MapCredentialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/credentials")
            .WithTags("Credentials")
            .RequireAuthorization(VisionAuthExtensions.Policies.CredentialAdmin);

        group.MapGet("/", GetCredentials).WithName("GetCredentials");
        group.MapGet("/summary", GetCredentialSummary).WithName("GetCredentialSummary");
        group.MapGet("/{id:guid}", GetCredentialById).WithName("GetCredentialById");
        group.MapPost("/{id:guid}/revoke", RevokeCredential).WithName("RevokeCredential");

        return group;
    }

    public static RouteGroupBuilder MapCredentialIssuanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/people/{personId:guid}/credentials")
            .WithTags("Credentials")
            .RequireAuthorization(VisionAuthExtensions.Policies.CredentialAdmin);

        group.MapPost("/", IssueCredential).WithName("IssueCredential");

        return group;
    }

    private static async Task<IResult> GetCredentials(
        ISender mediator, string? status, string? accessLevel, Guid? personId,
        bool? expiringSoon, string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = new GetCredentialsQuery(status, accessLevel, personId, expiringSoon, search, page ?? 1, pageSize ?? 25);
        return Results.Ok(await mediator.Send(query, cancellationToken));
    }

    private static async Task<IResult> GetCredentialSummary(ISender mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new GetCredentialSummaryQuery(), cancellationToken));
    }

    private static async Task<IResult> GetCredentialById(Guid id, ISender mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCredentialByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> RevokeCredential(
        Guid id, RevokeCredentialRequest request, ISender mediator, CancellationToken cancellationToken)
    {
        var command = new RevokeCredentialCommand(id, request.Reason);
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }

    private static async Task<IResult> IssueCredential(
        Guid personId, IssueCredentialRequest request, ISender mediator, CancellationToken cancellationToken)
    {
        var command = new IssueCredentialCommand(personId, request.CredentialNumber, request.AccessLevel, request.ExpiresAt);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/v1/credentials/{result.Id}", result);
    }
}

public sealed record RevokeCredentialRequest(string Reason);
public sealed record IssueCredentialRequest(string CredentialNumber, string AccessLevel, DateTimeOffset ExpiresAt);
