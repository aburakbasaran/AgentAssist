using AgentAssist.Domain;

namespace AgentAssist.Domain.UnitTests;

public sealed class CitationTests
{
    [Fact]
    public void Citation_SameValues_AreEqual()
    {
        var first = new Citation
        {
            DocumentId = "DOC-001",
            ChunkId = "CHK-001",
            Title = "Title"
        };
        var second = new Citation
        {
            DocumentId = "DOC-001",
            ChunkId = "CHK-001",
            Title = "Title"
        };

        first.Should().Be(second);
    }
}
