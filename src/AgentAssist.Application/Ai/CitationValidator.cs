using AgentAssist.Domain;

namespace AgentAssist.Application.Ai;

/// <summary>
/// Validates that every citation claimed by the model in <see cref="AssistantAnswerEnvelope.Citations"/> exists in the retrieved chunk whitelist. Empty citation lists on a non-refused envelope are also rejected.
/// </summary>
public static class CitationValidator
{
    /// <summary>
    /// Validates the claimed citations against the retrieved chunk whitelist.
    /// </summary>
    /// <param name="claimedCitations">Citation IDs claimed by the model.</param>
    /// <param name="retrievedChunks">Retrieved chunks that form the citation whitelist.</param>
    /// <returns>The validation outcome.</returns>
    public static CitationValidationResult Validate(
        IReadOnlyList<string> claimedCitations,
        IReadOnlyList<RetrievedChunk> retrievedChunks)
    {
        ArgumentNullException.ThrowIfNull(claimedCitations);
        ArgumentNullException.ThrowIfNull(retrievedChunks);

        if (claimedCitations.Count is 0)
        {
            return new CitationValidationResult(CitationValidationOutcome.Empty, []);
        }

        var whitelist = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in retrievedChunks)
        {
            whitelist.Add(chunk.ChunkId);
        }

        var unknown = new List<string>();
        foreach (var id in claimedCitations)
        {
            if (string.IsNullOrWhiteSpace(id) || !whitelist.Contains(id))
            {
                unknown.Add(id ?? string.Empty);
            }
        }

        return unknown.Count is 0
            ? new CitationValidationResult(CitationValidationOutcome.Valid, [])
            : new CitationValidationResult(CitationValidationOutcome.UnknownCitations, unknown);
    }
}

/// <summary>
/// Citation validation outcome categories.
/// </summary>
public enum CitationValidationOutcome
{
    /// <summary>Every citation exists in the retrieved chunk whitelist.</summary>
    Valid,

    /// <summary>The model returned a non-refused envelope with zero citations.</summary>
    Empty,

    /// <summary>The model returned at least one citation that is not in the retrieved chunk whitelist.</summary>
    UnknownCitations
}

/// <summary>
/// The result of validating a citation list against the retrieved chunk whitelist.
/// </summary>
/// <param name="Outcome">The validation outcome category.</param>
/// <param name="UnknownCitationIds">Citation IDs that were not in the whitelist (only populated when <paramref name="Outcome"/> is <see cref="CitationValidationOutcome.UnknownCitations"/>).</param>
public sealed record CitationValidationResult(
    CitationValidationOutcome Outcome,
    IReadOnlyList<string> UnknownCitationIds);
