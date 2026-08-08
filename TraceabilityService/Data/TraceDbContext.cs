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
        
        if (Database.IsRelational())
        {
            modelBuilder.HasDefaultSchema("traceability");
        }
        
        modelBuilder.Entity<TraceEventRecord>().OwnsOne(t => t.Identity, id => 
        {
            id.Property(p => p.TenantId).HasColumnName("TenantId");
            id.Property(p => p.AppId).HasColumnName("AppId");
            id.Property(p => p.JobId).HasColumnName("JobId");
            id.HasIndex(p => new { p.TenantId, p.AppId, p.JobId });
        });

        modelBuilder.Entity<TraceEventRecord>()
            .HasIndex(t => new { t.CorrelationId });
            
        modelBuilder.Entity<TraceEventRecord>()
            .HasIndex(t => t.Timestamp);

        modelBuilder.Entity<JobStateEntity>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<JobStateEntity>().OwnsOne(e => e.Identity, id =>
        {
            id.Property(p => p.TenantId).HasColumnName("TenantId");
            id.Property(p => p.AppId).HasColumnName("AppId");
            id.Property(p => p.JobId).HasColumnName("JobId");
            id.HasIndex(p => new { p.TenantId, p.AppId, p.JobId }).IsUnique();
        });

        modelBuilder.Entity<JobStateEntity>()
            .HasIndex(t => t.Status);
            
        modelBuilder.Entity<JobStateEntity>()
            .Property(e => e.Status)
            .HasConversion<string>();
    }
}
