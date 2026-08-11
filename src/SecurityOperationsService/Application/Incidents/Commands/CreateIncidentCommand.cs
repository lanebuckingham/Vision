using MediatR;
using Vision.SecurityOperationsService.Application.Incidents.Queries;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed record CreateIncidentCommand(
    Guid LocationId,
    Guid? SecurityAssetId,
    string Title,
    string Description,
    string Severity) : IRequest<IncidentDetailDto>;
