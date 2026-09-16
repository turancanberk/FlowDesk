using FlowDesk.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFlowDeskInfrastructure(builder.Configuration);

// Background consumers and the outbox publisher are registered here once the
// messaging infrastructure exists (Faz 10 and Faz 11). Until then this host
// deliberately runs no work rather than a placeholder loop: a background
// service that only sleeps would look like progress without being any.

var host = builder.Build();
await host.RunAsync();
