namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed record CredentialDetailDto(
    Guid Id,
    string CredentialNumber,
    string AccessLevel,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpiringSoon,
    DateTimeOffset? RevokedAt,
    string? RevocationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    CredentialDetailPersonDto Person);

public sealed record CredentialDetailPersonDto(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string PersonType,
    bool IsActive,
    string? EmployeeNumber,
    string? Email,
    string? Department,
    string? JobTitle);
