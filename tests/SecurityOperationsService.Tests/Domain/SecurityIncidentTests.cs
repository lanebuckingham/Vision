using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Tests.Domain;

/// <summary>
/// Domain-level tests for <see cref="SecurityIncident"/> lifecycle behavior:
/// investigation transitions, resolution, work-order attachment, and the
/// automatic-work-order qualification rule. These tests require no infrastructure.
/// </summary>
public class SecurityIncidentTests
{
    private static SecurityIncident CreateOpenIncident(
        IncidentSeverity severity = IncidentSeverity.Medium,
        Guid? assetId = null)
    {
        var now = DateTimeOffset.UtcNow.AddHours(-1);
        return new SecurityIncident
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            SecurityAssetId = assetId,
            Title = "Test incident",
            Description = "Test incident description",
            Severity = severity,
            Status = IncidentStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // === StartInvestigation ===

    [Fact]
    public void StartInvestigation_WhenOpen_TransitionsToInvestigatingAndAdvancesUpdatedAt()
    {
        var incident = CreateOpenIncident();
        var updatedAtBefore = incident.UpdatedAt;

        incident.StartInvestigation();

        Assert.Equal(IncidentStatus.Investigating, incident.Status);
        Assert.True(incident.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public void StartInvestigation_WhenAlreadyInvestigating_IsIdempotentAndDoesNotThrow()
    {
        var incident = CreateOpenIncident();
        incident.StartInvestigation();
        var updatedAtAfterFirstCall = incident.UpdatedAt;

        incident.StartInvestigation();

        Assert.Equal(IncidentStatus.Investigating, incident.Status);
        Assert.Equal(updatedAtAfterFirstCall, incident.UpdatedAt);
    }

    [Fact]
    public void StartInvestigation_WhenResolved_ThrowsAndPreservesResolvedState()
    {
        var incident = CreateOpenIncident();
        incident.Resolve("Issue fixed.");
        var resolvedAt = incident.ResolvedAt;
        var resolutionSummary = incident.ResolutionSummary;

        Assert.Throws<InvalidOperationException>(incident.StartInvestigation);

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(resolvedAt, incident.ResolvedAt);
        Assert.Equal(resolutionSummary, incident.ResolutionSummary);
    }

    // === Resolve ===

    [Theory]
    [InlineData(IncidentStatus.Open)]
    [InlineData(IncidentStatus.Investigating)]
    public void Resolve_WhenOpenOrInvestigating_SetsResolvedStateWithSummaryAndTimestamps(IncidentStatus initialStatus)
    {
        var incident = CreateOpenIncident();
        if (initialStatus == IncidentStatus.Investigating)
            incident.StartInvestigation();

        incident.Resolve("Root cause identified and fixed.");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("Root cause identified and fixed.", incident.ResolutionSummary);
        Assert.True(incident.ResolvedAt.HasValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithBlankSummary_ThrowsArgumentException(string? summary)
    {
        var incident = CreateOpenIncident();

        Assert.Throws<ArgumentException>(() => incident.Resolve(summary!));
        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_IsIdempotentAndPreservesOriginalResolutionData()
    {
        var incident = CreateOpenIncident();
        incident.Resolve("Original resolution summary.");
        var originalResolvedAt = incident.ResolvedAt;
        var originalSummary = incident.ResolutionSummary;

        // Attempting to resolve again with a different summary must not overwrite the original.
        incident.Resolve("A different summary that should be ignored.");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(originalResolvedAt, incident.ResolvedAt);
        Assert.Equal(originalSummary, incident.ResolutionSummary);
    }

    // === AttachWorkOrder ===

    [Fact]
    public void AttachWorkOrder_FirstValidId_AttachesSuccessfully()
    {
        var incident = CreateOpenIncident();
        var workOrderId = Guid.NewGuid();

        incident.AttachWorkOrder(workOrderId);

        Assert.Equal(workOrderId, incident.WorkOrderId);
    }

    [Fact]
    public void AttachWorkOrder_SameIdAgain_IsIdempotent()
    {
        var incident = CreateOpenIncident();
        var workOrderId = Guid.NewGuid();
        incident.AttachWorkOrder(workOrderId);
        var updatedAtAfterFirstAttach = incident.UpdatedAt;

        incident.AttachWorkOrder(workOrderId);

        Assert.Equal(workOrderId, incident.WorkOrderId);
        Assert.Equal(updatedAtAfterFirstAttach, incident.UpdatedAt);
    }

    [Fact]
    public void AttachWorkOrder_EmptyGuid_ThrowsArgumentException()
    {
        var incident = CreateOpenIncident();

        Assert.Throws<ArgumentException>(() => incident.AttachWorkOrder(Guid.Empty));
        Assert.Null(incident.WorkOrderId);
    }

    [Fact]
    public void AttachWorkOrder_DifferentIdWhenAlreadyAttached_ThrowsAndDoesNotOverwrite()
    {
        var incident = CreateOpenIncident();
        var firstWorkOrderId = Guid.NewGuid();
        var secondWorkOrderId = Guid.NewGuid();
        incident.AttachWorkOrder(firstWorkOrderId);

        Assert.Throws<InvalidOperationException>(() => incident.AttachWorkOrder(secondWorkOrderId));
        Assert.Equal(firstWorkOrderId, incident.WorkOrderId);
    }

    // === QualifiesForAutomaticWorkOrder ===

    [Fact]
    public void QualifiesForAutomaticWorkOrder_CriticalWithAsset_IsTrue()
    {
        var incident = CreateOpenIncident(IncidentSeverity.Critical, Guid.NewGuid());

        Assert.True(incident.QualifiesForAutomaticWorkOrder);
    }

    [Fact]
    public void QualifiesForAutomaticWorkOrder_CriticalWithoutAsset_IsFalse()
    {
        var incident = CreateOpenIncident(IncidentSeverity.Critical, assetId: null);

        Assert.False(incident.QualifiesForAutomaticWorkOrder);
    }

    [Fact]
    public void QualifiesForAutomaticWorkOrder_HighWithAsset_IsFalse()
    {
        var incident = CreateOpenIncident(IncidentSeverity.High, Guid.NewGuid());

        Assert.False(incident.QualifiesForAutomaticWorkOrder);
    }
}
