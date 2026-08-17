using System.Reflection;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Verifies the application starts successfully with the observability configuration
/// used by tests, and that observability being disabled entirely does not disable
/// core business processing (health endpoints, authenticated API access).
/// </summary>
[Collection("SecurityOperationsApplication")]
public class ObservabilityStartupTests : IAsyncLifetime
{
    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Application_StartsAndServesRequests_WithDefaultObservabilityConfiguration()
    {
        // No OTEL_EXPORTER_OTLP_ENDPOINT is configured in the test environment — this
        // proves the OTLP exporter being unconfigured does not prevent startup or
        // block a normal authenticated request from completing.
        using var client = _factory.CreateDefaultClient();

        var health = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, health.StatusCode);

        var dashboard = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(System.Net.HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Fact]
    public async Task Application_StartsAndServesRequests_WithObservabilityDisabled()
    {
        // Disabling telemetry entirely must not disable application logging, health
        // endpoints, or business API processing.
        using var disabledFactory = new SecurityOperationsApplicationFactory();
        disabledFactory.DisableObservability = true;
        await disabledFactory.EnsureDatabaseReadyAsync();

        using var client = disabledFactory.CreateDefaultClient();

        var health = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, health.StatusCode);

        var dashboard = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(System.Net.HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    [InlineData("2")]
    public void InvalidSamplingRatio_FailsFastWithClearConfigurationError(string invalidRatio)
    {
        // An out-of-range Observability:SamplingRatio must fail fast during startup
        // with an actionable message, rather than surfacing later as an obscure
        // ArgumentOutOfRangeException from deep inside TraceIdRatioBasedSampler.
        using var invalidFactory = new SecurityOperationsApplicationFactory();
        invalidFactory.SamplingRatioOverride = invalidRatio;

        var ex = Assert.ThrowsAny<Exception>(() => invalidFactory.Services);
        var actual = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;

        Assert.Contains("SamplingRatio", actual.ToString());
    }
}
