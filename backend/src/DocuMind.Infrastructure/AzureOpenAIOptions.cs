namespace DocuMind.Infrastructure;

/// <summary>
/// Configuration for the Azure OpenAI resource used for embeddings (and, in Slice B, chat).
/// Bound from the <c>AzureOpenAI</c> configuration section. Real secrets are supplied via
/// <c>dotnet user-secrets</c> locally or environment variables in deployed environments — never
/// committed to source control.
/// </summary>
public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingDeployment { get; set; } = string.Empty;

    /// <summary>Chat deployment name; used starting in Slice B.</summary>
    public string ChatDeployment { get; set; } = string.Empty;
}
