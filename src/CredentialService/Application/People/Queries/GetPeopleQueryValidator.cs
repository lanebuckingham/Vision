using FluentValidation;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Application.People.Queries;

public sealed class GetPeopleQueryValidator : AbstractValidator<GetPeopleQuery>
{
    public GetPeopleQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.PersonType)
            .Must(BeDefinedEnum<PersonType>)
            .When(x => !string.IsNullOrWhiteSpace(x.PersonType))
            .WithMessage("Invalid person type. Valid values: Employee, Contractor.");
    }

    private static bool BeDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
