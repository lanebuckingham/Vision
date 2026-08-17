using FluentValidation;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed class RevokeCredentialCommandValidator : AbstractValidator<RevokeCredentialCommand>
{
    public RevokeCredentialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Credential ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Revocation reason is required.")
            .MaximumLength(500).WithMessage("Revocation reason must be 500 characters or fewer.");
    }
}
