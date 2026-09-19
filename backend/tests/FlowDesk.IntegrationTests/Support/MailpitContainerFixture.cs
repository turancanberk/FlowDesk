using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// A real SMTP server whose inbox the test can read over HTTP.
/// </summary>
/// <remarks>
/// The same image the development environment runs. The notification tests
/// record mail through a stand-in sender, which checks what the consumers
/// decide to send; this checks that it is actually sent — that the SMTP
/// conversation, the addressing and the encoding survive a real server.
///
/// A class fixture rather than one shared by the whole suite: only the
/// end-to-end test sends mail, and every other test should not wait for a
/// server it never uses.
/// </remarks>
public sealed class MailpitContainerFixture : IAsyncLifetime
{
    private const string MailpitImage = "axllent/mailpit:latest";
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder(MailpitImage)
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        // The HTTP API answering means the process is up; SMTP starts first.
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/api/v1/info")))
        .WithCleanUp(true)
        .Build();

    public string SmtpHost => _container.Hostname;

    public int SmtpPortNumber => _container.GetMappedPublicPort(SmtpPort);

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Every message addressed to <paramref name="address"/>, newest first.</summary>
    internal async Task<IReadOnlyList<MailpitMessage>> MessagesToAsync(
        string address,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient();

        var inbox = await client.GetFromJsonAsync<MailpitInbox>(
            new Uri("/api/v1/messages?limit=500", UriKind.Relative), cancellationToken);

        return inbox?.Messages
            .Where(message => message.To.Any(recipient =>
                string.Equals(recipient.Address, address, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            ?? [];
    }

    /// <summary>The HTML body of one message.</summary>
    internal async Task<string> HtmlBodyAsync(string messageId, CancellationToken cancellationToken)
    {
        using var client = CreateClient();

        var message = await client.GetFromJsonAsync<MailpitMessageDetail>(
            new Uri($"/api/v1/message/{messageId}", UriKind.Relative), cancellationToken);

        return message?.Html ?? string.Empty;
    }

    private HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}"),
    };

    private sealed record MailpitInbox([property: JsonPropertyName("messages")] List<MailpitMessage> Messages);

    private sealed record MailpitMessageDetail([property: JsonPropertyName("HTML")] string Html);
}

internal sealed record MailpitAddress([property: JsonPropertyName("Address")] string Address);

internal sealed record MailpitMessage(
    [property: JsonPropertyName("ID")] string Id,
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To);
