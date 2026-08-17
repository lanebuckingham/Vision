using MediatR;
using Vision.CredentialService.Application.Credentials.Queries;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed record RevokeCredentialCommand(Guid Id, string Reason) : IRequest<CredentialDetailDto>;
