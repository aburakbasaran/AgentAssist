namespace AgentAssist.Infrastructure.IntegrationTests;

/// <summary>
/// Helper that skips integration tests when the required Azure configuration values are absent. Configuration is read from environment variables to keep secrets out of <c>appsettings.json</c>; the local development convention is to set them via <c>dotnet user-secrets</c> exported as env vars or via the host shell.
/// </summary>
internal static class AzureConfigurationGuard
{
    /// <summary>
    /// Reads an environment variable, returns the trimmed value when present, or <see langword="null"/>.
    /// </summary>
    public static string? Read(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Skips the current test when any required environment variable is missing.
    /// </summary>
    /// <param name="variables">Names of the required environment variables.</param>
    public static IReadOnlyDictionary<string, string> RequireOrSkip(params string[] variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var collected = new Dictionary<string, string>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var name in variables)
        {
            var value = Read(name);
            if (value is null)
            {
                missing.Add(name);
            }
            else
            {
                collected[name] = value;
            }
        }

        if (missing.Count > 0)
        {
            Assert.Skip($"Azure integration test skipped; missing config: {string.Join(", ", missing)}.");
        }

        return collected;
    }
}
