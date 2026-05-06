using AgentAssist.Application.Ai;
using AgentAssist.Domain.Exceptions;
using AgentAssist.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Infrastructure.UnitTests;

public sealed class PromptProviderTests
{
    [Fact]
    public async Task PromptProvider_KnownTemplate_ReturnsTemplate()
    {
        var provider = CreateProvider();

        var template = await provider.GetAsync("assistant.answer.v1", CancellationToken.None);

        template.TemplateId.Should().Be("assistant.answer.v1");
        template.SystemMessage.Should().Contain("citations");
        template.SystemMessage.Should().Contain("citation-first");
        template.UserMessageFormat.Should().Contain("{{question}}");
        template.UserMessageFormat.Should().Contain("{{retrievedChunks}}");
    }

    [Fact]
    public async Task PromptProvider_UnknownTemplate_ThrowsDomainException()
    {
        var provider = CreateProvider();

        var act = async () => await provider.GetAsync("missing.template", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    private static IPromptProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddMockInfrastructure();

        return services.BuildServiceProvider().GetRequiredService<IPromptProvider>();
    }
}
