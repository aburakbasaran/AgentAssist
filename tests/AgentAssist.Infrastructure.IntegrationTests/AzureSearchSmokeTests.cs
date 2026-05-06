using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace AgentAssist.Infrastructure.IntegrationTests;

/// <summary>
/// Live smoke tests for Azure AI Search. These tests run only when <c>AZURESEARCH__ENDPOINT</c> and <c>AZURESEARCH__INDEXNAME</c> environment variables are present; otherwise they call <see cref="Assert.Skip(string)"/> so the suite remains green in environments without Azure access.
/// </summary>
public sealed class AzureSearchSmokeTests
{
    [Fact]
    public async Task SearchClient_PingQuery_ExecutesSuccessfully()
    {
        var config = AzureConfigurationGuard.RequireOrSkip(
            "AZURESEARCH__ENDPOINT",
            "AZURESEARCH__INDEXNAME");
        var ct = TestContext.Current.CancellationToken;

        var client = new SearchClient(
            new Uri(config["AZURESEARCH__ENDPOINT"]),
            config["AZURESEARCH__INDEXNAME"],
            new DefaultAzureCredential());
        var response = await client.SearchAsync<SearchDocument>(
            "*",
            new SearchOptions { Size = 0 },
            ct);

        response.Value.Should().NotBeNull();
    }
}
