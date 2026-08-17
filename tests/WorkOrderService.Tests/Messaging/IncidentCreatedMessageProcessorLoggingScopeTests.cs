using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Messaging;

/// <summary>
/// Proves that IncidentCreatedMessageProcessor establishes an ILogger.BeginScope
/// containing the durable Vision CorrelationId while handling/acknowledging a message,
/// so any log line emitted during that work — across the handler, EF Core, the
/// DeleteMessage decision, etc. — can be searched by CorrelationId. Uses a recording
/// ILogger rather than a real provider to avoid depending on logging-framework
/// internals.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class IncidentCreatedMessageProcessorLoggingScopeTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_consumer_scope;Username=vision;Password=vision_dev";

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    private static WorkOrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    private static IncidentCreatedV1 CreateValidEvent() => new()
    {
        EventId = Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = "consumer-scope-test-correlation",
        Incident = new IncidentCreatedIncidentV1
        {
            Id = Guid.NewGuid(),
            Title = "Logging scope test camera",
            Description = "Testing consumer CorrelationId logging scope",
            Severity = "Critical"
        },
        Asset = new IncidentCreatedAssetV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Camera",
            AssetTag = "CAM-SCOPE",
            AssetType = "Camera"
        },
        Location = new IncidentCreatedLocationV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Location",
            BuildingId = Guid.NewGuid(),
            BuildingName = "Test Building"
        }
    };

    private static Message CreateSqsMessage(IncidentCreatedV1 evt) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        ReceiptHandle = Guid.NewGuid().ToString(),
        Body = JsonSerializer.Serialize(evt),
        Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "1" },
        MessageAttributes = []
    };

    private static IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<WorkOrderDbContext>(opts =>
            opts.UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IncidentCreatedHandler>();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ProcessAsync_EstablishesLoggingScopeContainingCorrelationId()
    {
        var recordingLogger = new RecordingLogger<IncidentCreatedMessageProcessor>();
        var sqsMock = Substitute.For<IAmazonSQS>();

        object? scopeDuringDelete = null;
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                scopeDuringDelete = recordingLogger.CapturedScopes.LastOrDefault();
                return Task.FromResult(new DeleteMessageResponse());
            });

        var processor = new IncidentCreatedMessageProcessor(CreateScopeFactory(), sqsMock, recordingLogger);
        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(scopeDuringDelete);
        var scopeDictionary = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scopeDuringDelete);
        Assert.Equal("consumer-scope-test-correlation", scopeDictionary["CorrelationId"]);

        // Scope must be cleaned up once message processing completes.
        Assert.Empty(recordingLogger.CapturedScopes);
    }

    /// <summary>
    /// Minimal ILogger test double that records BeginScope state objects.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<object> CapturedScopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            CapturedScopes.Add(state);
            return new PopOnDispose(this, state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // No-op: this double only needs to capture scopes for these tests.
        }

        private sealed class PopOnDispose(RecordingLogger<T> owner, object state) : IDisposable
        {
            public void Dispose() => owner.CapturedScopes.Remove(state);
        }
    }
}
