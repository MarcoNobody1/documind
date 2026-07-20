using DocuMind.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Application;

/// <summary>
/// Composition root for the Application layer: use case handlers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UploadDocumentHandler>();

        return services;
    }
}
