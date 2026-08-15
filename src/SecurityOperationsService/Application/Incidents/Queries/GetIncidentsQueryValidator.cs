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
            .Must(BeDefinedEnum<IncidentStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid incident status. Valid values: Open, Investigating, Resolved.");

        RuleFor(x => x.Severity)
            .Must(BeDefinedEnum<IncidentSeverity>)
            .When(x => !string.IsNullOrWhiteSpace(x.Severity))
            .WithMessage("Invalid incident severity. Valid values: Low, Medium, High, Critical.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
