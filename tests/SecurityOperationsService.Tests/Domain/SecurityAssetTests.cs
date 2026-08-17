using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Tests.Domain;

/// <summary>
/// Domain-level tests for <see cref="SecurityAsset.ChangeStatus"/> behavior.
/// </summary>
public class SecurityAssetTests
{
    private static SecurityAsset CreateAsset(SecurityAssetStatus status)
    {
        var now = DateTimeOffset.UtcNow.AddHours(-1);
        return new SecurityAsset
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Name = "Test Camera",
            AssetType = SecurityAssetType.Camera,
            Status = status,
            StatusChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    [Fact]
    public void ChangeStatus_ToDifferentStatus_UpdatesStatusAndTimestamps()
    {
        var asset = CreateAsset(SecurityAssetStatus.Offline);
        var statusChangedAtBefore = asset.StatusChangedAt;
        var updatedAtBefore = asset.UpdatedAt;

        asset.ChangeStatus(SecurityAssetStatus.Operational);

        Assert.Equal(SecurityAssetStatus.Operational, asset.Status);
        Assert.True(asset.StatusChangedAt > statusChangedAtBefore);
        Assert.True(asset.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public void ChangeStatus_ToCurrentStatus_IsIdempotentAndDoesNotMutateTimestamps()
    {
        var asset = CreateAsset(SecurityAssetStatus.Operational);
        var statusChangedAtBefore = asset.StatusChangedAt;
        var updatedAtBefore = asset.UpdatedAt;

        asset.ChangeStatus(SecurityAssetStatus.Operational);

        Assert.Equal(SecurityAssetStatus.Operational, asset.Status);
        Assert.Equal(statusChangedAtBefore, asset.StatusChangedAt);
        Assert.Equal(updatedAtBefore, asset.UpdatedAt);
    }
}
