using MediatR;

namespace Vision.SecurityOperationsService.Application.Dashboard.Queries;

public sealed record GetSecurityDashboardQuery : IRequest<SecurityDashboardDto>;
