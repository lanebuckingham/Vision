namespace Vision.CredentialService.Application.People.Queries;

public sealed record PersonListItemDto(
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
    PersonCredentialSummaryDto CredentialSummary);

public sealed record PersonCredentialSummaryDto(
    int ActiveCount,
    int ExpiringSoonCount,
    int RevokedCount);
