using AgentAssist.Application.Ai;
using AgentAssist.Domain;

namespace AgentAssist.Application.UnitTests;

public sealed class CitationValidatorTests
{
    [Fact]
    public void CitationValidator_AllCitationsMustExistInRetrievedChunks()
    {
        var retrieved = new[] { CreateChunk("CHK-1"), CreateChunk("CHK-2") };

        var result = CitationValidator.Validate(["CHK-1"], retrieved);

        result.Outcome.Should().Be(CitationValidationOutcome.Valid);
    }

    [Fact]
    public void CitationValidator_EmptyCitations_ReturnsEmptyOutcome()
    {
        var retrieved = new[] { CreateChunk("CHK-1") };

        var result = CitationValidator.Validate([], retrieved);

        result.Outcome.Should().Be(CitationValidationOutcome.Empty);
    }

    [Fact]
    public void CitationValidator_UnknownCitation_ReturnsUnknownOutcome()
    {
        var retrieved = new[] { CreateChunk("CHK-1") };

        var result = CitationValidator.Validate(["CHK-2"], retrieved);

        result.Outcome.Should().Be(CitationValidationOutcome.UnknownCitations);
        result.UnknownCitationIds.Should().ContainSingle().Which.Should().Be("CHK-2");
    }

    [Fact]
    public void CitationValidator_MixedKnownAndUnknown_FlagsUnknown()
    {
        var retrieved = new[] { CreateChunk("CHK-1") };

        var result = CitationValidator.Validate(["CHK-1", "CHK-X"], retrieved);

        result.Outcome.Should().Be(CitationValidationOutcome.UnknownCitations);
        result.UnknownCitationIds.Should().ContainSingle().Which.Should().Be("CHK-X");
    }

    [Fact]
    public void CitationValidator_WhitespaceCitation_FlagsUnknown()
    {
        var retrieved = new[] { CreateChunk("CHK-1") };

        var result = CitationValidator.Validate(["   "], retrieved);

        result.Outcome.Should().Be(CitationValidationOutcome.UnknownCitations);
    }

    private static RetrievedChunk CreateChunk(string chunkId) => new()
    {
        DocumentId = "DOC-1",
        ChunkId = chunkId,
        Title = "Title",
        Content = "Content",
        AllowedRoles = ["agent"],
        DocumentType = DocumentType.Guidance,
        RiskLevel = RiskClass.Low,
        Score = 0.9D
    };
}
