namespace AgentAssist.Api.Contracts;

internal sealed record AssistantQueryRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Question { get; init; }

    public string? UserId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = ["agent"];
}
