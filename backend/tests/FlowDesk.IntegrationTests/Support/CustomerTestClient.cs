using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Customers.CreateCustomer;
using FlowDesk.Application.Customers.UpdateCustomer;
using FlowDesk.Domain.Customers;

namespace FlowDesk.IntegrationTests.Support;

internal static class CustomerTestClient
{
    private static string Base(string slug) => $"/api/workspaces/{slug}/customers";

    public static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string slug,
        string name,
        CancellationToken cancellationToken,
        string? email = null,
        string? company = null,
        CustomerStatus status = CustomerStatus.Active,
        string? notes = null,
        string? phone = null) =>
        client.PostAsJsonAsync(
            new Uri(Base(slug), UriKind.Relative),
            new CreateCustomerCommand(name, email, phone, company, status, notes),
            FlowDeskJson.Options,
            cancellationToken);

    public static async Task<CustomerDetailResponse> CreateAndReadAsync(
        HttpClient client,
        string slug,
        string name,
        CancellationToken cancellationToken,
        string? email = null,
        string? company = null,
        CustomerStatus status = CustomerStatus.Active)
    {
        using var response = await CreateAsync(
            client, slug, name, cancellationToken, email, company, status);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<CustomerDetailResponse>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(customer);

        return customer;
    }

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri($"{Base(slug)}/{customerId}", UriKind.Relative), cancellationToken);

    public static Task<HttpResponseMessage> ListAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken,
        string? query = null) =>
        client.GetAsync(
            new Uri($"{Base(slug)}{(query is null ? string.Empty : $"?{query}")}", UriKind.Relative),
            cancellationToken);

    public static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        string name,
        CancellationToken cancellationToken,
        CustomerStatus status = CustomerStatus.Active) =>
        client.PatchAsJsonAsync(
            new Uri($"{Base(slug)}/{customerId}", UriKind.Relative),
            new UpdateCustomerCommand(name, null, null, null, status, null),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> ArchiveAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(
            new Uri($"{Base(slug)}/{customerId}", UriKind.Relative),
            cancellationToken);

    public static Task<HttpResponseMessage> RestoreAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken) =>
        client.PostAsync(
            new Uri($"{Base(slug)}/{customerId}/restore", UriKind.Relative),
            content: null,
            cancellationToken);

    public static async Task<PagedResponse<CustomerListItemResponse>> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<CustomerListItemResponse>>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(page);

        return page;
    }
}
