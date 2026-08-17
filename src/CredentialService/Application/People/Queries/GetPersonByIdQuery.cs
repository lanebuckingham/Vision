using MediatR;

namespace Vision.CredentialService.Application.People.Queries;

public sealed record GetPersonByIdQuery(Guid Id) : IRequest<PersonDetailDto?>;
