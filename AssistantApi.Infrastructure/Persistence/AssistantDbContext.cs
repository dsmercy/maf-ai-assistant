using AssistantApi.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

public class AssistantDbContext : DbContext
{
    public AssistantDbContext(DbContextOptions<AssistantDbContext> options) : base(options) { }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<FileHash> FileHashes => Set<FileHash>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Repository>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Url).IsRequired().HasMaxLength(2048);
            e.Property(r => r.Name).IsRequired().HasMaxLength(256);
            e.Property(r => r.Branch).HasMaxLength(256);
            e.Property(r => r.Status).HasConversion<string>();
            e.HasIndex(r => r.Url);
        });

        modelBuilder.Entity<IngestionJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.JobType).HasConversion<string>();
            e.Property(j => j.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.UserId).IsRequired().HasMaxLength(256);
            e.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId);
        });

        modelBuilder.Entity<ConversationMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Role).IsRequired().HasMaxLength(32);
            e.Property(m => m.DetectedIntent).HasConversion<string>();
        });

        modelBuilder.Entity<FileHash>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FilePath).IsRequired().HasMaxLength(2048);
            e.Property(f => f.Hash).IsRequired().HasMaxLength(64);
            e.HasIndex(f => new { f.RepositoryId, f.FilePath }).IsUnique();
        });
    }
}
