using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Covers CreateIncidentCommandHandler's cross-entity validation: unknown location,
/// unknown asset, and asset/location mismatch. These paths are not exercised by the
/// authorization suite, which only tests the happy path.
/// </summary>
[Collection("SecurityOperationsApplication")]
public class CreateIncidentValidationTests : IAsyncLifetime
{
    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateIncident_UnknownLocation_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = Guid.NewGuid(),
            assetId = (Guid?)null,
            title = "Unknown location test",
            description = "Should fail because the location does not exist.",
            severity = "Low",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIncident_UnknownAsset_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = SeedDataIds.PharmacyStorage,
            assetId = Guid.NewGuid(),
            title = "Unknown asset test",
            description = "Should fail because the asset does not exist.",
            severity = "Low",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIncident_AssetBelongsToDifferentLocation_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();

        // PharmacyStorageCamera02 belongs to PharmacyStorage, not MainLobby.
        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = SeedDataIds.MainLobby,
            assetId = SeedDataIds.PharmacyStorageCamera02,
            title = "Mismatched location/asset test",
            description = "Should fail because the asset belongs to a different location.",
            severity = "Low",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIncident_ValidLocationAndMatchingAsset_Succeeds()
    {
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = SeedDataIds.PharmacyStorage,
            assetId = SeedDataIds.PharmacyStorageCamera02,
            title = "Valid location/asset test",
            description = "Should succeed because location and asset are consistent.",
            severity = "Low",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("NotARealStatus")]
    [InlineData("")]
    public async Task GetIncidents_InvalidStatus_ReturnsBadRequest(string status)
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/incidents?status={status}");

        // Blank status is treated as "no filter" by the query itself, so only a
        // genuinely invalid enum value should fail validation.
        if (string.IsNullOrWhiteSpace(status))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetIncidents_InvalidSeverity_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/incidents?severity=Catastrophic");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetIncidents_PageLessThanOne_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/incidents?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetIncidents_PageSizeExceedsMaximum_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/incidents?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAssets_InvalidStatus_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/assets?status=Broken");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAssets_InvalidAssetType_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/assets?type=Drone");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAssets_PageLessThanOne_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/assets?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAssets_PageSizeExceedsMaximum_ReturnsBadRequest()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/assets?pageSize=250");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
