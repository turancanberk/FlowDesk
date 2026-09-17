using FlowDesk.Api.Authentication;
using FlowDesk.Application.Authentication.GetCurrentUser;
using FlowDesk.Application.Authentication.LoginUser;
using FlowDesk.Application.Authentication.LogoutSession;
using FlowDesk.Application.Authentication.RefreshSession;
using FlowDesk.Application.Authentication.RegisterUser;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Tenancy.CreateWorkspace;
using FlowDesk.Application.Tenancy.DeleteWorkspace;
using FlowDesk.Application.Tenancy.GetWorkspace;
using FlowDesk.Application.Tenancy.ListWorkspaces;
using FlowDesk.Application.Tenancy.UpdateWorkspace;
using FlowDesk.Application.Team.AcceptInvitation;
using FlowDesk.Application.Team.ChangeMemberRole;
using FlowDesk.Application.Team.InviteMember;
using FlowDesk.Application.Team.ListInvitations;
using FlowDesk.Application.Team.ListMembers;
using FlowDesk.Application.Team.RemoveMember;
using FlowDesk.Application.Team.RevokeInvitation;
using FlowDesk.Application.Customers.ArchiveCustomer;
using FlowDesk.Application.Customers.CreateCustomer;
using FlowDesk.Application.Customers.GetCustomer;
using FlowDesk.Application.Customers.ListCustomers;
using FlowDesk.Application.Customers.RestoreCustomer;
using FlowDesk.Application.Customers.UpdateCustomer;
using FlowDesk.Application.Tickets.AddTicketComment;
using FlowDesk.Application.Tickets.AssignTicket;
using FlowDesk.Application.Tickets.ChangeTicketStatus;
using FlowDesk.Application.Tickets.CreateTicket;
using FlowDesk.Application.Tickets.DeleteTicket;
using FlowDesk.Application.Tickets.GetTicket;
using FlowDesk.Application.Tickets.ListTicketComments;
using FlowDesk.Application.Tickets.ListTickets;
using FlowDesk.Application.Tickets.UpdateTicket;
using FlowDesk.Application.Tasks.ChangeTaskStatus;
using FlowDesk.Application.Tasks.CreateTask;
using FlowDesk.Application.Tasks.DeleteTask;
using FlowDesk.Application.Tasks.GetTask;
using FlowDesk.Application.Tasks.ListTasks;
using FlowDesk.Application.Tasks.UpdateTask;
using FlowDesk.Application.Dashboard.GetDashboard;
using FlowDesk.Application.Notifications.ListNotifications;
using FlowDesk.Application.Notifications.MarkNotificationsRead;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FlowDesk.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Common;

/// <summary>Wires the application layer and HTTP-facing services.</summary>
public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "FlowDeskWeb";

    public static IServiceCollection AddFlowDeskApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use cases are injected directly. No mediator sits in between: it
        // would hide the call chain without removing any coupling (ADR-0004).
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutSessionHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddScoped<CreateWorkspaceHandler>();
        services.AddScoped<ListWorkspacesHandler>();
        services.AddScoped<GetWorkspaceHandler>();
        services.AddScoped<UpdateWorkspaceHandler>();
        services.AddScoped<DeleteWorkspaceHandler>();

        services.AddScoped<ListMembersHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();

        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<ArchiveCustomerHandler>();
        services.AddScoped<RestoreCustomerHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<ListCustomersHandler>();

        services.AddScoped<CreateTicketHandler>();
        services.AddScoped<UpdateTicketHandler>();
        services.AddScoped<ChangeTicketStatusHandler>();
        services.AddScoped<AssignTicketHandler>();
        services.AddScoped<DeleteTicketHandler>();
        services.AddScoped<GetTicketHandler>();
        services.AddScoped<ListTicketsHandler>();
        services.AddScoped<AddTicketCommentHandler>();
        services.AddScoped<ListTicketCommentsHandler>();

        services.AddScoped<CreateTaskHandler>();
        services.AddScoped<UpdateTaskHandler>();
        services.AddScoped<ChangeTaskStatusHandler>();
        services.AddScoped<DeleteTaskHandler>();
        services.AddScoped<GetTaskHandler>();
        services.AddScoped<ListTasksHandler>();

        services.AddScoped<GetDashboardHandler>();

        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<MarkNotificationsReadHandler>();

        services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>(ServiceLifetime.Singleton);

        return services;
    }

    public static IServiceCollection AddFlowDeskApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        /*
          Enums travel as their names, not their numeric values.

          The default is the number, which makes a contract that depends on
          declaration order: inserting a role or a ticket status in the middle
          of an enum would silently change what every stored and in-flight value
          means. Names also keep the wire format readable and match what
          docs/API_CONVENTIONS.md promises clients.
        */
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        /*
          One TenantContext instance per request, exposed under two service
          types. The resolution filter needs the concrete type to write to it;
          everything downstream sees the read-only interface, so no use case can
          change which workspace it is operating in halfway through a request.
        */
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());

        services.Configure<CookieOptionsSettings>(configuration.GetSection(CookieOptionsSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        services.AddCors(options =>
        {
            var allowedOrigins = configuration
                .GetSection($"{CorsSettings.SectionName}:AllowedOrigins")
                .Get<string[]>() ?? [];

            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    // Production serves the frontend and the API from one
                    // origin behind Caddy, so there is nothing to allow.
                    return;
                }

                policy
                    .WithOrigins(allowedOrigins)
                    // Never AllowAnyOrigin: it is invalid together with
                    // credentials and gives a false sense of configuration.
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    // The refresh cookie has to ride along on /api/auth calls.
                    .AllowCredentials();
            });
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        /*
          Validation parameters are configured through the options pipeline
          rather than inside AddJwtBearer, because reading AuthenticationOptions
          there would require building a second service provider — which creates
          a parallel container and a second copy of every singleton.

          Deriving the parameters from the same options the issuer uses means
          the signing and validating sides cannot drift apart.
        */
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FlowDesk.Infrastructure.Authentication.AuthenticationOptions>>(
                (jwtOptions, authOptions) =>
                {
                    jwtOptions.TokenValidationParameters =
                        JwtAccessTokenIssuer.BuildValidationParameters(authOptions.Value);

                    // Tokens arrive in the Authorization header only. Reading
                    // them from a cookie would reintroduce the CSRF surface that
                    // keeping the access token in memory removes (ADR-0006).
                    jwtOptions.MapInboundClaims = false;
                });

        services.AddAuthorization();

        return services;
    }
}
