using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed class GetTechnicianByIdQueryHandler(WorkOrderDbContext db)
    : IRequestHandler<GetTechnicianByIdQuery, TechnicianDetailDto?>
{
    public async Task<TechnicianDetailDto?> Handle(
        GetTechnicianByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Technicians
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new TechnicianDetailDto(
                t.Id,
                t.DisplayName,
                t.Email,
                t.Specialty,
                t.IsActive,
                t.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
