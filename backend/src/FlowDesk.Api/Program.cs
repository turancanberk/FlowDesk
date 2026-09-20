using FlowDesk.Api.Common;
using FlowDesk.Api.Endpoints;
using FlowDesk.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/*
  The server's own name is not news to a client and is a free hint to anyone
  scanning for a version with a known hole (docs/SECURITY.md §14).
*/
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// First, so a failure while wiring anything else is already logged the way
// everything else will be (ADR-0042).
builder.AddFlowDeskApiObservability();

builder.Services.AddFlowDeskInfrastructure(builder.Configuration);
builder.Services.AddFlowDeskApplication();
builder.Services.AddFlowDeskApiServices(builder.Configuration);

builder.Services.AddFlowDeskForwardedHeaders(builder.Configuration);
builder.Services.AddFlowDeskRateLimiting(builder.Configuration);
builder.Services.AddOpenApi();

// ProblemDetails is registered from the start so that every error leaving the
// API already has one shape; individual endpoints never invent their own
// (docs/API_CONVENTIONS.md).
builder.Services.AddProblemDetails();

var app = builder.Build();

app.EnsureSecureCookiePolicyInProduction();

/*
  Before everything else: the rate limiter and the cookie policy both depend on
  who the caller is and whether the connection was secure, and both would read
  the proxy's answers instead.
*/
app.UseFlowDeskForwardedHeaders();

/*
  First in the pipeline, so even a response that never reaches an endpoint —
  a 404, a rate-limited 429, an unhandled failure — carries them.
*/
app.UseFlowDeskSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    /*
      Only outside development. Sending it over plain http would be ignored by
      browsers anyway, and on localhost a stray max-age would pin every other
      project on this machine to https for as long as it lasted.
    */
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Before the handlers, so the one line it writes covers the whole request,
// including a failure that never reaches an endpoint.
app.UseFlowDeskRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapTeamEndpoints();
app.MapCustomerEndpoints();
app.MapTicketEndpoints();
app.MapTaskEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationEndpoints();
app.MapActivityEndpoints();

await app.RunAsync();

/// <summary>
/// Exposed so that <c>WebApplicationFactory</c> can boot the real application in
/// integration tests rather than a stand-in.
/// </summary>
public partial class Program;
