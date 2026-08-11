using MediatR;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

public sealed record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDetailDto?>;
