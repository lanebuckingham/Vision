using MediatR;
using Vision.CredentialService.Application.Credentials.Queries;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed record IssueCredentialCommand(
    Guid PersonId,
    string CredentialNumber,
    string AccessLevel,
    DateTimeOffset ExpiresAt) : IRequest<CredentialDetailDto>;
