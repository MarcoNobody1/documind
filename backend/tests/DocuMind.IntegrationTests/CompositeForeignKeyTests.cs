using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace DocuMind.IntegrationTests;

/// <summary>
/// Proves ADR-H's invariant is enforced by the database, not by discipline: a
/// <see cref="DocumentChunk"/> whose <see cref="DocumentChunk.OwnerId"/> disagrees with its own
/// <see cref="Document"/>'s <see cref="Document.OwnerId"/> must be rejected outright by the
/// composite foreign key <c>(DocumentId, OwnerId) -&gt; documents (Id, OwnerId)</c> — not merely
/// avoided by application code that happens to always stamp both columns together.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CompositeForeignKeyTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private DocuMindDbContext _dbContext = null!;

    public CompositeForeignKeyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _dbContext = _fixture.CreateDbContext();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task InsertingChunk_WithOwnerIdDisagreeingWithItsDocument_IsRejectedByCompositeForeignKey()
    {
        var trueOwnerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        _dbContext.Users.AddRange(
            NewUser(trueOwnerId, "composite-fk-true@test.documind"),
            NewUser(otherOwnerId, "composite-fk-other@test.documind"));

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = trueOwnerId,
            FileName = "composite-fk-test.pdf",
            PageCount = 1,
            UploadedAtUtc = DateTime.UtcNow
        };
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        var mismatchedChunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            OwnerId = otherOwnerId, // Disagrees with document.OwnerId on purpose — this is the case under test.
            PageNumber = 1,
            Ordinal = 0,
            Content = "A chunk claiming an owner its document does not have.",
            Embedding = new Vector(new float[DocuMindDbContext.EmbeddingDimensions])
        };
        _dbContext.DocumentChunks.Add(mismatchedChunk);

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    private static ApplicationUser NewUser(Guid id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
    };
}
