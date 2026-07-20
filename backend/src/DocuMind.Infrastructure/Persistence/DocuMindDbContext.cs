using DocuMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the DocuMind shared document store, backed by PostgreSQL + pgvector.
/// </summary>
public class DocuMindDbContext : DbContext
{
    /// <summary>
    /// Embedding vector dimensionality, matching the configured Azure OpenAI embedding deployment
    /// (1536 for text-embedding-3-small).
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    public DocuMindDbContext(DbContextOptions<DocuMindDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.FileName).IsRequired();
            entity.HasMany(document => document.Chunks)
                .WithOne()
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.Content).IsRequired();
            entity.Property(chunk => chunk.Embedding).HasColumnType($"vector({EmbeddingDimensions})");

            entity.HasIndex(chunk => chunk.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        base.OnModelCreating(modelBuilder);
    }
}
