using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        builder.Property(m => m.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.LastError)
            .HasMaxLength(2000);

        // W3C traceparent is a fixed-format string (version-traceid-parentid-flags);
        // tracestate is vendor-defined and can be longer. Both are optional observability
        // metadata, not business identifiers.
        builder.Property(m => m.TraceParent)
            .HasMaxLength(100);

        builder.Property(m => m.TraceState)
            .HasMaxLength(512);

        // Partial index for unpublished messages ordered by occurred_at
        builder.HasIndex(m => m.OccurredAt)
            .HasFilter("published_at IS NULL")
            .HasDatabaseName("ix_outbox_messages_unpublished");
    }
}
