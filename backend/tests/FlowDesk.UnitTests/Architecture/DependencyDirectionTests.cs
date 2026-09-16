using System.Reflection;

namespace FlowDesk.UnitTests.Architecture;

/// <summary>
/// Guards the dependency direction recorded in ADR-0001:
/// Api/Worker → Application → Domain, and Infrastructure → Application/Domain.
/// </summary>
/// <remarks>
/// These tests are the reason the layering survives twenty-odd phases of
/// feature work. A failure here is an architecture regression, not a test
/// problem; fix the reference rather than the assertion.
/// </remarks>
public sealed class DependencyDirectionTests
{
    private const string Domain = "FlowDesk.Domain";
    private const string Application = "FlowDesk.Application";
    private const string Infrastructure = "FlowDesk.Infrastructure";
    private const string Api = "FlowDesk.Api";
    private const string Worker = "FlowDesk.Worker";

    /// <summary>
    /// Package name fragments that mark a dependency as an infrastructure
    /// concern. Application must stay clear of all of them.
    /// </summary>
    /// <remarks>
    /// EF Core itself is not on this list, and that is deliberate (ADR-0021).
    /// The application layer works against <c>IFlowDeskDbContext</c>, which
    /// returns <c>DbSet&lt;T&gt;</c>, so it needs the EF Core types. The line
    /// that is held instead is the database <em>provider</em>: Npgsql belongs
    /// to infrastructure, and a query that only runs on PostgreSQL has no
    /// business being written in a use case.
    ///
    /// Domain is stricter still and takes no packages at all.
    /// </remarks>
    private static readonly string[] InfrastructurePackageMarkers =
    [
        "Npgsql",
        "EntityFrameworkCore.Design",
        "StackExchange.Redis",
        "RabbitMQ",
        "Azure.Storage",
        "MailKit",
        "MimeKit",
        "Serilog",
        "OpenTelemetry",
        "Microsoft.AspNetCore",
    ];

    [Fact]
    public void Domain_declares_no_project_references()
    {
        var domain = SolutionLayout.ReadProject(Domain);

        Assert.Empty(domain.ProjectReferences);
    }

    [Fact]
    public void Domain_declares_no_package_or_framework_references()
    {
        var domain = SolutionLayout.ReadProject(Domain);

        Assert.Empty(domain.PackageReferences);
        Assert.Empty(domain.FrameworkReferences);
    }

    [Fact]
    public void Application_references_only_Domain()
    {
        var application = SolutionLayout.ReadProject(Application);

        Assert.Equal([Domain], application.ProjectReferences);
    }

    [Fact]
    public void Application_declares_no_infrastructure_packages()
    {
        var application = SolutionLayout.ReadProject(Application);

        var violations = application.PackageReferences
            .Where(IsInfrastructurePackage)
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// Guards the exception carved out in ADR-0021: the application layer may
    /// use EF Core, but never a database provider. Without this the carve-out
    /// would widen silently the first time someone needed a provider-specific
    /// helper in a use case.
    /// </summary>
    [Fact]
    public void Application_uses_EntityFrameworkCore_but_no_database_provider()
    {
        var application = SolutionLayout.ReadProject(Application);

        Assert.Contains("Microsoft.EntityFrameworkCore", application.PackageReferences);
        Assert.DoesNotContain(
            application.PackageReferences,
            package => package.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_does_not_reference_the_AspNetCore_shared_framework()
    {
        var application = SolutionLayout.ReadProject(Application);

        Assert.DoesNotContain("Microsoft.AspNetCore.App", application.FrameworkReferences);
    }

    [Fact]
    public void Infrastructure_references_only_Application_and_Domain()
    {
        var infrastructure = SolutionLayout.ReadProject(Infrastructure);

        Assert.Equal([Application, Domain], infrastructure.ProjectReferences);
    }

    /// <summary>
    /// Infrastructure implements contracts for the hosts; it must not depend on
    /// the web stack itself. Pulling in the ASP.NET Core shared framework here
    /// would let routing, MVC and HTTP concerns leak below the host boundary.
    /// </summary>
    [Fact]
    public void Infrastructure_does_not_reference_the_AspNetCore_shared_framework()
    {
        var infrastructure = SolutionLayout.ReadProject(Infrastructure);

        Assert.DoesNotContain("Microsoft.AspNetCore.App", infrastructure.FrameworkReferences);
    }

    [Theory]
    [InlineData(Api)]
    [InlineData(Worker)]
    public void Hosts_reference_Application_and_Infrastructure(string hostProjectName)
    {
        var host = SolutionLayout.ReadProject(hostProjectName);

        Assert.Contains(Application, host.ProjectReferences);
        Assert.Contains(Infrastructure, host.ProjectReferences);
    }

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    public void Inner_layers_do_not_reference_outer_layers(string innerProjectName)
    {
        var inner = SolutionLayout.ReadProject(innerProjectName);

        Assert.DoesNotContain(Infrastructure, inner.ProjectReferences);
        Assert.DoesNotContain(Api, inner.ProjectReferences);
        Assert.DoesNotContain(Worker, inner.ProjectReferences);
    }

    /// <summary>
    /// Complements the project-file checks: verifies that nothing pulled a
    /// forbidden assembly into Domain through a transitive path at compile time.
    /// </summary>
    [Fact]
    public void Compiled_Domain_assembly_references_no_other_FlowDesk_assembly()
    {
        var domainAssembly = Assembly.Load(Domain);

        var flowDeskReferences = domainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("FlowDesk.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(flowDeskReferences);
    }

    private static bool IsInfrastructurePackage(string packageName) =>
        InfrastructurePackageMarkers.Any(marker =>
            packageName.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
