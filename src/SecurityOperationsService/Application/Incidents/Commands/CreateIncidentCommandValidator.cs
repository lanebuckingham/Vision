using FluentValidation;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.LocationId)
            .NotEmpty().WithMessage("Location is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(150).WithMessage("Title must not exceed 150 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required.")
            .Must(BeDefinedEnum<IncidentSeverity>)
            .WithMessage("Invalid severity. Valid values: Low, Medium, High, Critical.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
