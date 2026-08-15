using FluentValidation;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Application.Assets.Commands;

public sealed class UpdateAssetStatusCommandValidator : AbstractValidator<UpdateAssetStatusCommand>
{
    public UpdateAssetStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Asset ID is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(BeDefinedEnum<SecurityAssetStatus>)
            .WithMessage("Invalid asset status. Valid values: Operational, Degraded, Offline.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
