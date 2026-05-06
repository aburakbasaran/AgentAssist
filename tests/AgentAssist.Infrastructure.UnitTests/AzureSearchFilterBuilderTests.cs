using AgentAssist.Domain;
using AgentAssist.Infrastructure.Azure.Search;

namespace AgentAssist.Infrastructure.UnitTests;

public sealed class AzureSearchFilterBuilderTests
{
    [Fact]
    public void Build_WithNoFilters_AlwaysIncludesIsActiveTrue()
    {
        var filter = AzureSearchFilterBuilder.Build([]);

        filter.Should().StartWith("isActive eq true");
    }

    [Fact]
    public void Build_WithRoles_AddsAnyClause()
    {
        var filter = AzureSearchFilterBuilder.Build(["agent", "supervisor"]);

        filter.Should().Contain("allowedRoles/any(r: search.in(r, 'agent,supervisor', ','))");
    }

    [Fact]
    public void Build_WithDocumentType_AddsEqClause()
    {
        var filter = AzureSearchFilterBuilder.Build(["agent"], DocumentType.Guidance);

        filter.Should().Contain("documentType eq 'Guidance'");
    }

    [Fact]
    public void Build_WithLocation_AddsEqClause()
    {
        var filter = AzureSearchFilterBuilder.Build(["agent"], location: "branch-a");

        filter.Should().Contain("location eq 'branch-a'");
    }

    [Fact]
    public void Build_RequiresIsActiveTrue()
    {
        var filter = AzureSearchFilterBuilder.Build(["agent"], DocumentType.Guidance, "branch-a");

        filter.Should().StartWith("isActive eq true");
    }

    [Theory]
    [InlineData("agent'; --")]
    [InlineData("' or 1 eq 1 --")]
    [InlineData("' or '1' eq '1")]
    [InlineData("administrator")]
    [InlineData("admin")]
    public void Build_AllowListBypassRoleAttempt_IsDroppedFromFilter(string maliciousRole)
    {
        var filter = AzureSearchFilterBuilder.Build([maliciousRole]);

        filter.Should().NotContain(maliciousRole);
        filter.Should().NotContain("or 1 eq 1");
        filter.Should().NotContain("--");
    }

    [Fact]
    public void Build_EmptyRole_IsDroppedSilently()
    {
        var filter = AzureSearchFilterBuilder.Build([string.Empty]);

        filter.Should().Be("isActive eq true");
    }

    [Theory]
    [InlineData("branch-x")]
    [InlineData("'; drop")]
    [InlineData("hq")]
    public void Build_AllowListBypassLocation_IsDroppedFromFilter(string maliciousLocation)
    {
        var filter = AzureSearchFilterBuilder.Build(["agent"], location: maliciousLocation);

        filter.Should().NotContain("location eq");
    }

    [Fact]
    public void Build_VeryLongRoleString_IsDroppedSilently()
    {
        var longRole = new string('a', 5000);

        var filter = AzureSearchFilterBuilder.Build([longRole]);

        filter.Should().NotContain(longRole);
    }

    [Fact]
    public void Build_UnicodeRoleNotInAllowList_IsDroppedSilently()
    {
        var filter = AzureSearchFilterBuilder.Build(["yöneticı", "管理者"]);

        filter.Should().Be("isActive eq true");
    }

    [Fact]
    public void Build_DuplicateAllowedRoles_AreCollapsedAndOrderedDeterministically()
    {
        var filter = AzureSearchFilterBuilder.Build(["agent", "Agent", "agent"]);

        filter.Should().Contain("'agent'");
        filter.Should().NotContain("agent,agent");
    }

    [Theory]
    [InlineData("'", "''")]
    [InlineData("a'b", "a''b")]
    [InlineData("normal", "normal")]
    public void EscapeOData_DoublesSingleQuotes(string input, string expected)
    {
        var actual = AzureSearchFilterBuilder.EscapeOData(input);

        actual.Should().Be(expected);
    }

    [Fact]
    public void EscapeOData_ControlCharactersAreReplaced()
    {
        var input = "value\u0001\u0002";

        var actual = AzureSearchFilterBuilder.EscapeOData(input);

        actual.Should().Be("value  ");
    }

    [Fact]
    public void IsLocationAllowed_ForUnknownLocation_ReturnsFalse()
    {
        AzureSearchFilterBuilder.IsLocationAllowed("branch-x").Should().BeFalse();
    }

    [Fact]
    public void IsDocumentTypeAllowed_ForKnownEnum_ReturnsTrue()
    {
        AzureSearchFilterBuilder.IsDocumentTypeAllowed(DocumentType.Procedure).Should().BeTrue();
    }
}
