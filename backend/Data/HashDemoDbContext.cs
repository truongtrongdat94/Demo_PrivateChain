using HashAnchorDemo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HashAnchorDemo.Data;

public sealed class HashDemoDbContext : DbContext
{
    public HashDemoDbContext(DbContextOptions<HashDemoDbContext> options)
        : base(options)
    {
    }

    public DbSet<SensorRecord> SensorRecords => Set<SensorRecord>();

    public DbSet<AnchorBatch> AnchorBatches => Set<AnchorBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorRecord>(entity =>
        {
            entity.ToTable("sensor_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.OriginalJson).HasColumnName("original_json");
            entity.Property(record => record.BatchId).HasColumnName("batch_id");
            entity.Property(record => record.Status).HasColumnName("status");
            entity.HasOne(record => record.Batch)
                .WithMany(batch => batch.Records)
                .HasForeignKey(record => record.BatchId);
        });

        modelBuilder.Entity<AnchorBatch>(entity =>
        {
            entity.ToTable("anchor_batches");
            entity.HasKey(batch => batch.Id);
            entity.Property(batch => batch.Id).HasColumnName("id");
            entity.Property(batch => batch.RecordCount).HasColumnName("record_count");
            entity.Property(batch => batch.TransactionHash).HasColumnName("transaction_hash").HasMaxLength(66);
            entity.Property(batch => batch.Status).HasColumnName("status");
            entity.Property(batch => batch.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();
            entity.Property(batch => batch.AnchoredAt).HasColumnName("anchored_at");
        });
    }
}
