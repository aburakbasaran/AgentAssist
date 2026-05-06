using AgentAssist.Infrastructure.Identity;

using Microsoft.AspNetCore.Http;

namespace AgentAssist.Infrastructure.UnitTests;

public sealed class HeaderUserContextProviderTests
{
    [Fact]
    public void HeaderUserContext_ParsesXAgentHeaders_PopulatesUserAndRolesAndLocation()
    {
        var accessor = CreateAccessor(headers => headers.AddRange(new (string, string)[]
        {
            (HeaderUserContextProvider.UserHeader, "pilot-user"),
            (HeaderUserContextProvider.RolesHeader, "agent,supervisor"),
            (HeaderUserContextProvider.LocationHeader, "branch-a")
        }));

        var provider = new HeaderUserContextProvider(accessor);

        provider.UserId.Should().Be("pilot-user");
        provider.Roles.Should().BeEquivalentTo(new[] { "agent", "supervisor" });
        provider.Location.Should().Be("branch-a");
        provider.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void HeaderUserContext_MissingHeaders_ReturnsAnonDefaults()
    {
        var accessor = CreateAccessor(_ => { });
        var provider = new HeaderUserContextProvider(accessor);

        provider.UserId.Should().BeNull();
        provider.Roles.Should().BeEquivalentTo(new[] { "anon" });
        provider.Location.Should().BeNull();
    }

    [Fact]
    public void HeaderUserContext_InvalidRoleCharacters_AreRejected()
    {
        var accessor = CreateAccessor(headers => headers.Add((HeaderUserContextProvider.RolesHeader, "agent,role with spaces,bad'role")));
        var provider = new HeaderUserContextProvider(accessor);

        provider.Roles.Should().BeEquivalentTo(new[] { "agent" });
    }

    [Fact]
    public void HeaderUserContext_OverlongHeader_IsRejected()
    {
        var longRole = new string('x', 1000);
        var accessor = CreateAccessor(headers => headers.Add((HeaderUserContextProvider.RolesHeader, longRole)));
        var provider = new HeaderUserContextProvider(accessor);

        provider.Roles.Should().BeEquivalentTo(new[] { "anon" });
    }

    [Fact]
    public void HeaderUserContext_RoleNotInAllowList_IsDroppedAtHeaderBoundary()
    {
        var accessor = CreateAccessor(headers => headers.Add((HeaderUserContextProvider.RolesHeader, "agent,administrator,super-admin,supervisor")));
        var provider = new HeaderUserContextProvider(accessor);

        provider.Roles.Should().BeEquivalentTo(new[] { "agent", "supervisor" });
    }

    [Fact]
    public void HeaderUserContext_AllRolesUnknown_FallsBackToAnon()
    {
        var accessor = CreateAccessor(headers => headers.Add((HeaderUserContextProvider.RolesHeader, "administrator,owner,root")));
        var provider = new HeaderUserContextProvider(accessor);

        provider.Roles.Should().BeEquivalentTo(new[] { "anon" });
    }

    [Fact]
    public void HeaderUserContext_NullHttpContext_ReturnsAnonDefaults()
    {
        var accessor = new HttpContextAccessor();
        var provider = new HeaderUserContextProvider(accessor);

        provider.UserId.Should().BeNull();
        provider.Roles.Should().BeEquivalentTo(new[] { "anon" });
    }

    [Fact]
    public void MockUserContextProvider_ReturnsFixedDefaults()
    {
        var provider = new MockUserContextProvider();

        provider.UserId.Should().Be("pilot-user");
        provider.Roles.Should().BeEquivalentTo(new[] { "agent" });
        provider.Location.Should().Be("branch-a");
        provider.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void AnonymousUserContextProvider_ReturnsDenyByDefault()
    {
        var provider = new AnonymousUserContextProvider();

        provider.UserId.Should().BeNull();
        provider.Roles.Should().BeEquivalentTo(new[] { "anon" });
        provider.Location.Should().BeNull();
        provider.IsAuthenticated.Should().BeFalse();
    }

    private static IHttpContextAccessor CreateAccessor(Action<List<(string Name, string Value)>> setHeaders)
    {
        var headers = new List<(string Name, string Value)>();
        setHeaders(headers);

        var context = new DefaultHttpContext();
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return new HttpContextAccessor { HttpContext = context };
    }
}

internal static class HeaderListExtensions
{
    public static void AddRange(this List<(string Name, string Value)> list, IEnumerable<(string Name, string Value)> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            list.Add(item);
        }
    }
}
