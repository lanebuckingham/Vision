using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Tests.Domain;

public class CredentialRevocationTests
{
    [Fact]
    public void Revoke_WithValidReason_SetsRevokedAtAndReason()
    {
        var credential = CreateActiveCredential();
        var beforeRevoke = DateTimeOffset.UtcNow;

        credential.Revoke("Badge reported lost");

        Assert.NotNull(credential.RevokedAt);
        Assert.True(credential.RevokedAt >= beforeRevoke);
        Assert.Equal("Badge reported lost", credential.RevocationReason);
        Assert.NotNull(credential.UpdatedAt);
        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    [Fact]
    public void Revoke_WithValidReason_UsesConsistentTimestamp()
    {
        var credential = CreateActiveCredential();

        credential.Revoke("Lost");

        // RevokedAt and UpdatedAt should be the same instant
        Assert.Equal(credential.RevokedAt, credential.UpdatedAt);
    }

    [Fact]
    public void Revoke_WithBlankReason_ThrowsArgumentException()
    {
        var credential = CreateActiveCredential();

        Assert.Throws<ArgumentException>(() => credential.Revoke(""));
    }

    [Fact]
    public void Revoke_WithWhitespaceReason_ThrowsArgumentException()
    {
        var credential = CreateActiveCredential();

        Assert.Throws<ArgumentException>(() => credential.Revoke("   "));
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_PreservesOriginalRevokedAt()
    {
        var credential = CreateActiveCredential();

        credential.Revoke("First reason");
        var originalRevokedAt = credential.RevokedAt;

        // Wait a moment to ensure different timestamps
        credential.Revoke("Second reason");

        Assert.Equal(originalRevokedAt, credential.RevokedAt);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_PreservesOriginalReason()
    {
        var credential = CreateActiveCredential();

        credential.Revoke("First reason");
        credential.Revoke("Different reason");

        Assert.Equal("First reason", credential.RevocationReason);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_StatusRemainsRevoked()
    {
        var credential = CreateActiveCredential();

        credential.Revoke("First reason");
        credential.Revoke("Second attempt");

        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    private static Credential CreateActiveCredential() => new()
    {
        Id = Guid.NewGuid(),
        PersonId = Guid.NewGuid(),
        CredentialNumber = "NMC-TEST-001",
        AccessLevel = CredentialAccessLevel.Clinical,
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(335),
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
    };
}
