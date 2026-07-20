
using Microsoft.EntityFrameworkCore;
using TraceabilityService.Models;

namespace TraceabilityService.Data;

public class TraceDbContext : DbContext
{
    public TraceDbContext(DbContextOptions<TraceDbContext> options) : base(options) { }

    public DbSet<TraceEventRecord> TraceEvents => Set<TraceEventRecord>();
    public DbSet<JobStateEntity> JobStates => Set<JobStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Add indexes for fast querying based on gRPC needs
        modelBuilder.Entity<TraceEventRecord>()
            .HasIndex(t => t.JobId);
            
        modelBuilder.Entity<TraceEventRecord>()
            .HasIndex(t => t.CorrelationId);
            
        modelBuilder.Entity<TraceEventRecord>()
            .HasIndex(t => t.Timestamp);

        modelBuilder.Entity<JobStateEntity>()
            .HasKey(e => new { e.TenantId, e.AppId, e.JobId });

        modelBuilder.Entity<JobStateEntity>()
            .HasIndex(t => t.Status);
    }
}
