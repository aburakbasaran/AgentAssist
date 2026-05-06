using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.Search;

namespace AgentAssist.Infrastructure.UnitTests;

public sealed class AzureSearchDocumentMapperTests
{
    [Fact]
    public void Map_PopulatesAllRetrievedChunkFields()
    {
        var document = new AzureSearchDocument
        {
            Id = "DOC-1::CHK-1",
            DocumentId = "DOC-1",
            ChunkId = "CHK-1",
            Title = "Title",
            Content = "Content",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = "Guidance",
            RiskLevel = "High",
            IsActive = true,
            Location = "branch-a",
            UpdatedAt = DateTimeOffset.UnixEpoch
        };

        var chunk = AzureSearchDocumentMapper.ToRetrievedChunk(document, rawScore: 4.5D);

        chunk.DocumentId.Should().Be("DOC-1");
        chunk.ChunkId.Should().Be("CHK-1");
        chunk.Title.Should().Be("Title");
        chunk.Content.Should().Be("Content");
        chunk.AllowedRoles.Should().BeEquivalentTo(new[] { "agent", "supervisor" });
        chunk.DocumentType.Should().Be(DocumentType.Guidance);
        chunk.RiskLevel.Should().Be(RiskClass.High);
        chunk.Score.Should().BeApproximately(0.818D, 0.01D);
    }

    [Fact]
    public void Map_NormalizesScoreBetweenZeroAndOne()
    {
        var document = CreateDocument();

        var chunk = AzureSearchDocumentMapper.ToRetrievedChunk(document, rawScore: 100D);

        chunk.Score.Should().BeGreaterThan(0.0D).And.BeLessThanOrEqualTo(1.0D);
    }

    [Theory]
    [InlineData(0.0D, 0.0D)]
    [InlineData(double.NaN, 0.0D)]
    [InlineData(double.NegativeInfinity, 0.0D)]
    [InlineData(double.PositiveInfinity, 1.0D)]
    [InlineData(-5.0D, 0.0D)]
    public void NormalizeScore_HandlesEdgeCases(double rawScore, double expected)
    {
        AzureSearchDocumentMapper.NormalizeScore(rawScore).Should().Be(expected);
    }

    [Fact]
    public void Map_UnknownDocumentType_DefaultsToGuidance()
    {
        var document = CreateDocument(documentType: "<<unknown>>");

        var chunk = AzureSearchDocumentMapper.ToRetrievedChunk(document, rawScore: 1.0D);

        chunk.DocumentType.Should().Be(DocumentType.Guidance);
    }

    [Fact]
    public void Map_UnknownRiskLevel_DefaultsToLow()
    {
        var document = CreateDocument(riskLevel: "<<unknown>>");

        var chunk = AzureSearchDocumentMapper.ToRetrievedChunk(document, rawScore: 1.0D);

        chunk.RiskLevel.Should().Be(RiskClass.Low);
    }

    private static AzureSearchDocument CreateDocument(
        string documentType = "Guidance",
        string riskLevel = "Low") => new()
    {
        Id = "DOC-1::CHK-1",
        DocumentId = "DOC-1",
        ChunkId = "CHK-1",
        Title = "Title",
        Content = "Content",
        AllowedRoles = ["agent"],
        DocumentType = documentType,
        RiskLevel = riskLevel,
        IsActive = true,
        Location = "branch-a",
        UpdatedAt = DateTimeOffset.UnixEpoch
    };
}
