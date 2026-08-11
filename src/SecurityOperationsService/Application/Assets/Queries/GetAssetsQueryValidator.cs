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
            .Must(s => Enum.TryParse<SecurityAssetStatus>(s, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid asset status. Valid values: Operational, Degraded, Offline.");

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<SecurityAssetType>(t, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Type))
            .WithMessage("Invalid asset type. Valid values: Camera, AccessControlledDoor, BadgeReader, SecurityGate.");
    }
}
