using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Tenancy.CreateWorkspace;
using FlowDesk.Application.Tenancy.UpdateWorkspace;

namespace FlowDesk.IntegrationTests.Support;

internal static class WorkspaceTestClient
{
    /// <summary>
    /// A fresh slug per call. Tests share one database and the slug is unique,
    /// so colliding would make failures depend on execution order.
    /// </summary>
    public static string UniqueSlug(string prefix = "alan") =>
        $"{prefix}-{Guid.CreateVersion7():N}"[..30];

    public static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string name,
        CancellationToken cancellationToken,
        string? slug = null) =>
        client.PostAsJsonAsync(
            new Uri("/api/workspaces", UriKind.Relative),
            new CreateWorkspaceCommand(name, slug),
            FlowDeskJson.Options,
            cancellationToken);

    /// <summary>Creates a workspace and returns it, failing the test if creation did not work.</summary>
    public static async Task<WorkspaceResponse> CreateOwnedAsync(
        HttpClient client,
        CancellationToken cancellationToken,
        string name = "Acme Teknoloji")
    {
        using var response = await CreateAsync(client, name, cancellationToken, UniqueSlug());

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(FlowDeskJson.Options, cancellationToken);
        Assert.NotNull(workspace);

        return workspace;
    }

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri($"/api/workspaces/{slug}", UriKind.Relative), cancellationToken);

    public static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client,
        string slug,
        string name,
        CancellationToken cancellationToken) =>
        client.PatchAsJsonAsync(
            new Uri($"/api/workspaces/{slug}", UriKind.Relative),
            new UpdateWorkspaceCommand(name),
            cancellationToken);

    public static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(new Uri($"/api/workspaces/{slug}", UriKind.Relative), cancellationToken);

    public static Task<HttpResponseMessage> ListAsync(
        HttpClient client,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri("/api/workspaces", UriKind.Relative), cancellationToken);
}
