using AgentAssist.Infrastructure.Persistence.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentAssist.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackRecordConfiguration : IEntityTypeConfiguration<FeedbackRecordEntity>
{
    public void Configure(EntityTypeBuilder<FeedbackRecordEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("FeedbackRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.CorrelationId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.UserId).HasMaxLength(200);
        builder.Property(e => e.Helpful).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(2000);
        builder.Property(e => e.Timestamp).IsRequired();

        builder.HasIndex(e => e.CorrelationId);
    }
}
