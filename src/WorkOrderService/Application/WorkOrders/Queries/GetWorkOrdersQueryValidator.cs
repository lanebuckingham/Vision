using FluentValidation;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed class GetWorkOrdersQueryValidator : AbstractValidator<GetWorkOrdersQuery>
{
    public GetWorkOrdersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(BeDefinedEnum<WorkOrderStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid work order status. Valid values: New, Assigned, InProgress, Completed.");

        RuleFor(x => x.Priority)
            .Must(BeDefinedEnum<WorkOrderPriority>)
            .When(x => !string.IsNullOrWhiteSpace(x.Priority))
            .WithMessage("Invalid work order priority. Valid values: Low, Medium, High, Critical.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
