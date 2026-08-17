namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// All application/API test classes share one PostgreSQL database
/// (vision_secops_app_test) and each resets it at InitializeAsync. Placing them in
/// the same xUnit collection prevents xUnit from running these test classes in
/// parallel with each other, which would otherwise race on the shared schema.
/// </summary>
[CollectionDefinition("SecurityOperationsApplication", DisableParallelization = true)]
public class SecurityOperationsApplicationCollection;
