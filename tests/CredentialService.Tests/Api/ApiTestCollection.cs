namespace Vision.CredentialService.Tests.Api;

/// <summary>
/// Collection definition to ensure API test classes run sequentially.
/// Prevents TestIdentityStore interference between test classes.
/// </summary>
[CollectionDefinition("ApiTests", DisableParallelization = true)]
public class ApiTestCollection
{
}
