using MediatR;

namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed record GetTechnicianByIdQuery(Guid Id) : IRequest<TechnicianDetailDto?>;
