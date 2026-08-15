using FluentValidation;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class AssignTechnicianCommandValidator : AbstractValidator<AssignTechnicianCommand>
{
    public AssignTechnicianCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order ID is required.");

        RuleFor(x => x.TechnicianId)
            .NotEmpty().WithMessage("Technician ID is required.");
    }
}
