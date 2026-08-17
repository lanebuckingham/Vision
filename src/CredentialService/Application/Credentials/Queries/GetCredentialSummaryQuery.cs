using MediatR;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed record GetCredentialSummaryQuery : IRequest<CredentialSummaryDto>;

public sealed record CredentialSummaryDto(
    int ActiveCount,
    int ExpiringSoonCount,
    int ExpiredCount,
    int RevokedCount);
