using AgentAssist.Application.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AgentAssist.Testing;

/// <summary>
/// Central configuration for evaluation and integration test hosts. When <c>EVAL_MODE</c> is unset, forces Mock mode
/// so user-secrets DevCloud settings cannot leak into CI. When <c>EVAL_MODE=DevCloud</c>, applies environment-variable
/// overrides on top of user-secrets (env wins per key).
/// </summary>
public static class EvalHostConfiguration
{
    /// <summary>
    /// Environment variable that selects the test host mode. Unset or any value other than <c>DevCloud</c> → <see cref="EvalHostMode.Mock"/>.
    /// </summary>
    public const string EvalModeEnvironmentVariable = "EVAL_MODE";

    private static readonly string[] DevCloudOverlayKeys =
    [
        $"{AgentAssistOptions.SectionName}:Mode",
        "AzureSearch:Endpoint",
        "AzureSearch:IndexName",
        "AzureSearch:SemanticConfigurationName",
        "AzureSearch:VectorFieldName",
        "AzureOpenAI:Endpoint",
        "AzureOpenAI:ChatDeploymentName",
        "AzureOpenAI:EmbeddingDeploymentName",
        "AzureSql:ConnectionString"
    ];

    /// <summary>
    /// Resolves the active test host mode from <see cref="EvalModeEnvironmentVariable"/>.
    /// </summary>
    public static EvalHostMode ResolveMode()
    {
        var raw = Environment.GetEnvironmentVariable(EvalModeEnvironmentVariable);
        return string.Equals(raw, nameof(EvalHostMode.DevCloud), StringComparison.OrdinalIgnoreCase)
            ? EvalHostMode.DevCloud
            : EvalHostMode.Mock;
    }

    /// <summary>
    /// Configures the web host builder for test factories. Call from <see cref="AgentAssistWebApplicationFactory"/>.
    /// </summary>
    public static void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var mode = ResolveMode();
        _ = builder.UseEnvironment(Environments.Development);

        // reason: host settings are merged before appsettings/user-secrets during WebApplication startup,
        // so service registration (AddMock vs AddDevCloud) sees the intended mode—not only IOptions at resolve time.
        _ = builder.UseSetting($"{AgentAssistOptions.SectionName}:Mode", mode.ToString());

        _ = builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            if (mode is EvalHostMode.Mock)
            {
                configBuilder.AddInMemoryCollection(CreateMockOverrides());
                return;
            }

            configBuilder.AddInMemoryCollection(BuildDevCloudEnvironmentOverlay());
        });
    }

    private static Dictionary<string, string?> CreateMockOverrides() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [$"{AgentAssistOptions.SectionName}:Mode"] = nameof(AgentAssistMode.Mock)
        };

    private static Dictionary<string, string?> BuildDevCloudEnvironmentOverlay()
    {
        var overlay = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [$"{AgentAssistOptions.SectionName}:Mode"] = nameof(AgentAssistMode.DevCloud)
        };

        foreach (var configKey in DevCloudOverlayKeys)
        {
            if (configKey.EndsWith(":Mode", StringComparison.Ordinal))
            {
                continue;
            }

            var envName = ToEnvironmentVariableName(configKey);
            var value = Environment.GetEnvironmentVariable(envName);
            if (value is not null)
            {
                overlay[configKey] = value;
            }
        }

        return overlay;
    }

    private static string ToEnvironmentVariableName(string configKey) =>
        configKey.Replace(":", "__", StringComparison.Ordinal);
}
