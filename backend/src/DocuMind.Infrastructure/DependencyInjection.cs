using System.ClientModel;
using Azure.AI.OpenAI;
using DocuMind.Application.Abstractions;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: EF Core + pgvector persistence, PDF text
/// extraction, chunking, and the Azure OpenAI embedding client.
/// </summary>
/// <remarks>
/// Registration never contacts Azure OpenAI or the database — clients and the DbContext are
/// constructed lazily and only make network calls when actually used, so DI composition succeeds
/// even without real credentials or a running database (e.g. at startup, in tests).
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));

        // Absent from appsettings.json on purpose: a connection string carries a password, and no
        // credential literal belongs in tracked configuration. Local development supplies it
        // through user-secrets, which stores it outside the working tree.
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing required configuration 'ConnectionStrings:Postgres'. Set it from "
                + "backend/src/DocuMind.Api with: dotnet user-secrets set "
                + "\"ConnectionStrings:Postgres\" \"Host=localhost;Port=5432;Database=<POSTGRES_DB>;"
                + "Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>\" — using the same values "
                + "as the .env file that Docker Compose reads. See .env.example.");

        services.AddDbContext<DocuMindDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddSingleton<ITextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<IChunker, FixedSizeChunker>();
        services.AddScoped<IChunkRepository, EfChunkRepository>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return new AzureOpenAIClient(new Uri(options.Endpoint), new ApiKeyCredential(options.ApiKey));
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var azureClient = sp.GetRequiredService<AzureOpenAIClient>();
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return azureClient.GetEmbeddingClient(options.EmbeddingDeployment).AsIEmbeddingGenerator();
        });

        return services;
    }
}
