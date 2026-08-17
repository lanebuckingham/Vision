using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Tests.Infrastructure;

[Collection("PostgreSQL")]
public class PersistenceConstraintTests(PostgresFixture fixture)
{

    [Fact]
    public async Task CredentialNumber_MustBeUnique()
    {
        await using var db = fixture.CreateDbContext();

        var person = CreatePerson();
        db.People.Add(person);
        await db.SaveChangesAsync();

        var cred1 = CreateCredential(person.Id, "UNIQUE-001");
        db.Credentials.Add(cred1);
        await db.SaveChangesAsync();

        var cred2 = CreateCredential(person.Id, "UNIQUE-001");
        db.Credentials.Add(cred2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EmployeeNumber_MustBeUniqueWhenNotNull()
    {
        await using var db = fixture.CreateDbContext();

        var person1 = CreatePerson(employeeNumber: "EMP-DUPE");
        var person2 = CreatePerson(employeeNumber: "EMP-DUPE");

        db.People.AddRange(person1, person2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EmployeeNumber_AllowsMultipleNullValues()
    {
        await using var db = fixture.CreateDbContext();

        var person1 = CreatePerson(employeeNumber: null);
        var person2 = CreatePerson(employeeNumber: null);

        db.People.AddRange(person1, person2);
        await db.SaveChangesAsync();

        Assert.True(await db.People.CountAsync() >= 2);
    }

    [Fact]
    public async Task PersonDeletion_RestrictedWhileCredentialsExist()
    {
        await using var dbSetup = fixture.CreateDbContext();

        var person = CreatePerson();
        dbSetup.People.Add(person);
        await dbSetup.SaveChangesAsync();

        var cred = CreateCredential(person.Id, "RESTRICT-001");
        dbSetup.Credentials.Add(cred);
        await dbSetup.SaveChangesAsync();

        // Fresh context — no tracked relationships
        await using var dbDelete = fixture.CreateDbContext();
        var personToDelete = await dbDelete.People.FindAsync(person.Id);
        Assert.NotNull(personToDelete);
        dbDelete.People.Remove(personToDelete!);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbDelete.SaveChangesAsync());
    }

    [Fact]
    public async Task CredentialStatus_IsNotPersistedAsColumn()
    {
        await using var db = fixture.CreateDbContext();

        // Use ADO.NET to check column existence
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'credentials' AND table_name = 'credentials'";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.DoesNotContain("status", columns);
    }

    [Fact]
    public async Task EnumValues_PersistedAsStrings()
    {
        await using var db = fixture.CreateDbContext();

        var person = CreatePerson(personType: PersonType.Contractor);
        db.People.Add(person);
        await db.SaveChangesAsync();

        var cred = CreateCredential(person.Id, "ENUM-001", CredentialAccessLevel.Security);
        db.Credentials.Add(cred);
        await db.SaveChangesAsync();

        // Use ADO.NET to read raw values
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = $"SELECT person_type FROM credentials.people WHERE id = '{person.Id}'";
        var personTypeValue = (string)(await cmd1.ExecuteScalarAsync())!;
        Assert.Equal("Contractor", personTypeValue);

        await using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = $"SELECT access_level FROM credentials.credentials WHERE id = '{cred.Id}'";
        var accessLevelValue = (string)(await cmd2.ExecuteScalarAsync())!;
        Assert.Equal("Security", accessLevelValue);
    }

    [Fact]
    public async Task CredentialsSchema_IsUsed()
    {
        await using var db = fixture.CreateDbContext();
        var schema = db.Model.GetDefaultSchema();

        Assert.NotNull(schema);
        Assert.Equal("credentials", schema);

        // Verify tables exist in the schema via ADO.NET
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'credentials'";
        var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        Assert.True(tableCount >= 2); // people + credentials at minimum
    }

    private static Person CreatePerson(string? employeeNumber = null, PersonType personType = PersonType.Employee) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = $"Person-{Guid.NewGuid():N}"[..20],
        PersonType = personType,
        IsActive = true,
        EmployeeNumber = employeeNumber,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Credential CreateCredential(Guid personId, string number, CredentialAccessLevel level = CredentialAccessLevel.General) => new()
    {
        Id = Guid.NewGuid(),
        PersonId = personId,
        CredentialNumber = number,
        AccessLevel = level,
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(335),
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
    };
}
