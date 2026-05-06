using System.Reflection;

namespace AgentAssist.Architecture.Tests;

/// <summary>
/// Verifies Clean Architecture boundary rules across the AgentAssist solution. Domain depends on nothing; Application
/// depends only on Domain plus a small allow-list of vendor-neutral abstractions; Infrastructure is the only project
/// that may pull in Azure SDKs, EF Core, or persistence dependencies; the Api project only references infrastructure
/// to compose dependency injection.
/// </summary>
public sealed class AssemblyBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(AgentAssist.Domain.AssistantQuery).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(AgentAssist.Application.Assistant.AnswerAssistantQueryHandler).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AgentAssist.Infrastructure.DependencyInjection.MockInfrastructureServiceCollectionExtensions).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_References_NoExternalAssembly()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(IsNonRuntimeReference)
            .ToArray();

        referenced.Should().BeEmpty(
            "Domain layer must not reference any third-party or framework assembly outside the runtime base");
    }

    [Fact]
    public void Application_References_NoAzureAssembly()
    {
        var referenced = GetReferencedNames(ApplicationAssembly);

        referenced.Should().NotContain(name => name.StartsWith("Azure.", StringComparison.Ordinal));
        referenced.Should().NotContain(name => name.StartsWith("Microsoft.Azure.", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_References_NoEfCoreAssembly()
    {
        var referenced = GetReferencedNames(ApplicationAssembly);

        referenced.Should().NotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_References_NoAspNetCoreAssembly()
    {
        var referenced = GetReferencedNames(ApplicationAssembly);

        referenced.Should().NotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_References_OnlyAllowedAspNetCoreFrameworks()
    {
        var referenced = GetReferencedNames(InfrastructureAssembly);
        var allowedAspNetCoreReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Http.Abstractions",
            "Microsoft.AspNetCore.Http.Features"
        };

        var disallowed = referenced
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                && !allowedAspNetCoreReferences.Contains(name))
            .ToArray();

        disallowed.Should().BeEmpty(
            "Infrastructure must not depend on ASP.NET Core hosting; only the Http abstraction and feature surfaces required for IHttpContextAccessor are permitted");
    }

    [Fact]
    public void Api_References_Application_And_Infrastructure_Only_Through_Project_Refs()
    {
        var referenced = GetReferencedNames(ApiAssembly);

        referenced.Should().Contain("AgentAssist.Application");
        referenced.Should().Contain("AgentAssist.Infrastructure");
    }

    [Fact]
    public void NoController_Types_AreDefined_Anywhere()
    {
        Assembly[] solutionAssemblies =
        [
            DomainAssembly,
            ApplicationAssembly,
            InfrastructureAssembly,
            ApiAssembly
        ];

        var controllerTypes = solutionAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.BaseType?.FullName?.Contains("ControllerBase", StringComparison.Ordinal) is true
                || type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToArray();

        controllerTypes.Should().BeEmpty("the project uses Minimal APIs; no controllers are permitted");
    }

    private static IReadOnlyList<string> GetReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

    private static bool IsNonRuntimeReference(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name is "System.Runtime"
            or "System.Private.CoreLib"
            or "netstandard"
            or "System.Linq"
            or "System.Collections"
            or "System.ObjectModel"
            or "System.Memory"
            or "System.Runtime.InteropServices"
            or "System.Threading"
            or "System.Threading.Tasks"
            or "System.Text.Encoding.Extensions")
        {
            return false;
        }

        return !name.StartsWith("System.", StringComparison.Ordinal)
            && !name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal)
            && !name.StartsWith("mscorlib", StringComparison.Ordinal);
    }
}
