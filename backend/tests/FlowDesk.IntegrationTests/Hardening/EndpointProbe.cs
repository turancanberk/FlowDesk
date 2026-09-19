using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// Sends catalog requests and checks whether a refused one left anything behind.
/// </summary>
internal static class EndpointProbe
{
    public static async Task<HttpStatusCode> SendAsync(
        HttpClient client,
        string slug,
        ScopedRequest request,
        CancellationToken cancellationToken)
    {
        using var message = request.ToMessage(slug);
        using var response = await client.SendAsync(message, cancellationToken);

        return response.StatusCode;
    }

    /// <summary>
    /// How many entries the workspace's history holds, read as the owner.
    /// </summary>
    /// <remarks>
    /// Every change a use case makes is recorded in the same transaction as the
    /// change itself (ADR-0036). An unchanged count after a refused request is
    /// therefore evidence that nothing was written — which a status code alone
    /// is not: a handler could write first and refuse afterwards.
    /// </remarks>
    public static async Task<int> HistoryLengthAsync(
        HttpClient ownerClient,
        string slug,
        CancellationToken cancellationToken)
    {
        var page = await ownerClient.GetFromJsonAsync<PagedResponse<ActivityResponse>>(
            new Uri($"/api/workspaces/{slug}/activity?pageSize=1", UriKind.Relative),
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(page);

        return page.TotalCount;
    }
}
