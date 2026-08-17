using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Application.People.Queries;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Tests.Infrastructure;

namespace Vision.CredentialService.Tests.Application;

[Collection("PostgreSQL")]
public class PeopleQueryTests(PostgresFixture fixture)
{
    

    
    

    [Fact]
    public async Task GetPeople_ReturnsPaginatedResults()
    {
        await using var db = fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (int i = 0; i < 5; i++)
        {
            db.People.Add(new Person
            {
                Id = Guid.NewGuid(), FirstName = $"Page{tag}", LastName = $"Last{i}",
                PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, null, $"Page{tag}", 1, 3), CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task GetPeople_FiltersByPersonType()
    {
        await using var db = fixture.CreateDbContext();
        await SeedMixedPeople(db);

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery("Contractor", null, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, p => Assert.Equal("Contractor", p.PersonType));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetPeople_FiltersByIsActive()
    {
        await using var db = fixture.CreateDbContext();
        await SeedMixedPeople(db);

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, false, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, p => Assert.False(p.IsActive));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetPeople_FiltersByDepartment()
    {
        await using var db = fixture.CreateDbContext();
        await SeedMixedPeople(db);

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, "Surgery", null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, p => Assert.Equal("Surgery", p.Department));
    }

    [Fact]
    public async Task GetPeople_SearchByLastName()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Uniquename", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, null, "Uniquename", 1, 25), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Alice", result.Items[0].FirstName);
    }

    [Fact]
    public async Task GetPeople_SearchByEmployeeNumber()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Bob", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-SEARCH-99", CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, null, "EMP-SEARCH-99", 1, 25), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Bob", result.Items[0].FirstName);
    }

    [Fact]
    public async Task GetPeople_DefaultSortsByLastNameThenFirstName()
    {
        await using var db = fixture.CreateDbContext();
        db.People.AddRange(
            new Person { Id = Guid.NewGuid(), FirstName = "Zara", LastName = "Adams", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new Person { Id = Guid.NewGuid(), FirstName = "Alex", LastName = "Brown", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new Person { Id = Guid.NewGuid(), FirstName = "Amy", LastName = "Adams", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, null, null, 1, 25), CancellationToken.None);

        // Adams before Brown, Amy Adams before Zara Adams
        Assert.Equal("Amy", result.Items[0].FirstName);
        Assert.Equal("Zara", result.Items[1].FirstName);
        Assert.Equal("Alex", result.Items[2].FirstName);
    }

    [Fact]
    public async Task GetPeople_IncludesCredentialSummary()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Sum", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        db.Credentials.AddRange(
            new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = "SUM-001", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) },
            new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = "SUM-002", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), RevokedAt = DateTimeOffset.UtcNow.AddDays(-5), RevocationReason = "Lost", CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) }
        );
        await db.SaveChangesAsync();

        var handler = new GetPeopleQueryHandler(db);
        var result = await handler.Handle(new GetPeopleQuery(null, null, null, null, 1, 25), CancellationToken.None);

        var item = result.Items.First(p => p.Id == person.Id);
        Assert.Equal(1, item.CredentialSummary.ActiveCount);
        Assert.Equal(1, item.CredentialSummary.RevokedCount);
    }

    [Fact]
    public async Task GetPersonById_ReturnsDetailWithCredentials()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Detail", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, Email = "detail@test.com", Department = "IT", CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = "DET-001", AccessLevel = CredentialAccessLevel.Clinical, IssuedAt = DateTimeOffset.UtcNow.AddDays(-10), ExpiresAt = DateTimeOffset.UtcNow.AddDays(355), CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var handler = new GetPersonByIdQueryHandler(db);
        var result = await handler.Handle(new GetPersonByIdQuery(person.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Detail", result.FirstName);
        Assert.Equal("Detail Test", result.DisplayName);
        Assert.Single(result.Credentials);
        Assert.Equal("DET-001", result.Credentials[0].CredentialNumber);
        Assert.Equal("Active", result.Credentials[0].Status);
    }

    [Fact]
    public async Task GetPersonById_UnknownId_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();

        var handler = new GetPersonByIdQueryHandler(db);
        var result = await handler.Handle(new GetPersonByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    private async Task SeedPeople(Microsoft.EntityFrameworkCore.DbContext db, int count)
    {
        var credDb = (Vision.CredentialService.Infrastructure.Persistence.CredentialDbContext)db;
        for (int i = 0; i < count; i++)
        {
            credDb.People.Add(new Person
            {
                Id = Guid.NewGuid(), FirstName = $"Person{i}", LastName = $"Last{i}",
                PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await credDb.SaveChangesAsync();
    }

    private async Task SeedMixedPeople(Microsoft.EntityFrameworkCore.DbContext db)
    {
        var credDb = (Vision.CredentialService.Infrastructure.Persistence.CredentialDbContext)db;
        credDb.People.AddRange(
            new Person { Id = Guid.NewGuid(), FirstName = "Active", LastName = "Employee", PersonType = PersonType.Employee, IsActive = true, Department = "Surgery", CreatedAt = DateTimeOffset.UtcNow },
            new Person { Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "Worker", PersonType = PersonType.Employee, IsActive = false, Department = "Admin", CreatedAt = DateTimeOffset.UtcNow },
            new Person { Id = Guid.NewGuid(), FirstName = "Active", LastName = "Contractor", PersonType = PersonType.Contractor, IsActive = true, Department = "Facilities", CreatedAt = DateTimeOffset.UtcNow }
        );
        await credDb.SaveChangesAsync();
    }
}
