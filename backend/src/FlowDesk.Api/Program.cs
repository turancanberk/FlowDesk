using FlowDesk.Api.Common;
using FlowDesk.Api.Endpoints;
using FlowDesk.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowDeskInfrastructure(builder.Configuration);
builder.Services.AddFlowDeskApplication();
builder.Services.AddFlowDeskApiServices(builder.Configuration);

builder.Services.AddRateLimiter(RateLimitingPolicies.Configure);
builder.Services.AddOpenApi();

// ProblemDetails is registered from the start so that every error leaving the
// API already has one shape; individual endpoints never invent their own
// (docs/API_CONVENTIONS.md).
builder.Services.AddProblemDetails();

var app = builder.Build();

app.EnsureSecureCookiePolicyInProduction();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

await app.RunAsync();

/// <summary>
/// Exposed so that <c>WebApplicationFactory</c> can boot the real application in
/// integration tests rather than a stand-in.
/// </summary>
public partial class Program;
