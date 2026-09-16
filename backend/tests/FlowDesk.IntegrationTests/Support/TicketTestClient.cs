using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Tickets.AddTicketComment;
using FlowDesk.Application.Tickets.AssignTicket;
using FlowDesk.Application.Tickets.ChangeTicketStatus;
using FlowDesk.Application.Tickets.CreateTicket;
using FlowDesk.Application.Tickets.UpdateTicket;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.IntegrationTests.Support;

internal static class TicketTestClient
{
    private static string Base(string slug) => $"/api/workspaces/{slug}/tickets";

    public static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken,
        string subject = "Fatura PDF'i indirilemiyor",
        string description = "Müşteri Mart ayı faturasını indiremiyor.",
        TicketPriority priority = TicketPriority.Medium,
        Guid? assignedUserId = null) =>
        client.PostAsJsonAsync(
            new Uri(Base(slug), UriKind.Relative),
            new CreateTicketCommand(customerId, subject, description, priority, assignedUserId),
            FlowDeskJson.Options,
            cancellationToken);

    public static async Task<TicketDetailResponse> CreateAndReadAsync(
        HttpClient client,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken,
        string subject = "Fatura PDF'i indirilemiyor",
        TicketPriority priority = TicketPriority.Medium,
        Guid? assignedUserId = null)
    {
        using var response = await CreateAsync(
            client, slug, customerId, cancellationToken, subject, priority: priority,
            assignedUserId: assignedUserId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadDetailAsync(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri($"{Base(slug)}/{ticketId}", UriKind.Relative), cancellationToken);

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
        Guid ticketId,
        Guid customerId,
        uint version,
        CancellationToken cancellationToken,
        string subject = "Güncellenmiş konu",
        string description = "Güncellenmiş açıklama.",
        TicketPriority priority = TicketPriority.Medium) =>
        client.PatchAsJsonAsync(
            new Uri($"{Base(slug)}/{ticketId}", UriKind.Relative),
            new UpdateTicketCommand(subject, description, priority, customerId, version),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        TicketStatus status,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri($"{Base(slug)}/{ticketId}/status", UriKind.Relative),
            new ChangeTicketStatusCommand(status),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> AssignAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        Guid? assignedUserId,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri($"{Base(slug)}/{ticketId}/assignment", UriKind.Relative),
            new AssignTicketCommand(assignedUserId),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(new Uri($"{Base(slug)}/{ticketId}", UriKind.Relative), cancellationToken);

    public static Task<HttpResponseMessage> AddCommentAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        string body,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri($"{Base(slug)}/{ticketId}/comments", UriKind.Relative),
            new AddTicketCommentCommand(body),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> ListCommentsAsync(
        HttpClient client,
        string slug,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        client.GetAsync(
            new Uri($"{Base(slug)}/{ticketId}/comments", UriKind.Relative),
            cancellationToken);

    public static async Task<TicketDetailResponse> ReadDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var ticket = await response.Content.ReadFromJsonAsync<TicketDetailResponse>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(ticket);

        return ticket;
    }

    public static async Task<PagedResponse<TicketListItemResponse>> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<TicketListItemResponse>>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(page);

        return page;
    }

    public static async Task<IReadOnlyList<TicketCommentResponse>> ReadCommentsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var comments = await response.Content.ReadFromJsonAsync<List<TicketCommentResponse>>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(comments);

        return comments;
    }
}
