using System.Reflection;

using AgentAssist.Application.Ai;
using AgentAssist.Domain.Exceptions;

namespace AgentAssist.Infrastructure.Ai;

internal sealed class EmbeddedResourcePromptProvider : IPromptProvider
{
    private const string ResourcePrefix = "AgentAssist.Infrastructure.Ai.Prompts.";
    private const string SystemMarker = "## system";
    private const string UserMarker = "## user";

    private readonly Assembly _assembly = typeof(EmbeddedResourcePromptProvider).Assembly;

    public async ValueTask<PromptTemplate> GetAsync(string templateId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resourceName = string.Concat(ResourcePrefix, templateId, ".md");
        await using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new PromptTemplateNotFoundException(templateId);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        return Parse(templateId, content);
    }

    private static PromptTemplate Parse(string templateId, string content)
    {
        var systemIndex = content.IndexOf(SystemMarker, StringComparison.OrdinalIgnoreCase);
        var userIndex = content.IndexOf(UserMarker, StringComparison.OrdinalIgnoreCase);

        if (systemIndex < 0 || userIndex < 0 || userIndex <= systemIndex)
        {
            throw new PromptTemplateNotFoundException(templateId);
        }

        var systemStart = systemIndex + SystemMarker.Length;
        var systemMessage = content[systemStart..userIndex].Trim();
        var userStart = userIndex + UserMarker.Length;
        var userMessageFormat = content[userStart..].Trim();

        return new PromptTemplate(templateId, systemMessage, userMessageFormat);
    }
}
