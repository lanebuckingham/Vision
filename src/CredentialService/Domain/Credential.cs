namespace Vision.CredentialService.Domain;

public class Credential
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public required string CredentialNumber { get; set; }
    public CredentialAccessLevel AccessLevel { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public Person Person { get; set; } = null!;

    public CredentialStatus Status
    {
        get
        {
            if (RevokedAt is not null)
                return CredentialStatus.Revoked;

            if (ExpiresAt <= DateTimeOffset.UtcNow)
                return CredentialStatus.Expired;

            return CredentialStatus.Active;
        }
    }

    public void Revoke(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason is required.", nameof(reason));

        // Idempotent — preserve original RevokedAt
        if (RevokedAt is not null)
            return;

        var now = DateTimeOffset.UtcNow;
        RevokedAt = now;
        RevocationReason = reason;
        UpdatedAt = now;
    }
}
