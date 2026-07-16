using DocuMind.Domain.Entities;

namespace DocuMind.UnitTests.Domain;

public class DocumentTests
{
    [Fact]
    public void Document_WhenCreated_HoldsAssignedValues()
    {
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        var document = new Document
        {
            Id = id,
            FileName = "sample.pdf",
            PageCount = 12,
            UploadedAtUtc = uploadedAt
        };

        Assert.Equal(id, document.Id);
        Assert.Equal("sample.pdf", document.FileName);
        Assert.Equal(12, document.PageCount);
        Assert.Equal(uploadedAt, document.UploadedAtUtc);
    }
}
