using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Tasks.ChangeTaskStatus;
using FlowDesk.Application.Tasks.CreateTask;
using FlowDesk.Application.Tasks.UpdateTask;
using FlowDesk.Domain.Tasks;

namespace FlowDesk.IntegrationTests.Support;

internal static class TaskTestClient
{
    private static string Base(string slug) => $"/api/workspaces/{slug}/tasks";

    public static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken,
        string title = "Sözleşmeyi gözden geçir",
        string? description = "Yenileme öncesi maddeler kontrol edilecek.",
        Guid? customerId = null,
        Guid? assignedUserId = null,
        DateTimeOffset? dueAt = null) =>
        client.PostAsJsonAsync(
            new Uri(Base(slug), UriKind.Relative),
            new CreateTaskCommand(title, description, customerId, assignedUserId, dueAt),
            FlowDeskJson.Options,
            cancellationToken);

    public static async Task<TaskDetailResponse> CreateAndReadAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken,
        string title = "Sözleşmeyi gözden geçir",
        Guid? customerId = null,
        Guid? assignedUserId = null,
        DateTimeOffset? dueAt = null)
    {
        using var response = await CreateAsync(
            client, slug, cancellationToken, title,
            customerId: customerId, assignedUserId: assignedUserId, dueAt: dueAt);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadDetailAsync(response, cancellationToken);
    }

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string slug,
        Guid taskId,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri($"{Base(slug)}/{taskId}", UriKind.Relative), cancellationToken);

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
        Guid taskId,
        CancellationToken cancellationToken,
        string title = "Güncellenmiş başlık",
        string? description = null,
        Guid? customerId = null,
        Guid? assignedUserId = null,
        DateTimeOffset? dueAt = null) =>
        client.PatchAsJsonAsync(
            new Uri($"{Base(slug)}/{taskId}", UriKind.Relative),
            new UpdateTaskCommand(title, description, customerId, assignedUserId, dueAt),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        string slug,
        Guid taskId,
        TaskItemStatus status,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri($"{Base(slug)}/{taskId}/status", UriKind.Relative),
            new ChangeTaskStatusCommand(status),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        string slug,
        Guid taskId,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(new Uri($"{Base(slug)}/{taskId}", UriKind.Relative), cancellationToken);

    public static async Task<TaskDetailResponse> ReadDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var task = await response.Content.ReadFromJsonAsync<TaskDetailResponse>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(task);

        return task;
    }

    public static async Task<PagedResponse<TaskListItemResponse>> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<TaskListItemResponse>>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(page);

        return page;
    }
}
