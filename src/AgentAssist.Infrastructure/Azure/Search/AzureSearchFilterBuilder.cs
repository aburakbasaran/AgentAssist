using System.Globalization;
using System.Text;

using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Azure.Search;

/// <summary>
/// Safe OData filter builder for Azure AI Search. Inputs are filtered through deterministic allow-lists (<see cref="AzureSearchAllowList"/>) before composition; raw user-supplied strings never reach the OData expression.
/// </summary>
public static class AzureSearchFilterBuilder
{
    /// <summary>
    /// Builds an OData filter expression for the given user context.
    /// </summary>
    /// <param name="roles">Caller roles. Values not in <see cref="AzureSearchAllowList.Roles"/> are dropped.</param>
    /// <param name="documentType">Optional document type. Values not in <see cref="AzureSearchAllowList.DocumentTypes"/> are dropped.</param>
    /// <param name="location">Optional location. Values not in <see cref="AzureSearchAllowList.Locations"/> are dropped.</param>
    /// <returns>An OData filter expression that always begins with <c>isActive eq true</c>.</returns>
    public static string Build(
        IReadOnlyList<string> roles,
        DocumentType? documentType = null,
        string? location = null)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var clauses = new List<string>(capacity: 4)
        {
            "isActive eq true"
        };

        var allowedRoles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => AzureSearchAllowList.Roles.TryGetValue(role, out var canonical) ? canonical : null)
            .Where(role => role is not null)
            .Select(role => role!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (allowedRoles.Length > 0)
        {
            var rolesCsv = string.Join(",", allowedRoles);
            clauses.Add($"allowedRoles/any(r: search.in(r, '{EscapeOData(rolesCsv)}', ','))");
        }

        if (documentType is { } docType && AzureSearchAllowList.DocumentTypes.Contains(docType))
        {
            clauses.Add($"documentType eq '{EscapeOData(docType.ToString())}'");
        }

        if (!string.IsNullOrWhiteSpace(location)
            && AzureSearchAllowList.Locations.TryGetValue(location, out var canonicalLocation))
        {
            clauses.Add($"location eq '{EscapeOData(canonicalLocation)}'");
        }

        return string.Join(" and ", clauses);
    }

    /// <summary>
    /// Escapes a string for safe inclusion in an OData literal by doubling single quotes per the OData v4 grammar.
    /// </summary>
    public static string EscapeOData(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '\'')
            {
                _ = builder.Append("''");
                continue;
            }

            if (ch is < ' ')
            {
                _ = builder.Append(' ');
                continue;
            }

            _ = builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns the invariant-culture lowercased token used internally for allow-list lookups.
    /// </summary>
    public static string Normalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToLowerInvariant();

    /// <summary>
    /// Provided for diagnostics: returns the count of role entries that survived the allow-list filter.
    /// </summary>
    public static int CountAllowedRoles(IReadOnlyList<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var count = 0;
        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role) && AzureSearchAllowList.Roles.ContainsKey(role))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Provided for diagnostics: returns whether the supplied location is allow-listed.
    /// </summary>
    public static bool IsLocationAllowed(string? location) =>
        !string.IsNullOrWhiteSpace(location) && AzureSearchAllowList.Locations.ContainsKey(location);

    /// <summary>
    /// Provided for diagnostics: returns whether the document type is allow-listed.
    /// </summary>
    public static bool IsDocumentTypeAllowed(DocumentType documentType) =>
        AzureSearchAllowList.DocumentTypes.Contains(documentType);

    private static string CultureInvariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
