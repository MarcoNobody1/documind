using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the DocuMind document store, backed by PostgreSQL + pgvector, and
/// for ASP.NET Core Identity's <c>AspNet*</c> tables (users only — no roles, no claims/tokens
/// stores beyond what Identity's own model needs). Documents and chunks are owner-scoped (Phase 2,
/// PR4), not shared across users — see <c>Document.OwnerId</c>/<c>DocumentChunk.OwnerId</c>.
/// </summary>
public class DocuMindDbContext : IdentityUserContext<ApplicationUser, Guid>
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
        // IdentityUserContext's own conventions (AspNetUsers and its indexes) must be applied
        // FIRST, before the documents/document_chunks overrides below. Calling this last (as the
        // pre-Identity version of this method did) would let Identity's conventions run after —
        // and, more importantly, is the wrong order to reason about once both configurations share
        // one model. Kept first for that reason, not because of an observed conflict.
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.FileName).IsRequired();

            // Alternate key, not just an index: this is what lets DocumentChunk below declare a
            // composite FK against (Id, OwnerId) rather than just Id. Without it, nothing stops a
            // chunk row from naming a document id/owner-id pair that never co-occurred on
            // `documents` (ADR-H).
            entity.HasAlternateKey(document => new { document.Id, document.OwnerId });

            // Restrict, not Cascade: deleting an ASP.NET Identity user must not silently destroy
            // that user's documents, chunks and embeddings. There is no account-deletion flow yet
            // (Known follow-up); Restrict just ensures one never arrives that does this by
            // accident.
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(document => document.OwnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // The Document -> Chunks relationship itself (Cascade on delete) is configured below,
            // on DocumentChunk, as a composite FK against this entity's (Id, OwnerId) alternate
            // key rather than a plain DocumentId-only FK — see the comment there (ADR-H).
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.Content).IsRequired();
            entity.Property(chunk => chunk.Embedding).HasColumnType($"vector({EmbeddingDimensions})");

            // Composite FK against `documents`' alternate key (Id, OwnerId), replacing the plain
            // DocumentId-only FK. This is the structural guarantee behind ADR-H: the database
            // rejects any row where OwnerId disagrees with its own Document's OwnerId — a chunk
            // cannot claim an owner its document does not have. Cascade (not Restrict, unlike the
            // Document->AspNetUsers FK above): deleting a document must still delete its chunks, as
            // it always has.
            entity.HasOne<Document>()
                .WithMany(document => document.Chunks)
                .HasForeignKey(chunk => new { chunk.DocumentId, chunk.OwnerId })
                .HasPrincipalKey(document => new { document.Id, document.OwnerId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasIndex(chunk => chunk.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });
    }
}
