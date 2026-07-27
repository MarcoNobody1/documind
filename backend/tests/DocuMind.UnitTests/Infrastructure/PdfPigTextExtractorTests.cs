using System.Text;
using DocuMind.Application.Exceptions;
using DocuMind.Infrastructure.Text;

namespace DocuMind.UnitTests.Infrastructure;

public class PdfPigTextExtractorTests
{
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public async Task ExtractAsync_SinglePageFixture_ReturnsOnePageTextWithPageNumberOne()
    {
        var extractor = new PdfPigTextExtractor();

        await using var stream = OpenFixture("handbook-test.pdf");
        var pages = await extractor.ExtractAsync(stream);

        Assert.Single(pages);
        Assert.Equal(1, pages[0].PageNumber);
    }

    [Fact]
    public async Task ExtractAsync_HandbookFixture_ContainsKnownVacationPolicyText()
    {
        var extractor = new PdfPigTextExtractor();

        await using var stream = OpenFixture("handbook-test.pdf");
        var pages = await extractor.ExtractAsync(stream);

        var text = string.Join(" ", pages.Select(page => page.Text));
        Assert.Contains("entitled to 22 days of paid vacation per year", text);
        Assert.Contains("carried over until March 31", text);
        Assert.Contains("at least 14 days in advance", text);
    }

    [Fact]
    public async Task ExtractAsync_ParentalLeaveFixture_ContainsKnownLeavePolicyText()
    {
        var extractor = new PdfPigTextExtractor();

        await using var stream = OpenFixture("policy-leave.pdf");
        var pages = await extractor.ExtractAsync(stream);

        var text = string.Join(" ", pages.Select(page => page.Text));
        Assert.Contains("16 weeks of paid", text);
        Assert.Contains("60 days before the intended", text);
    }

    [Fact]
    public async Task ExtractAsync_ExpensesFixture_ContainsKnownExpensePolicyText()
    {
        var extractor = new PdfPigTextExtractor();

        await using var stream = OpenFixture("policy-expenses.pdf");
        var pages = await extractor.ExtractAsync(stream);

        var text = string.Join(" ", pages.Select(page => page.Text));
        Assert.Contains("within 30 days of purchase", text);
        Assert.Contains("up to 45 euros per day", text);
    }

    [Fact]
    public async Task ExtractAsync_CorruptNonPdfStream_ThrowsInvalidDocumentException()
    {
        // This is the real, current contract (see DocumentsEndpoints.UploadDocumentAsync, which
        // catches exactly this exception type to return HTTP 400): a stream PdfPig cannot parse
        // must surface as `InvalidDocumentException`, never as a raw PdfPig/parser exception or
        // a silent empty result.
        var extractor = new PdfPigTextExtractor();
        using var garbage = new MemoryStream(Encoding.UTF8.GetBytes("this is not a PDF file at all"));

        var ex = await Assert.ThrowsAsync<InvalidDocumentException>(
            () => extractor.ExtractAsync(garbage));

        Assert.NotNull(ex.InnerException);
    }

    private static FileStream OpenFixture(string fileName)
        => File.OpenRead(Path.Combine(FixturesDirectory, fileName));
}
