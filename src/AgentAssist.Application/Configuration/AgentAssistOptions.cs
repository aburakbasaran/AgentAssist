using System.ComponentModel.DataAnnotations;

namespace AgentAssist.Application.Configuration;

/// <summary>
/// Represents strongly typed, vendor-neutral configuration for Agent Assist.
/// </summary>
public sealed class AgentAssistOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "AgentAssist";

    /// <summary>
    /// Gets or initializes the runtime mode.
    /// </summary>
    public required AgentAssistMode Mode { get; init; }

    /// <summary>
    /// Gets or initializes the minimum score for retrieved chunks.
    /// </summary>
    [Range(0.0D, 1.0D)]
    public required double MinChunkScore { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of chunks used for generation.
    /// </summary>
    [Range(1, 10)]
    public required int MaxRetrievedChunks { get; init; }

    /// <summary>
    /// Gets or initializes the per-call timeout for retrieval in seconds.
    /// </summary>
    [Range(1, 60)]
    public int RetrievalTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Gets or initializes the per-call timeout for chat generation in seconds.
    /// </summary>
    [Range(1, 120)]
    public int ChatTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Gets or initializes whether the header-based pilot user context provider may be registered. Defaults to <see langword="true"/>; production-like deployments override this to <see langword="false"/>.
    /// </summary>
    public bool AllowHeaderUserContext { get; init; } = true;
}
