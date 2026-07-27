using DocuMind.Application.Abstractions;
using DocuMind.Application.UseCases;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Application;

/// <summary>
/// Composition root for the Application layer: use case handlers.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Default number of chunks retrieved per question when <c>Retrieval:TopK</c> is not
    /// configured. Callers should prefer passing the configured value.
    /// </summary>
    public const int DefaultRetrievalTopK = 5;

    /// <summary>
    /// Registers Application use case handlers.
    /// </summary>
    /// <param name="retrievalTopK">
    /// The number of chunks <see cref="AskQuestionHandler"/> retrieves per question. The
    /// Application layer takes this as a plain value rather than reading configuration itself,
    /// so it stays unaware of the configuration source; the composition root (Api's
    /// <c>Program.cs</c>) is what reads <c>Retrieval:TopK</c> from <c>appsettings.json</c>.
    /// </param>
    public static IServiceCollection AddApplication(this IServiceCollection services, int retrievalTopK = DefaultRetrievalTopK)
    {
        services.AddScoped<UploadDocumentHandler>();

        services.AddScoped(sp => new AskQuestionHandler(
            sp.GetRequiredService<IChunkRepository>(),
            sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            sp.GetRequiredService<IChatClient>(),
            retrievalTopK));

        return services;
    }
}
