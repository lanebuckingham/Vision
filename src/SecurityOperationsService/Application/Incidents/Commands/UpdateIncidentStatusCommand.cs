using MediatR;
using Vision.SecurityOperationsService.Application.Incidents.Queries;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed record UpdateIncidentStatusCommand(
    Guid Id,
    string Status,
    string? ResolutionSummary) : IRequest<IncidentDetailDto>;
