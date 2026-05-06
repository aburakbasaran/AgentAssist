using AgentAssist.Infrastructure.Persistence.Configurations;
using AgentAssist.Infrastructure.Persistence.Entities;

using Microsoft.EntityFrameworkCore;

namespace AgentAssist.Infrastructure.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the audit and feedback bounded context. Configured via <see cref="IEntityTypeConfiguration{TEntity}"/> classes; never with fluent API in <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AgentAssistDbContext(DbContextOptions<AgentAssistDbContext> options) : DbContext(options)
{
    internal DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    internal DbSet<FeedbackRecordEntity> FeedbackRecords => Set<FeedbackRecordEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new FeedbackRecordConfiguration());
    }
}
