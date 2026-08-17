namespace Vision.WorkOrderService.Tests.Authorization;

/// <summary>
/// Authorization, validation, and health-check classes share one PostgreSQL database
/// (vision_wo_auth_test). Each class starts a WebApplicationFactory that migrates and
/// seeds on startup. Placing them in one xUnit collection prevents parallel MigrateAsync
/// / SeedAsync races on that shared schema.
/// </summary>
[CollectionDefinition("WorkOrderAuth", DisableParallelization = true)]
public class WorkOrderAuthCollection;
