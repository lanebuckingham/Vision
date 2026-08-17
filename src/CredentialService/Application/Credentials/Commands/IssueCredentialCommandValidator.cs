using FluentValidation;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed class IssueCredentialCommandValidator : AbstractValidator<IssueCredentialCommand>
{
    public IssueCredentialCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("Person ID is required.");

        RuleFor(x => x.CredentialNumber)
            .NotEmpty().WithMessage("Credential number is required.")
            .MaximumLength(50).WithMessage("Credential number must be 50 characters or fewer.");

        RuleFor(x => x.AccessLevel)
            .NotEmpty().WithMessage("Access level is required.")
            .Must(BeDefinedEnum<CredentialAccessLevel>)
            .WithMessage("Invalid access level. Valid values: General, Clinical, Restricted, Security.");

        RuleFor(x => x.ExpiresAt)
            .NotEmpty().WithMessage("Expiration date is required.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
