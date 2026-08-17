namespace Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Deterministic GUIDs for seed data. Stable IDs ensure idempotent seeding
/// and enable cross-service display references.
/// </summary>
public static class SeedDataIds
{
    // Technicians
    public static readonly Guid TechMarcusJohnson = new("a1a2b3c4-1001-4eee-a101-100000000001");
    public static readonly Guid TechSarahChen = new("a1a2b3c4-1002-4eee-a102-100000000002");
    public static readonly Guid TechDavidPark = new("a1a2b3c4-1003-4eee-a103-100000000003");
    public static readonly Guid TechLisaReeves = new("a1a2b3c4-1004-4eee-a104-100000000004");

    // Cognito subject IDs for Technician mapping.
    // These must match the 'sub' claim of the corresponding Cognito user.
    // See docs/development/cognito-setup.md for provisioning instructions.
    public const string CognitoSubTechMarcus = "cognito-tech-marcus-johnson";
    public const string CognitoSubTechSarah = "cognito-tech-sarah-chen";
    public const string CognitoSubTechDavid = "cognito-tech-david-park";
    public const string CognitoSubTechLisa = "cognito-tech-lisa-reeves";

    // Work Orders
    public static readonly Guid WorkOrderCompleted = new("b1a2b3c4-2001-4fff-a201-200000000001");
    public static readonly Guid WorkOrderInProgress = new("b1a2b3c4-2002-4fff-a202-200000000002");
    public static readonly Guid WorkOrderAssigned = new("b1a2b3c4-2003-4fff-a203-200000000003");
    public static readonly Guid WorkOrderNew = new("b1a2b3c4-2004-4fff-a204-200000000004");

    // Technician Notes
    public static readonly Guid NoteCompletedWorkOrder = new("c1a2b3c4-3001-4aaa-a301-300000000001");
    public static readonly Guid NoteInProgressWorkOrder = new("c1a2b3c4-3002-4aaa-a302-300000000002");

    // Cross-service references (from SecurityOperationsService seed)
    public static readonly Guid PharmacyStorageCamera02 = new("99750ccc-976b-49ee-a485-f3677b9b91ef");
    public static readonly Guid DataCenterCamera = new("e1a2b3c4-8001-4ddd-a111-111111111111");
    public static readonly Guid MainLobbyGate = new("e1a2b3c4-2006-4ddd-a111-111111111111");
    public static readonly Guid AdminBadgeReader = new("e1a2b3c4-6004-4ddd-a111-111111111111");
    public static readonly Guid EmergencyDepartmentDoor = new("e1a2b3c4-3002-4ddd-a111-111111111111");

    // Cross-service incident references
    public static readonly Guid DataCenterCameraIncident = new("3f896236-5741-44d2-ac41-34aa2acb5b68");
    public static readonly Guid AdminBadgeReaderIncident = new("4fa97347-6852-45e3-bd52-45bb3bdc6c79");
}
