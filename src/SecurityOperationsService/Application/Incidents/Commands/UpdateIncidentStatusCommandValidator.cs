using FluentValidation;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed class UpdateIncidentStatusCommandValidator : AbstractValidator<UpdateIncidentStatusCommand>
{
    public UpdateIncidentStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Incident ID is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => Enum.TryParse<IncidentStatus>(s, ignoreCase: true, out _))
            .WithMessage("Invalid status. Valid values: Open, Investigating, Resolved.");

        RuleFor(x => x.ResolutionSummary)
            .NotEmpty().WithMessage("Resolution summary is required when resolving an incident.")
            .When(x => string.Equals(x.Status, "Resolved", StringComparison.OrdinalIgnoreCase));
    }
}
