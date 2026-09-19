using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// The worker can build everything it runs.
/// </summary>
/// <remarks>
/// The API is built by every integration test, so a registration it cannot
/// satisfy fails immediately. The worker was built by none, and in Phase 15 it
/// gained a dependency — the workspace context — that only the API registers.
/// The host still started; every outbox pass then failed, and no notice or
/// mail was sent until Phase 16's end-to-end test found it.
///
/// Nothing here connects to anything: connections are opened lazily, so
/// building the services proves the wiring without needing the services
/// themselves.
/// </remarks>
public sealed class WorkerCompositionTests
{
    [Fact]
    public async Task The_worker_builds_its_context_and_every_consumer_it_subscribes()
    {
        // The API's settings, so the worker is validated against the same
        // options a deployment gives both hosts.
        await using var api = new FlowDeskApiFactory("Host=127.0.0.1;Port=1;Database=unused");

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Development turns on scope and build-time validation.
            EnvironmentName = Environments.Development,
        });

        builder.Configuration.AddInMemoryCollection(api.Settings);
        builder.Services.AddFlowDeskWorker(builder.Configuration);

        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        /*
          Resolved rather than only validated. Build-time validation skips
          services registered through a factory, and the context — together with
          the interceptor that broke — is exactly such a registration.
        */
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFlowDeskDbContext>());

        var subscriptions = scope.ServiceProvider.GetServices<IMessageSubscription>().ToList();
        Assert.NotEmpty(subscriptions);

        foreach (var subscription in subscriptions)
        {
            var messageType = subscription.GetType().GetGenericArguments().Single();
            var consumerType = typeof(IMessageConsumer<>).MakeGenericType(messageType);

            Assert.NotNull(scope.ServiceProvider.GetRequiredService(consumerType));
        }

        Assert.NotEmpty(host.Services.GetServices<IHostedService>());
    }
}
