using AgentAssist.Infrastructure.Persistence.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentAssist.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Timestamp).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Mode).IsRequired().HasMaxLength(40);
        builder.Property(e => e.UserId).HasMaxLength(200);
        builder.Property(e => e.QuestionHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.QuestionPreview).IsRequired().HasMaxLength(160);
        builder.Property(e => e.RetrievalCount).IsRequired();
        builder.Property(e => e.CitationCount).IsRequired();
        builder.Property(e => e.ConfidenceLevel).IsRequired().HasMaxLength(20);
        builder.Property(e => e.RiskClass).IsRequired().HasMaxLength(20);
        builder.Property(e => e.EscalationRequired).IsRequired();
        builder.Property(e => e.Refused).IsRequired();
        builder.Property(e => e.RefusalReason).HasMaxLength(200);
        builder.Property(e => e.LatencyMs).IsRequired();

        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.Timestamp);
    }
}
