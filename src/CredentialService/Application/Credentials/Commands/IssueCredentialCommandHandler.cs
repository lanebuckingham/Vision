using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Queries;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Application.Credentials.Commands;

public sealed class IssueCredentialCommandHandler(
    CredentialDbContext db,
    ILogger<IssueCredentialCommandHandler> logger)
    : IRequestHandler<IssueCredentialCommand, CredentialDetailDto>
{
    public async Task<CredentialDetailDto> Handle(
        IssueCredentialCommand request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person {request.PersonId} not found.");

        if (!person.IsActive)
            throw new InvalidOperationException("A new credential cannot be issued to an inactive person.");

        var now = DateTimeOffset.UtcNow;

        if (request.ExpiresAt <= now)
            throw new ArgumentException("Expiration date must be in the future.");

        // Check uniqueness
        var duplicateExists = await db.Credentials
            .AnyAsync(c => c.CredentialNumber == request.CredentialNumber, cancellationToken);

        if (duplicateExists)
            throw new InvalidOperationException($"Credential number {request.CredentialNumber} is already in use.");

        var accessLevel = Enum.Parse<CredentialAccessLevel>(request.AccessLevel, ignoreCase: true);

        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CredentialNumber = request.CredentialNumber,
            AccessLevel = accessLevel,
            IssuedAt = now,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now
        };

        db.Credentials.Add(credential);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException(
                $"Credential number {request.CredentialNumber} is already in use.");
        }

        logger.LogInformation(
            "Credential {CredentialNumber} issued to person {PersonId}",
            credential.CredentialNumber,
            credential.PersonId);

        var expiringSoonThreshold = now.AddDays(CredentialPolicy.ExpiringSoonDays);

        return new CredentialDetailDto(
            credential.Id,
            credential.CredentialNumber,
            credential.AccessLevel.ToString(),
            "Active",
            credential.IssuedAt,
            credential.ExpiresAt,
            credential.ExpiresAt <= expiringSoonThreshold,
            null,
            null,
            credential.CreatedAt,
            credential.UpdatedAt,
            new CredentialDetailPersonDto(
                person.Id,
                person.FirstName,
                person.LastName,
                person.FirstName + " " + person.LastName,
                person.PersonType.ToString(),
                person.IsActive,
                person.EmployeeNumber,
                person.Email,
                person.Department,
                person.JobTitle));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique_violation error code is 23505
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
    }
}
