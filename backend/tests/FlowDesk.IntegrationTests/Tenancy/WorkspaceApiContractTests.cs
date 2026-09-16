using System.Net;
using System.Text.Json;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tenancy;

/// <summary>
/// Guards the wire format promised in docs/API_CONVENTIONS.md.
/// </summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class WorkspaceApiContractTests
{
    private readonly PostgresContainerFixture _postgres;

    public WorkspaceApiContractTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Enums are serialised by name, never by number.
    /// </summary>
    /// <remarks>
    /// The framework default is the numeric value, which ties the contract to
    /// declaration order: inserting a role in the middle of the enum would
    /// silently change what every previously issued value means. This test
    /// exists because that regression is invisible until a client misreads a
    /// role — and a misread role is an authorisation display bug.
    /// </remarks>
    [Fact]
    public async Task Roles_are_serialised_by_name()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var response = await WorkspaceTestClient.CreateAsync(
            owner.Client,
            "Acme Teknoloji",
            Cancellation,
            WorkspaceTestClient.UniqueSlug());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(Cancellation);
        using var document = JsonDocument.Parse(body);

        var role = document.RootElement.GetProperty("role");

        Assert.Equal(JsonValueKind.String, role.ValueKind);
        Assert.Equal("Owner", role.GetString());
    }

    /// <summary>
    /// A Turkish workspace name must produce a usable ASCII address without the
    /// user having to invent one.
    /// </summary>
    [Fact]
    public async Task A_turkish_name_yields_a_transliterated_slug()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        var uniqueName = $"Kuzey Yazılım {Guid.CreateVersion7():N}"[..30];

        using var response = await WorkspaceTestClient.CreateAsync(owner.Client, uniqueName, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(Cancellation);
        using var document = JsonDocument.Parse(body);
        var slug = document.RootElement.GetProperty("slug").GetString();

        Assert.NotNull(slug);
        Assert.StartsWith("kuzey-yazilim-", slug, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_taken_address_is_reported_as_a_conflict()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        var slug = WorkspaceTestClient.UniqueSlug();

        using var first = await WorkspaceTestClient.CreateAsync(
            owner.Client,
            "Acme Teknoloji",
            Cancellation,
            slug);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await WorkspaceTestClient.CreateAsync(
            owner.Client,
            "Başka Bir Alan",
            Cancellation,
            slug);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(
            "workspace.slug_taken",
            await AuthTestClient.ReadProblemCodeAsync(second, Cancellation));
    }

    [Fact]
    public async Task The_creator_of_a_workspace_becomes_its_owner()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var creator = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        var workspace = await WorkspaceTestClient.CreateOwnedAsync(creator.Client, Cancellation);

        Assert.Equal(Domain.Tenancy.MembershipRole.Owner, workspace.Role);
    }

    [Theory]
    [InlineData("AA")]
    [InlineData("Büyük Harfli")]
    [InlineData("bosluk lu")]
    [InlineData("-bastatire")]
    public async Task A_malformed_address_is_rejected(string slug)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var response = await WorkspaceTestClient.CreateAsync(
            owner.Client,
            "Acme Teknoloji",
            Cancellation,
            slug);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
