using System.Text.Json;

using AgentAssist.Infrastructure.Azure.Search;
using AgentAssist.Testing;

using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Evaluation.Tests.DevCloud;

/// <summary>
/// One-shot index seeding for the golden pilot corpus. Run with EVAL_MODE=DevCloud before Layer 1.
/// </summary>
public sealed class GoldenPilotIndexUploadTests
{
    [Fact]
    public async Task GoldenPilot_UploadKnowledgeDocuments_ToAzureSearch()
    {
        if (EvalHostConfiguration.ResolveMode() is not EvalHostMode.DevCloud)
        {
            Assert.Skip("Set EVAL_MODE=DevCloud to upload golden pilot documents.");
        }

        var ct = TestContext.Current.CancellationToken;
        var path = ResolveDataPath("golden-pilot-knowledge.json");
        var payload = await File.ReadAllTextAsync(path, ct);
        using var document = JsonDocument.Parse(payload);
        var actions = new List<IndexDocumentsAction<AzureSearchDocument>>();

        foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
        {
            var doc = JsonSerializer.Deserialize<AzureSearchDocument>(item.GetRawText());
            if (doc is not null)
            {
                actions.Add(IndexDocumentsAction.Upload(doc));
            }
        }

        using var factory = new AgentAssistWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var searchClient = scope.ServiceProvider.GetRequiredService<SearchClient>();

        // Remove pre-pilot single-doc seed (duplicate CHK-001 noise).
        var legacyDelete = IndexDocumentsBatch.Create(
            IndexDocumentsAction.Delete(new AzureSearchDocument { Id = "acme-mr-prep-001" }));
        await searchClient.IndexDocumentsAsync(legacyDelete, cancellationToken: ct);

        var batch = IndexDocumentsBatch.Create(actions.ToArray());
        var response = await searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);

        response.Value.Results.Should().OnlyContain(r => r.Succeeded, "all golden pilot documents must upload successfully");
    }

    private static string ResolveDataPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 12 && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, "test", "data", "azure-search", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate test/data/azure-search/{fileName} from {AppContext.BaseDirectory}.");
    }
}
