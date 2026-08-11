namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Deterministic GUIDs for seed data. Using stable IDs ensures seeding is idempotent
/// and enables cross-service references (e.g., WorkOrderService referencing an asset ID).
/// </summary>
public static class SeedDataIds
{
    // Hospital
    public static readonly Guid NorthstarMedicalCenter = new("d71c9475-fdb1-4d78-aa12-f9849de39dc2");

    // Buildings
    public static readonly Guid MainHospital = new("9ca90164-c910-44f6-98f0-142058ffdf1b");
    public static readonly Guid AdministrativeBuilding = new("b2e4f8a1-3c7d-4e5f-9a1b-2c3d4e5f6a7b");
    public static readonly Guid DataCenter = new("c3f5a9b2-4d8e-5f60-ab2c-3d4e5f6a7b8c");

    // Locations — Main Hospital
    public static readonly Guid MainLobby = new("a1b2c3d4-1111-4aaa-b111-111111111111");
    public static readonly Guid EmergencyDeptEntrance = new("a1b2c3d4-2222-4aaa-b222-222222222222");
    public static readonly Guid PharmacyStorage = new("72533c8e-5541-48bd-8821-8ae4c434634f");
    public static readonly Guid IcuEastCorridor = new("a1b2c3d4-4444-4aaa-b444-444444444444");
    public static readonly Guid SurgicalWingStaffEntrance = new("a1b2c3d4-5555-4aaa-b555-555555555555");

    // Locations — Administrative Building
    public static readonly Guid AdministrationLobby = new("a1b2c3d4-6666-4aaa-b666-666666666666");
    public static readonly Guid RecordsStorageEntrance = new("a1b2c3d4-7777-4aaa-b777-777777777777");

    // Locations — Data Center
    public static readonly Guid DataCenterEntrance = new("a1b2c3d4-8888-4aaa-b888-888888888888");
    public static readonly Guid ServerRoomCorridor = new("a1b2c3d4-9999-4aaa-b999-999999999999");

    // Key demo assets (deterministic IDs based on location prefix + counter)
    public static readonly Guid PharmacyStorageCamera02 = new("99750ccc-976b-49ee-a485-f3677b9b91ef");
    public static readonly Guid MainLobbyGate = new("e1a2b3c4-2006-4ddd-a111-111111111111");
    public static readonly Guid DataCenterCamera01 = new("e1a2b3c4-8001-4ddd-a111-111111111111");
    public static readonly Guid AdminBadgeReader = new("e1a2b3c4-6004-4ddd-a111-111111111111");

    // Key demo incidents
    public static readonly Guid PharmacyCameraIncident = new("2f785125-4630-43c1-ab30-239919cb4a57");
    public static readonly Guid DataCenterCameraIncident = new("3f896236-5741-44d2-ac41-34aa2acb5b68");
    public static readonly Guid AdminBadgeReaderIncident = new("4fa97347-6852-45e3-bd52-45bb3bdc6c79");
    public static readonly Guid MainLobbyGateIncident = new("5ab08458-7963-46f4-ce63-56cc4ced7d8a");
}
