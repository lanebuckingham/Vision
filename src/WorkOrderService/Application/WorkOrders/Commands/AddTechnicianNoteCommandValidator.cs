using FluentValidation;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class AddTechnicianNoteCommandValidator : AbstractValidator<AddTechnicianNoteCommand>
{
    public AddTechnicianNoteCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Note content is required.")
            .MaximumLength(2000).WithMessage("Note content must not exceed 2000 characters.");
    }
}
