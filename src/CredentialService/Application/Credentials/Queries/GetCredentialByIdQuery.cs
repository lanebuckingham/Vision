using MediatR;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed record GetCredentialByIdQuery(Guid Id) : IRequest<CredentialDetailDto?>;
