using System.Text.Json.Serialization;

namespace AgentAssist.Api.Contracts;

/// <summary>
/// Strict request body contract for <c>POST /api/v1/assistant/query</c>. Identity (user / roles / location) is sourced from the <see cref="AgentAssist.Application.Abstractions.IUserContextProvider"/>, NOT the request body, so any unmapped fields are rejected with HTTP 400 + a problem detail explaining the authentication-context boundary (see ADR-0010).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AssistantQueryRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Question { get; init; }
}
