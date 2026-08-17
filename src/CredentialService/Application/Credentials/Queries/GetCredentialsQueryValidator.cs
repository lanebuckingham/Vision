using FluentValidation;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed class GetCredentialsQueryValidator : AbstractValidator<GetCredentialsQuery>
{
    public GetCredentialsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(BeDefinedEnum<CredentialStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Invalid credential status. Valid values: Active, Expired, Revoked.");

        RuleFor(x => x.AccessLevel)
            .Must(BeDefinedEnum<CredentialAccessLevel>)
            .When(x => !string.IsNullOrWhiteSpace(x.AccessLevel))
            .WithMessage("Invalid access level. Valid values: General, Clinical, Restricted, Security.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
