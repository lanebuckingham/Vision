using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

public static class SecurityOperationsSeeder
{
    public static async Task SeedAsync(SecurityOperationsDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Hospitals.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;

        var hospital = CreateHospital(now);
        var buildings = CreateBuildings(hospital.Id, now);
        var locations = CreateLocations(buildings, now);
        var assets = CreateAssets(locations, now);
        var incidents = CreateIncidents(now);

        context.Hospitals.Add(hospital);
        context.Buildings.AddRange(buildings);
        context.Locations.AddRange(locations);
        context.SecurityAssets.AddRange(assets);
        context.SecurityIncidents.AddRange(incidents);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Hospital CreateHospital(DateTimeOffset now) => new()
    {
        Id = SeedDataIds.NorthstarMedicalCenter,
        Name = "Northstar Medical Center",
        Code = "NMC",
        CreatedAt = now
    };

    private static List<Building> CreateBuildings(Guid hospitalId, DateTimeOffset now) =>
    [
        new() { Id = SeedDataIds.MainHospital, HospitalId = hospitalId, Name = "Main Hospital", CreatedAt = now },
        new() { Id = SeedDataIds.AdministrativeBuilding, HospitalId = hospitalId, Name = "Administrative Building", CreatedAt = now },
        new() { Id = SeedDataIds.DataCenter, HospitalId = hospitalId, Name = "Data Center", CreatedAt = now }
    ];

    private static List<Location> CreateLocations(List<Building> buildings, DateTimeOffset now)
    {
        var mainHospitalId = SeedDataIds.MainHospital;
        var adminId = SeedDataIds.AdministrativeBuilding;
        var dataCenterId = SeedDataIds.DataCenter;

        return
        [
            new() { Id = SeedDataIds.MainLobby, BuildingId = mainHospitalId, Name = "Main Lobby", Floor = "1", Department = "General", CreatedAt = now },
            new() { Id = SeedDataIds.EmergencyDeptEntrance, BuildingId = mainHospitalId, Name = "Emergency Department Entrance", Floor = "1", Department = "Emergency", CreatedAt = now },
            new() { Id = SeedDataIds.PharmacyStorage, BuildingId = mainHospitalId, Name = "Pharmacy Storage", Floor = "1", Department = "Pharmacy", CreatedAt = now },
            new() { Id = SeedDataIds.IcuEastCorridor, BuildingId = mainHospitalId, Name = "ICU East Corridor", Floor = "2", Department = "ICU", CreatedAt = now },
            new() { Id = SeedDataIds.SurgicalWingStaffEntrance, BuildingId = mainHospitalId, Name = "Surgical Wing Staff Entrance", Floor = "2", Department = "Surgery", CreatedAt = now },
            new() { Id = SeedDataIds.AdministrationLobby, BuildingId = adminId, Name = "Administration Lobby", Floor = "1", CreatedAt = now },
            new() { Id = SeedDataIds.RecordsStorageEntrance, BuildingId = adminId, Name = "Records Storage Entrance", Floor = "B1", Department = "Records", CreatedAt = now },
            new() { Id = SeedDataIds.DataCenterEntrance, BuildingId = dataCenterId, Name = "Data Center Entrance", Floor = "1", CreatedAt = now },
            new() { Id = SeedDataIds.ServerRoomCorridor, BuildingId = dataCenterId, Name = "Server Room Corridor", Floor = "1", CreatedAt = now }
        ];
    }

    private static List<SecurityAsset> CreateAssets(List<Location> locations, DateTimeOffset now)
    {
        var assets = new List<SecurityAsset>();
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        // Pharmacy Storage — includes the critical demo camera
        assets.Add(new SecurityAsset
        {
            Id = new Guid("e1a2b3c4-1111-4ddd-a111-111111111101"),
            LocationId = SeedDataIds.PharmacyStorage, Name = "Pharmacy Storage Camera 01",
            AssetTag = "CAM-PHARM-001", AssetType = SecurityAssetType.Camera,
            Status = SecurityAssetStatus.Operational, Manufacturer = "Axis", Model = "P3245-V",
            LastServiceAt = thirtyDaysAgo, CreatedAt = sixtyDaysAgo, UpdatedAt = thirtyDaysAgo
        });
        assets.Add(new SecurityAsset
        {
            Id = SeedDataIds.PharmacyStorageCamera02,
            LocationId = SeedDataIds.PharmacyStorage, Name = "Pharmacy Storage Camera 02",
            AssetTag = "CAM-PHARM-002", AssetType = SecurityAssetType.Camera,
            Status = SecurityAssetStatus.Offline, Manufacturer = "Axis", Model = "P3245-V",
            LastServiceAt = thirtyDaysAgo, StatusChangedAt = now.AddHours(-2),
            CreatedAt = sixtyDaysAgo, UpdatedAt = now.AddHours(-2)
        });
        assets.Add(new SecurityAsset
        {
            Id = new Guid("e1a2b3c4-1111-4ddd-a111-111111111103"),
            LocationId = SeedDataIds.PharmacyStorage, Name = "Pharmacy Storage Door",
            AssetTag = "DOOR-PHARM-001", AssetType = SecurityAssetType.AccessControlledDoor,
            Status = SecurityAssetStatus.Operational, Manufacturer = "HID", Model = "iCLASS SE",
            LastServiceAt = thirtyDaysAgo, CreatedAt = sixtyDaysAgo, UpdatedAt = thirtyDaysAgo
        });
        assets.Add(new SecurityAsset
        {
            Id = new Guid("e1a2b3c4-1111-4ddd-a111-111111111104"),
            LocationId = SeedDataIds.PharmacyStorage, Name = "Pharmacy Badge Reader",
            AssetTag = "RDR-PHARM-001", AssetType = SecurityAssetType.BadgeReader,
            Status = SecurityAssetStatus.Operational, Manufacturer = "HID", Model = "Signo 40",
            LastServiceAt = thirtyDaysAgo, CreatedAt = sixtyDaysAgo, UpdatedAt = thirtyDaysAgo
        });

        // Main Lobby
        assets.AddRange(CreateLocationAssets(SeedDataIds.MainLobby, "LOBBY", 3, 1, 1, 1, now, sixtyDaysAgo, thirtyDaysAgo, 0x2000));

        // Emergency Department Entrance
        assets.AddRange(CreateLocationAssets(SeedDataIds.EmergencyDeptEntrance, "ER", 3, 2, 2, 1, now, sixtyDaysAgo, thirtyDaysAgo, 0x3000));

        // ICU East Corridor
        assets.AddRange(CreateLocationAssets(SeedDataIds.IcuEastCorridor, "ICU", 3, 2, 1, 0, now, sixtyDaysAgo, thirtyDaysAgo, 0x4000));

        // Surgical Wing Staff Entrance
        assets.AddRange(CreateLocationAssets(SeedDataIds.SurgicalWingStaffEntrance, "SURG", 2, 1, 2, 1, now, sixtyDaysAgo, thirtyDaysAgo, 0x5000));

        // Administration Lobby
        assets.AddRange(CreateLocationAssets(SeedDataIds.AdministrationLobby, "ADMIN", 2, 1, 1, 0, now, sixtyDaysAgo, thirtyDaysAgo, 0x6000));

        // Records Storage Entrance
        assets.AddRange(CreateLocationAssets(SeedDataIds.RecordsStorageEntrance, "REC", 1, 1, 1, 0, now, sixtyDaysAgo, thirtyDaysAgo, 0x7000));

        // Data Center Entrance
        assets.AddRange(CreateLocationAssets(SeedDataIds.DataCenterEntrance, "DC-ENT", 3, 1, 2, 1, now, sixtyDaysAgo, thirtyDaysAgo, 0x8000));

        // Server Room Corridor
        assets.AddRange(CreateLocationAssets(SeedDataIds.ServerRoomCorridor, "DC-SRV", 3, 2, 2, 1, now, sixtyDaysAgo, thirtyDaysAgo, 0x9000));

        // Add a few degraded assets
        assets[8].Status = SecurityAssetStatus.Degraded; // one in ER
        assets[8].StatusChangedAt = sevenDaysAgo;
        assets[8].UpdatedAt = sevenDaysAgo;

        assets[20].Status = SecurityAssetStatus.Degraded; // one in Admin
        assets[20].StatusChangedAt = sevenDaysAgo;
        assets[20].UpdatedAt = sevenDaysAgo;

        assets[30].Status = SecurityAssetStatus.Offline; // one in Data Center
        assets[30].StatusChangedAt = now.AddDays(-1);
        assets[30].UpdatedAt = now.AddDays(-1);

        return assets;
    }

    private static List<SecurityAsset> CreateLocationAssets(
        Guid locationId, string prefix,
        int cameras, int doors, int readers, int gates,
        DateTimeOffset now, DateTimeOffset created, DateTimeOffset lastService,
        int guidOffset)
    {
        var assets = new List<SecurityAsset>();
        var counter = 1;

        for (var i = 0; i < cameras; i++)
        {
            assets.Add(new SecurityAsset
            {
                Id = new Guid($"e1a2b3c4-{guidOffset + counter:x4}-4ddd-a111-111111111111"),
                LocationId = locationId, Name = $"{prefix} Camera {counter:D2}",
                AssetTag = $"CAM-{prefix}-{counter:D3}", AssetType = SecurityAssetType.Camera,
                Status = SecurityAssetStatus.Operational, Manufacturer = "Axis", Model = "P3245-V",
                LastServiceAt = lastService, CreatedAt = created, UpdatedAt = lastService
            });
            counter++;
        }
        for (var i = 0; i < doors; i++)
        {
            assets.Add(new SecurityAsset
            {
                Id = new Guid($"e1a2b3c4-{guidOffset + counter:x4}-4ddd-a111-111111111111"),
                LocationId = locationId, Name = $"{prefix} Door {counter:D2}",
                AssetTag = $"DOOR-{prefix}-{counter:D3}", AssetType = SecurityAssetType.AccessControlledDoor,
                Status = SecurityAssetStatus.Operational, Manufacturer = "HID", Model = "iCLASS SE",
                LastServiceAt = lastService, CreatedAt = created, UpdatedAt = lastService
            });
            counter++;
        }
        for (var i = 0; i < readers; i++)
        {
            assets.Add(new SecurityAsset
            {
                Id = new Guid($"e1a2b3c4-{guidOffset + counter:x4}-4ddd-a111-111111111111"),
                LocationId = locationId, Name = $"{prefix} Reader {counter:D2}",
                AssetTag = $"RDR-{prefix}-{counter:D3}", AssetType = SecurityAssetType.BadgeReader,
                Status = SecurityAssetStatus.Operational, Manufacturer = "HID", Model = "Signo 40",
                LastServiceAt = lastService, CreatedAt = created, UpdatedAt = lastService
            });
            counter++;
        }
        for (var i = 0; i < gates; i++)
        {
            assets.Add(new SecurityAsset
            {
                Id = new Guid($"e1a2b3c4-{guidOffset + counter:x4}-4ddd-a111-111111111111"),
                LocationId = locationId, Name = $"{prefix} Gate {counter:D2}",
                AssetTag = $"GATE-{prefix}-{counter:D3}", AssetType = SecurityAssetType.SecurityGate,
                Status = SecurityAssetStatus.Operational, Manufacturer = "Boon Edam", Model = "Tourlock 180",
                LastServiceAt = lastService, CreatedAt = created, UpdatedAt = lastService
            });
            counter++;
        }

        return assets;
    }

    private static List<SecurityIncident> CreateIncidents(DateTimeOffset now)
    {
        var twoHoursAgo = now.AddHours(-2);
        var oneDayAgo = now.AddDays(-1);
        var threeDaysAgo = now.AddDays(-3);
        var sevenDaysAgo = now.AddDays(-7);

        return
        [
            // Critical demo incident — Pharmacy camera offline
            new SecurityIncident
            {
                Id = SeedDataIds.PharmacyCameraIncident,
                LocationId = SeedDataIds.PharmacyStorage,
                SecurityAssetId = SeedDataIds.PharmacyStorageCamera02,
                Title = "Pharmacy storage camera offline",
                Description = "Camera stopped responding and is not producing video. Pharmacy storage area has no visual coverage.",
                Severity = IncidentSeverity.Critical,
                Status = IncidentStatus.Open,
                CreatedAt = twoHoursAgo,
                UpdatedAt = twoHoursAgo
            },
            // High severity — investigating
            new SecurityIncident
            {
                Id = SeedDataIds.DataCenterCameraIncident,
                LocationId = SeedDataIds.DataCenterEntrance,
                SecurityAssetId = SeedDataIds.DataCenterCamera01,
                Title = "Data center entrance camera intermittent",
                Description = "Camera feed dropping intermittently. Image quality degraded during overnight hours.",
                Severity = IncidentSeverity.High,
                Status = IncidentStatus.Investigating,
                CreatedAt = oneDayAgo,
                UpdatedAt = oneDayAgo.AddHours(4)
            },
            // Medium severity — open
            new SecurityIncident
            {
                Id = SeedDataIds.AdminBadgeReaderIncident,
                LocationId = SeedDataIds.AdministrationLobby,
                SecurityAssetId = SeedDataIds.AdminBadgeReader,
                Title = "Administration lobby badge reader slow response",
                Description = "Badge reader taking 3-5 seconds to respond. Staff reporting delays during morning rush.",
                Severity = IncidentSeverity.Medium,
                Status = IncidentStatus.Open,
                CreatedAt = threeDaysAgo,
                UpdatedAt = threeDaysAgo
            },
            // Low severity — resolved
            new SecurityIncident
            {
                Id = SeedDataIds.MainLobbyGateIncident,
                LocationId = SeedDataIds.MainLobby,
                SecurityAssetId = SeedDataIds.MainLobbyGate,
                Title = "Main lobby gate sensor calibration needed",
                Description = "Security gate occasionally not detecting badge tap on first attempt.",
                Severity = IncidentSeverity.Low,
                Status = IncidentStatus.Resolved,
                ResolvedAt = sevenDaysAgo.AddDays(1),
                ResolutionSummary = "Sensor recalibrated and firmware updated. Gate operating normally.",
                CreatedAt = sevenDaysAgo,
                UpdatedAt = sevenDaysAgo.AddDays(1)
            }
        ];
    }
}
