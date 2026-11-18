using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagementApi.Domain;

namespace TaskManagementApi.Infrastructure.Data;

/// <summary>
/// High-performance EF Core DbContext configured for DbContextPooling and zero-overhead tracking by default.
/// </summary>
public sealed class TaskDbContext(DbContextOptions<TaskDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("Tasks");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Description)
                .HasMaxLength(2000);

            entity.Property(t => t.Status)
                .IsRequired()
                .HasConversion<byte>();

            entity.Property(t => t.Assignee)
                .IsRequired()
                .HasMaxLength(150)
                .UseCollation("NOCASE");

            entity.Property(t => t.DueDate)
                .IsRequired()
                .HasConversion(dateTimeOffsetConverter);

            entity.Property(t => t.CreatedAtUtc)
                .IsRequired()
                .HasConversion(dateTimeOffsetConverter);

            entity.Property(t => t.UpdatedAtUtc)
                .IsRequired()
                .HasConversion(dateTimeOffsetConverter);

            // Composite & single-column indexes for fast filtering and sorting
            entity.HasIndex(t => new { t.Status, t.Assignee, t.DueDate })
                .HasDatabaseName("IX_Tasks_Status_Assignee_DueDate");

            entity.HasIndex(t => t.Assignee)
                .HasDatabaseName("IX_Tasks_Assignee");

            entity.HasIndex(t => t.DueDate)
                .HasDatabaseName("IX_Tasks_DueDate");
        });
    }
}
