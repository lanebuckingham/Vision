using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

public static class WorkOrderSeeder
{
    public static async Task SeedAsync(WorkOrderDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Technicians.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;

        var technicians = CreateTechnicians(now);
        var workOrders = CreateWorkOrders(now);

        context.Technicians.AddRange(technicians);
        context.WorkOrders.AddRange(workOrders);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<Technician> CreateTechnicians(DateTimeOffset now) =>
    [
        new()
        {
            Id = SeedDataIds.TechMarcusJohnson,
            DisplayName = "Marcus Johnson",
            Email = "marcus.johnson@northstarmedical.com",
            IsActive = true,
            Specialty = "Camera & Video Systems",
            CreatedAt = now.AddDays(-90)
        },
        new()
        {
            Id = SeedDataIds.TechSarahChen,
            DisplayName = "Sarah Chen",
            Email = "sarah.chen@northstarmedical.com",
            IsActive = true,
            Specialty = "Access Control & Door Systems",
            CreatedAt = now.AddDays(-90)
        },
        new()
        {
            Id = SeedDataIds.TechDavidPark,
            DisplayName = "David Park",
            Email = "david.park@northstarmedical.com",
            IsActive = true,
            Specialty = "Network & Badge Readers",
            CreatedAt = now.AddDays(-60)
        },
        new()
        {
            Id = SeedDataIds.TechLisaReeves,
            DisplayName = "Lisa Reeves",
            Email = "lisa.reeves@northstarmedical.com",
            IsActive = true,
            Specialty = "Gate & Perimeter Systems",
            CreatedAt = now.AddDays(-45)
        }
    ];

    private static List<WorkOrder> CreateWorkOrders(DateTimeOffset now)
    {
        var sevenDaysAgo = now.AddDays(-7);
        var fiveDaysAgo = now.AddDays(-5);
        var threeDaysAgo = now.AddDays(-3);

        return
        [
            // Completed work order — historical context (linked to resolved gate incident)
            new WorkOrder
            {
                Id = SeedDataIds.WorkOrderCompleted,
                SecurityAssetId = SeedDataIds.MainLobbyGate,
                Title = "Main lobby gate sensor recalibration",
                Description = "Security gate sensor not detecting badge tap reliably. Requires calibration and firmware update.",
                Priority = WorkOrderPriority.Low,
                Status = WorkOrderStatus.Completed,
                AssignedTechnicianId = SeedDataIds.TechLisaReeves,
                AssetNameSnapshot = "LOBBY Gate 06",
                LocationNameSnapshot = "Main Lobby",
                AssignedAt = sevenDaysAgo,
                StartedAt = sevenDaysAgo.AddHours(2),
                CompletedAt = sevenDaysAgo.AddHours(4),
                CompletionSummary = "Sensor recalibrated and firmware updated to v3.2.1. Gate operating normally on all test passes.",
                CreatedAt = sevenDaysAgo,
                UpdatedAt = sevenDaysAgo.AddHours(4),
                Notes =
                [
                    new TechnicianNote
                    {
                        Id = SeedDataIds.NoteCompletedWorkOrder,
                        WorkOrderId = SeedDataIds.WorkOrderCompleted,
                        TechnicianId = SeedDataIds.TechLisaReeves,
                        Content = "Firmware was 2 versions behind. Updated and recalibrated proximity sensor.",
                        CreatedAt = sevenDaysAgo.AddHours(3)
                    }
                ]
            },
            // In Progress work order — linked to Data Center camera incident
            new WorkOrder
            {
                Id = SeedDataIds.WorkOrderInProgress,
                SecurityAssetId = SeedDataIds.DataCenterCamera,
                SecurityIncidentId = SeedDataIds.DataCenterCameraIncident,
                Title = "Data center entrance camera intermittent feed repair",
                Description = "Camera feed dropping intermittently during overnight hours. Investigate power and network connection.",
                Priority = WorkOrderPriority.High,
                Status = WorkOrderStatus.InProgress,
                AssignedTechnicianId = SeedDataIds.TechMarcusJohnson,
                AssetNameSnapshot = "DC-ENT Camera 01",
                LocationNameSnapshot = "Data Center Entrance",
                AssignedAt = fiveDaysAgo,
                StartedAt = threeDaysAgo,
                CreatedAt = fiveDaysAgo,
                UpdatedAt = threeDaysAgo,
                Notes =
                [
                    new TechnicianNote
                    {
                        Id = SeedDataIds.NoteInProgressWorkOrder,
                        WorkOrderId = SeedDataIds.WorkOrderInProgress,
                        TechnicianId = SeedDataIds.TechMarcusJohnson,
                        Content = "Initial inspection shows intermittent power fluctuation at the PoE switch port. Testing alternate port.",
                        CreatedAt = threeDaysAgo.AddHours(1)
                    }
                ]
            },
            // Assigned work order — linked to Admin badge reader incident
            new WorkOrder
            {
                Id = SeedDataIds.WorkOrderAssigned,
                SecurityAssetId = SeedDataIds.AdminBadgeReader,
                SecurityIncidentId = SeedDataIds.AdminBadgeReaderIncident,
                Title = "Administration lobby badge reader slow response",
                Description = "Badge reader taking 3-5 seconds to respond. Staff reporting delays during morning rush hours.",
                Priority = WorkOrderPriority.Medium,
                Status = WorkOrderStatus.Assigned,
                AssignedTechnicianId = SeedDataIds.TechDavidPark,
                AssetNameSnapshot = "ADMIN Reader 04",
                LocationNameSnapshot = "Administration Lobby",
                AssignedAt = now.AddDays(-1),
                CreatedAt = threeDaysAgo,
                UpdatedAt = now.AddDays(-1)
            }
        ];
    }
}
