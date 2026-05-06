using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Deterministic allow-lists used by <see cref="AzureSearchFilterBuilder"/>. Any value submitted by the caller that is not in these lists is silently dropped before the OData filter is built; raw user-supplied strings never reach the OData filter.
/// </summary>
public static class AzureSearchAllowList
{
    /// <summary>
    /// Allowed role values. Keys are the lower-case input form; values are the canonical token used in the OData filter.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["agent"] = "agent",
        ["supervisor"] = "supervisor"
    };

    /// <summary>
    /// Allowed location values. Keys are lower-case input; values are the canonical token used in the OData filter.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Locations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["branch-a"] = "branch-a",
        ["branch-b"] = "branch-b",
        ["branch-c"] = "branch-c"
    };

    /// <summary>
    /// Allowed document type values, mapped from the domain enum so callers cannot inject arbitrary strings.
    /// </summary>
    public static readonly IReadOnlySet<DocumentType> DocumentTypes = new HashSet<DocumentType>
    {
        DocumentType.Procedure,
        DocumentType.Campaign,
        DocumentType.Guidance,
        DocumentType.Administrative
    };
}
