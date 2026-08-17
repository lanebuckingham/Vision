using Vision.CredentialService.Application.Credentials.Queries;

namespace Vision.CredentialService.Application.People.Queries;

public sealed record PersonDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string PersonType,
    bool IsActive,
    string? EmployeeNumber,
    string? Email,
    string? Department,
    string? JobTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<PersonCredentialDto> Credentials);

public sealed record PersonCredentialDto(
    Guid Id,
    string CredentialNumber,
    string AccessLevel,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpiringSoon,
    DateTimeOffset? RevokedAt,
    string? RevocationReason);
