using DocuMind.Application.Abstractions;
using UglyToad.PdfPig;

namespace DocuMind.Infrastructure.Text;

/// <summary>
/// Extracts per-page text from a PDF stream using PdfPig. Produces no text for scanned/image-only
/// pages (no OCR support) — see <see cref="ITextExtractor"/> for how the empty-text case is handled
/// upstream.
/// </summary>
public class PdfPigTextExtractor : ITextExtractor
{
    public Task<IReadOnlyList<PageText>> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(content);

        var pages = new List<PageText>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(new PageText(page.Number, page.Text));
        }

        return Task.FromResult<IReadOnlyList<PageText>>(pages);
    }
}
