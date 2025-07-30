using JobSharp.Core;
using JobSharp.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq;

namespace JobSharp.EntityFramework;

/// <summary>
/// Entity Framework DbContext for JobSharp.
/// </summary>
public class JobSharpDbContext : DbContext
{
    public JobSharpDbContext(DbContextOptions<JobSharpDbContext> options) : base(options)
    {
    }

    public DbSet<JobEntity> Jobs { get; set; } = null!;
    public DbSet<RecurringJobEntity> RecurringJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure JobEntity
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.TypeName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.State)
                .HasConversion<int>();

            entity.Property(e => e.BatchId)
                .HasMaxLength(36);

            entity.Property(e => e.ParentJobId)
                .HasMaxLength(36);

            // Configure relationships
            entity.HasOne(e => e.ParentJob)
                .WithMany(e => e.Continuations)
                .HasForeignKey(e => e.ParentJobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure indexes for better query performance
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.BatchId);
            entity.HasIndex(e => e.ParentJobId);
            entity.HasIndex(e => new { e.State, e.ScheduledAt });
        });

        // Configure RecurringJobEntity
        modelBuilder.Entity<RecurringJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.CronExpression)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.JobTypeName)
                .HasMaxLength(500)
                .IsRequired();

            // Configure indexes
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.NextExecution);
        });

        // SQLite DateTimeOffset workaround
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
            // here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
            // To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
            // use the DateTimeOffsetToBinaryConverter
            // Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
            // This only supports millisecond precision, but should be sufficient for most use cases.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(DateTimeOffset)
                                                                               || p.PropertyType == typeof(DateTimeOffset?));
                foreach (var property in properties)
                {
                    modelBuilder
                        .Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}