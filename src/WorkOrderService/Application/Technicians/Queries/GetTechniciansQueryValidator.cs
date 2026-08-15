using FluentValidation;

namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed class GetTechniciansQueryValidator : AbstractValidator<GetTechniciansQuery>
{
    public GetTechniciansQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
