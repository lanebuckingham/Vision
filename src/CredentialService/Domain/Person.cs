namespace Vision.CredentialService.Domain;

public class Person
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public PersonType PersonType { get; set; }
    public bool IsActive { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Credential> Credentials { get; set; } = [];
}
