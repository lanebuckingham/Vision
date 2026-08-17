using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.API.Auth;

/// <summary>
/// Resolves the authenticated Technician business identity from the Cognito JWT subject.
/// Returns null if no matching Technician.CognitoSubject exists.
/// </summary>
public static class TechnicianIdentityResolver
{
    public static async Task<Guid?> ResolveTechnicianIdAsync(
        ClaimsPrincipal user,
        WorkOrderDbContext db,
        CancellationToken cancellationToken = default)
    {
        var subject = user.FindFirstValue("sub")
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var technicianId = await db.Technicians
            .AsNoTracking()
            .Where(t => t.CognitoSubject == subject)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return technicianId;
    }
}
