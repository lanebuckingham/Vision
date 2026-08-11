using FluentValidation;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

public sealed class GetIncidentsQueryValidator : AbstractValidator<GetIncidentsQuery>
{
    public GetIncidentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<IncidentStatus>(s, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid incident status. Valid values: Open, Investigating, Resolved.");

        RuleFor(x => x.Severity)
            .Must(s => Enum.TryParse<IncidentSeverity>(s, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Severity))
            .WithMessage("Invalid incident severity. Valid values: Low, Medium, High, Critical.");
    }
}
