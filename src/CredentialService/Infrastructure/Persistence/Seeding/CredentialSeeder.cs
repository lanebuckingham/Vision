using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Infrastructure.Persistence.Seeding;

public static class CredentialSeeder
{
    public static async Task SeedAsync(CredentialDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.People.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;

        var people = CreatePeople(now);
        var credentials = CreateCredentials(people, now);

        context.People.AddRange(people);
        context.Credentials.AddRange(credentials);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<Person> CreatePeople(DateTimeOffset now)
    {
        var ninetyDaysAgo = now.AddDays(-90);

        return
        [
            // Employees
            new() { Id = SeedDataIds.PersonJamesWilson, FirstName = "James", LastName = "Wilson", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-001", Email = "james.wilson@northstarmedical.com", Department = "Security", JobTitle = "Security Manager", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonMariaGarcia, FirstName = "Maria", LastName = "Garcia", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-002", Email = "maria.garcia@northstarmedical.com", Department = "Pharmacy", JobTitle = "Lead Pharmacist", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonRobertKim, FirstName = "Robert", LastName = "Kim", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-003", Email = "robert.kim@northstarmedical.com", Department = "ICU", JobTitle = "Charge Nurse", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonEmilyCarter, FirstName = "Emily", LastName = "Carter", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-004", Email = "emily.carter@northstarmedical.com", Department = "Emergency", JobTitle = "ER Physician", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonMichaelBrown, FirstName = "Michael", LastName = "Brown", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-005", Email = "michael.brown@northstarmedical.com", Department = "Surgery", JobTitle = "Surgical Technician", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonJessicaDavis, FirstName = "Jessica", LastName = "Davis", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-006", Email = "jessica.davis@northstarmedical.com", Department = "Administration", JobTitle = "HR Coordinator", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonAndrewNguyen, FirstName = "Andrew", LastName = "Nguyen", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-007", Email = "andrew.nguyen@northstarmedical.com", Department = "IT", JobTitle = "Systems Administrator", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonRachelThompson, FirstName = "Rachel", LastName = "Thompson", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-008", Email = "rachel.thompson@northstarmedical.com", Department = "Pharmacy", JobTitle = "Pharmacist", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonDanielMartinez, FirstName = "Daniel", LastName = "Martinez", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-009", Email = "daniel.martinez@northstarmedical.com", Department = "Security", JobTitle = "Security Officer", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonOliviaWright, FirstName = "Olivia", LastName = "Wright", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-010", Email = "olivia.wright@northstarmedical.com", Department = "Radiology", JobTitle = "Radiologist", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonChrisLee, FirstName = "Chris", LastName = "Lee", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-011", Email = "chris.lee@northstarmedical.com", Department = "Facilities", JobTitle = "Facilities Coordinator", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonAmandaHall, FirstName = "Amanda", LastName = "Hall", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-012", Email = "amanda.hall@northstarmedical.com", Department = "ICU", JobTitle = "ICU Nurse", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonKevinScott, FirstName = "Kevin", LastName = "Scott", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-013", Email = "kevin.scott@northstarmedical.com", Department = "Emergency", JobTitle = "Paramedic", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonNatalieRoss, FirstName = "Natalie", LastName = "Ross", PersonType = PersonType.Employee, IsActive = true, EmployeeNumber = "EMP-014", Email = "natalie.ross@northstarmedical.com", Department = "Administration", JobTitle = "Credentials Administrator", CreatedAt = ninetyDaysAgo },
            // Contractors
            new() { Id = SeedDataIds.PersonBrianTaylor, FirstName = "Brian", LastName = "Taylor", PersonType = PersonType.Contractor, IsActive = true, EmployeeNumber = "CTR-001", Email = "brian.taylor@techservices.com", Department = "IT", JobTitle = "Network Contractor", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonSophiaAdams, FirstName = "Sophia", LastName = "Adams", PersonType = PersonType.Contractor, IsActive = true, EmployeeNumber = "CTR-002", Email = "sophia.adams@cleanteam.com", Department = "Facilities", JobTitle = "Cleaning Supervisor", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonJasonClark, FirstName = "Jason", LastName = "Clark", PersonType = PersonType.Contractor, IsActive = true, EmployeeNumber = "CTR-003", Email = "jason.clark@securetechpro.com", Department = "Security", JobTitle = "Security Consultant", CreatedAt = ninetyDaysAgo },
            new() { Id = SeedDataIds.PersonMeganWhite, FirstName = "Megan", LastName = "White", PersonType = PersonType.Contractor, IsActive = false, EmployeeNumber = "CTR-004", Email = "megan.white@medequip.com", Department = "Facilities", JobTitle = "Equipment Maintenance", CreatedAt = ninetyDaysAgo }
        ];
    }

    private static List<Credential> CreateCredentials(List<Person> people, DateTimeOffset now)
    {
        var credentials = new List<Credential>();
        var counter = 1;
        var oneYearFromNow = now.AddYears(1);
        var sixMonthsFromNow = now.AddMonths(6);
        var twentyDaysFromNow = now.AddDays(20); // expiring soon
        var tenDaysFromNow = now.AddDays(10); // expiring soon
        var thirtyDaysAgo = now.AddDays(-30); // already expired
        var ninetyDaysAgo = now.AddDays(-90);

        // Security personnel — Security access level
        credentials.Add(CreateCredential(SeedDataIds.PersonJamesWilson, $"NMC-{counter++:D5}", CredentialAccessLevel.Security, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonDanielMartinez, $"NMC-{counter++:D5}", CredentialAccessLevel.Security, ninetyDaysAgo, oneYearFromNow, now));

        // Clinical staff — Clinical or Restricted
        credentials.Add(CreateCredential(SeedDataIds.PersonMariaGarcia, $"NMC-{counter++:D5}", CredentialAccessLevel.Restricted, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonRobertKim, $"NMC-{counter++:D5}", CredentialAccessLevel.Clinical, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonEmilyCarter, $"NMC-{counter++:D5}", CredentialAccessLevel.Clinical, ninetyDaysAgo, oneYearFromNow, now));

        // Michael Brown — the "lost badge" demo credential (active, will be revoked in demo)
        credentials.Add(new Credential
        {
            Id = SeedDataIds.CredentialLostBadge,
            PersonId = SeedDataIds.PersonMichaelBrown,
            CredentialNumber = $"NMC-{counter++:D5}",
            AccessLevel = CredentialAccessLevel.Clinical,
            IssuedAt = ninetyDaysAgo,
            ExpiresAt = oneYearFromNow,
            CreatedAt = now
        });

        // General access employees
        credentials.Add(CreateCredential(SeedDataIds.PersonJessicaDavis, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonAndrewNguyen, $"NMC-{counter++:D5}", CredentialAccessLevel.Restricted, ninetyDaysAgo, sixMonthsFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonRachelThompson, $"NMC-{counter++:D5}", CredentialAccessLevel.Restricted, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonOliviaWright, $"NMC-{counter++:D5}", CredentialAccessLevel.Clinical, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonChrisLee, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonAmandaHall, $"NMC-{counter++:D5}", CredentialAccessLevel.Clinical, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonKevinScott, $"NMC-{counter++:D5}", CredentialAccessLevel.Clinical, ninetyDaysAgo, oneYearFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonNatalieRoss, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo, oneYearFromNow, now));

        // Contractors — shorter expiration
        credentials.Add(CreateCredential(SeedDataIds.PersonBrianTaylor, $"NMC-{counter++:D5}", CredentialAccessLevel.Restricted, ninetyDaysAgo, sixMonthsFromNow, now));
        credentials.Add(CreateCredential(SeedDataIds.PersonSophiaAdams, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo, twentyDaysFromNow, now)); // expiring soon
        credentials.Add(CreateCredential(SeedDataIds.PersonJasonClark, $"NMC-{counter++:D5}", CredentialAccessLevel.Security, ninetyDaysAgo, tenDaysFromNow, now)); // expiring soon

        // Expired credential (inactive contractor)
        credentials.Add(CreateCredential(SeedDataIds.PersonMeganWhite, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo.AddDays(-90), thirtyDaysAgo, now));

        // Revoked credential — historical example
        var revokedCred = CreateCredential(SeedDataIds.PersonMeganWhite, $"NMC-{counter++:D5}", CredentialAccessLevel.General, ninetyDaysAgo.AddDays(-180), oneYearFromNow, now);
        revokedCred.RevokedAt = ninetyDaysAgo;
        revokedCred.RevocationReason = "Contract terminated";
        credentials.Add(revokedCred);

        return credentials;
    }

    private static Credential CreateCredential(Guid personId, string number, CredentialAccessLevel level, DateTimeOffset issuedAt, DateTimeOffset expiresAt, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        PersonId = personId,
        CredentialNumber = number,
        AccessLevel = level,
        IssuedAt = issuedAt,
        ExpiresAt = expiresAt,
        CreatedAt = now
    };
}
