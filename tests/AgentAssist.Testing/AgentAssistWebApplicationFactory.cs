using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentAssist.Testing;

/// <summary>
/// Test host for <see cref="Program"/> that applies <see cref="EvalHostConfiguration"/> (Mock by default).
/// </summary>
public sealed class AgentAssistWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        EvalHostConfiguration.ConfigureWebHost(builder);
    }
}
