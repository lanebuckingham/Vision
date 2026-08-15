using MediatR;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed record AddTechnicianNoteCommand(
    Guid WorkOrderId,
    string Content) : IRequest<TechnicianNoteDto>;
