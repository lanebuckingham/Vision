using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Tests.Domain;

public class CredentialStatusTests
{
    [Fact]
    public void Status_WhenNotRevokedAndNotExpired_ReturnsActive()
    {
        var credential = CreateCredential(
            expiresAt: DateTimeOffset.UtcNow.AddDays(90),
            revokedAt: null);

        Assert.Equal(CredentialStatus.Active, credential.Status);
    }

    [Fact]
    public void Status_WhenNotRevokedAndExpired_ReturnsExpired()
    {
        var credential = CreateCredential(
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1),
            revokedAt: null);

        Assert.Equal(CredentialStatus.Expired, credential.Status);
    }

    [Fact]
    public void Status_WhenExpiresAtEqualsNow_ReturnsExpired()
    {
        var credential = CreateCredential(
            expiresAt: DateTimeOffset.UtcNow,
            revokedAt: null);

        Assert.Equal(CredentialStatus.Expired, credential.Status);
    }

    [Fact]
    public void Status_WhenRevoked_ReturnsRevoked()
    {
        var credential = CreateCredential(
            expiresAt: DateTimeOffset.UtcNow.AddDays(90),
            revokedAt: DateTimeOffset.UtcNow.AddDays(-5));

        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    [Fact]
    public void Status_WhenRevokedAndExpired_ReturnsRevoked()
    {
        // Revoked takes precedence over expired
        var credential = CreateCredential(
            expiresAt: DateTimeOffset.UtcNow.AddDays(-30),
            revokedAt: DateTimeOffset.UtcNow.AddDays(-60));

        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    private static Credential CreateCredential(DateTimeOffset expiresAt, DateTimeOffset? revokedAt) => new()
    {
        Id = Guid.NewGuid(),
        PersonId = Guid.NewGuid(),
        CredentialNumber = "TEST-001",
        AccessLevel = CredentialAccessLevel.General,
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-365),
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt,
        RevocationReason = revokedAt.HasValue ? "Test reason" : null,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-365),
    };
}
