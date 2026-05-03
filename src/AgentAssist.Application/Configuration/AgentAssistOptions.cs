using System.ComponentModel.DataAnnotations;

namespace AgentAssist.Application.Configuration;

/// <summary>
/// Represents strongly typed configuration for Agent Assist.
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
}
