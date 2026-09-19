using FlowDesk.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Composed in one place so the tests can start exactly this worker.
builder.Services.AddFlowDeskWorker(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
