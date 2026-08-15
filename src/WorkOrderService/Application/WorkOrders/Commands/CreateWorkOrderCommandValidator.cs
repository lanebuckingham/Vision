using FluentValidation;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.SecurityAssetId)
            .NotEmpty().WithMessage("Security asset ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(150).WithMessage("Title must not exceed 150 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(BeDefinedEnum<WorkOrderPriority>)
            .WithMessage("Invalid priority. Valid values: Low, Medium, High, Critical.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
