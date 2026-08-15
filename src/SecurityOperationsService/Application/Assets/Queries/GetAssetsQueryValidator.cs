using FluentValidation;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Application.Assets.Queries;

public sealed class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
    public GetAssetsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(BeDefinedEnum<SecurityAssetStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid asset status. Valid values: Operational, Degraded, Offline.");

        RuleFor(x => x.Type)
            .Must(BeDefinedEnum<SecurityAssetType>)
            .When(x => !string.IsNullOrWhiteSpace(x.Type))
            .WithMessage("Invalid asset type. Valid values: Camera, AccessControlledDoor, BadgeReader, SecurityGate.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
