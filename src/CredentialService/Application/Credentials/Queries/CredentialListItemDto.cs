namespace Vision.CredentialService.Application.Credentials.Queries;

public sealed record CredentialListItemDto(
    Guid Id,
    string CredentialNumber,
    string AccessLevel,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpiringSoon,
    DateTimeOffset? RevokedAt,
    CredentialPersonDto Person);

public sealed record CredentialPersonDto(
    Guid Id,
    string DisplayName,
    string PersonType,
    bool IsActive,
    string? EmployeeNumber,
    string? Department,
    string? JobTitle);
