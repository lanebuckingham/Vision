using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed class GetCredentialSummaryQueryHandler(CredentialDbContext db)
    : IRequestHandler<GetCredentialSummaryQuery, CredentialSummaryDto>
{
    public async Task<CredentialSummaryDto> Handle(
        GetCredentialSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        var activeCount = await db.Credentials
            .CountAsync(c => c.RevokedAt == null && c.ExpiresAt > now, cancellationToken);

        var expiringSoonCount = await db.Credentials
            .CountAsync(c => c.RevokedAt == null && c.ExpiresAt > now && c.ExpiresAt <= expiringSoonThreshold, cancellationToken);

        var expiredCount = await db.Credentials
            .CountAsync(c => c.RevokedAt == null && c.ExpiresAt <= now, cancellationToken);

        var revokedCount = await db.Credentials
            .CountAsync(c => c.RevokedAt != null, cancellationToken);

        return new CredentialSummaryDto(activeCount, expiringSoonCount, expiredCount, revokedCount);
    }
}
