using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentAssist.Testing;

public sealed class EvalHostConfigurationTests
{
    [Fact]
    public void ResolveMode_WhenEvalModeUnset_ReturnsMock()
    {
        var previous = Environment.GetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable, null);
            EvalHostConfiguration.ResolveMode().Should().Be(EvalHostMode.Mock);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void HostedOptions_WhenEvalModeUnset_ForcesMockDespiteUserSecrets()
    {
        var previousEval = Environment.GetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable);
        var previousAgentMode = Environment.GetEnvironmentVariable("AgentAssist__Mode");
        try
        {
            Environment.SetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable, null);
            Environment.SetEnvironmentVariable("AgentAssist__Mode", null);

            using var factory = new AgentAssistWebApplicationFactory();
            using var scope = factory.Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<AgentAssistOptions>>().Value;

            options.Mode.Should().Be(AgentAssistMode.Mock,
                "unset EVAL_MODE must force Mock so CI and local user-secrets DevCloud cannot override");

            var search = scope.ServiceProvider.GetRequiredService<IKnowledgeSearchService>();
            search.GetType().Name.Should().Be("MockKnowledgeSearchService",
                "startup service registration must use Mock adapters, not DevCloud Azure Search");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EvalHostConfiguration.EvalModeEnvironmentVariable, previousEval);
            Environment.SetEnvironmentVariable("AgentAssist__Mode", previousAgentMode);
        }
    }
}
