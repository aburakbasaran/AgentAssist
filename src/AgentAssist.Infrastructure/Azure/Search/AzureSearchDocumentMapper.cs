using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Explicit mapper from <see cref="AzureSearchDocument"/> wire records to the domain <see cref="RetrievedChunk"/>.
/// </summary>
public static class AzureSearchDocumentMapper
{
    /// <summary>
    /// Maps an Azure AI Search hit into a domain <see cref="RetrievedChunk"/>.
    /// </summary>
    /// <param name="document">The search index document.</param>
    /// <param name="rawScore">The raw search score returned by Azure; will be normalised to <c>[0,1]</c>.</param>
    /// <returns>A domain chunk.</returns>
    public static RetrievedChunk ToRetrievedChunk(AzureSearchDocument document, double rawScore)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new RetrievedChunk
        {
            DocumentId = document.DocumentId,
            ChunkId = document.ChunkId,
            Title = document.Title,
            Content = document.Content,
            AllowedRoles = document.AllowedRoles,
            DocumentType = ParseDocumentType(document.DocumentType),
            RiskLevel = ParseRiskClass(document.RiskLevel),
            Score = NormalizeScore(rawScore)
        };
    }

    /// <summary>
    /// Clamps a raw Azure AI Search score into the closed <c>[0,1]</c> interval used by the application.
    /// </summary>
    public static double NormalizeScore(double rawScore)
    {
        if (double.IsNaN(rawScore) || double.IsNegativeInfinity(rawScore))
        {
            return 0.0D;
        }

        if (double.IsPositiveInfinity(rawScore))
        {
            return 1.0D;
        }

        if (rawScore <= 0.0D)
        {
            return 0.0D;
        }

        // reason: Azure AI Search BM25 scores are unbounded; clamp via x/(1+x) to map (0,inf) → (0,1).
        return rawScore / (1.0D + rawScore);
    }

    private static DocumentType ParseDocumentType(string value) =>
        Enum.TryParse<DocumentType>(value, ignoreCase: true, out var result) ? result : DocumentType.Guidance;

    private static RiskClass ParseRiskClass(string value) =>
        Enum.TryParse<RiskClass>(value, ignoreCase: true, out var result) ? result : RiskClass.Low;
}
