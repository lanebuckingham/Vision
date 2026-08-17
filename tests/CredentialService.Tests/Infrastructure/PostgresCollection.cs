namespace Vision.CredentialService.Tests.Infrastructure;

/// <summary>
/// xUnit collection definition that ensures all PostgreSQL-dependent tests
/// run sequentially against the same database fixture.
/// </summary>
[CollectionDefinition("PostgreSQL")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
