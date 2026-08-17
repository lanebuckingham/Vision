using System.Net.Http.Headers;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Regression coverage for CorrelationMiddleware's X-Correlation-ID behavior. This is
/// the durable Vision-level identifier that Phase 6 observability work must not
/// replace with or conflate with OpenTelemetry TraceId — see
/// docs/development/observability.md.
/// </summary>
[Collection("SecurityOperationsApplication")]
public class CorrelationMiddlewareTests : IAsyncLifetime
{
    private const string HeaderName = "X-Correlation-ID";

    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidIncomingCorrelationId_IsPreservedInResponse()
    {
        using var client = _factory.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Add(HeaderName, "caller-supplied-correlation-123");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        Assert.Equal("caller-supplied-correlation-123", values!.Single());
    }

    [Fact]
    public async Task MissingCorrelationId_GeneratesNewNonBlankValue()
    {
        using var client = _factory.CreateDefaultClient();

        using var response = await client.GetAsync("/api/v1/dashboard");

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        var generated = values!.Single();
        Assert.False(string.IsNullOrWhiteSpace(generated));
    }

    [Fact]
    public async Task BlankCorrelationId_IsReplacedWithGeneratedValue()
    {
        using var client = _factory.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        // A header value that is present but blank must be treated as absent, not trusted as-is.
        request.Headers.TryAddWithoutValidation(HeaderName, "   ");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        var result = values!.Single();
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual("   ", result);
    }

    [Fact]
    public async Task OverlongCorrelationId_IsReplacedWithGeneratedValue()
    {
        using var client = _factory.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        var overlong = new string('a', 200);
        request.Headers.Add(HeaderName, overlong);

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        Assert.NotEqual(overlong, values!.Single());
    }

    [Fact]
    public async Task TwoRequestsWithoutHeader_ReceiveDifferentGeneratedCorrelationIds()
    {
        using var client = _factory.CreateDefaultClient();

        using var response1 = await client.GetAsync("/api/v1/dashboard");
        using var response2 = await client.GetAsync("/api/v1/dashboard");

        response1.Headers.TryGetValues(HeaderName, out var values1);
        response2.Headers.TryGetValues(HeaderName, out var values2);

        Assert.NotEqual(values1!.Single(), values2!.Single());
    }

    [Fact]
    public async Task RequestProcessing_EstablishesLoggingScopeContainingCorrelationId()
    {
        using var client = _factory.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Add(HeaderName, "scope-capture-correlation-id");

        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        // Prove CorrelationMiddleware actually calls ILogger.BeginScope with the
        // CorrelationId — not just that the header round-trips — by inspecting every
        // scope value established anywhere in the app during this request.
        var matchingScope = _factory.ScopeCapture.CapturedScopes
            .OfType<IEnumerable<KeyValuePair<string, object>>>()
            .SelectMany(scope => scope)
            .FirstOrDefault(kvp => kvp.Key == "CorrelationId"
                && Equals(kvp.Value, "scope-capture-correlation-id"));

        Assert.NotEqual(default, matchingScope);
    }
}
