using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Customers;
using FlowDesk.Application.Customers.ArchiveCustomer;
using FlowDesk.Application.Customers.CreateCustomer;
using FlowDesk.Application.Customers.GetCustomer;
using FlowDesk.Application.Customers.ListCustomers;
using FlowDesk.Application.Customers.RestoreCustomer;
using FlowDesk.Application.Customers.UpdateCustomer;
using FlowDesk.Domain.Customers;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var customers = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/customers")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Customers");

        customers.MapGet("/", ListAsync).WithName("ListCustomers");
        customers.MapGet("/{customerId:guid}", GetAsync).WithName("GetCustomer");

        customers.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateCustomerCommand>>()
            .WithName("CreateCustomer");

        customers.MapPatch("/{customerId:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateCustomerCommand>>()
            .WithName("UpdateCustomer");

        // DELETE archives rather than destroys (ADR-0012). The verb is kept
        // because from the client's point of view the meaning is "take this out
        // of my list" (docs/API_CONVENTIONS.md).
        customers.MapDelete("/{customerId:guid}", ArchiveAsync).WithName("ArchiveCustomer");

        customers.MapPost("/{customerId:guid}/restore", RestoreAsync).WithName("RestoreCustomer");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListCustomersHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] CustomerStatus? status = null,
        [FromQuery] bool includeArchived = false,
        [FromQuery] CustomerSort sort = CustomerSort.RecentlyUpdated,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var result = await handler.HandleAsync(
            new ListCustomersQuery(search, status, includeArchived, sort, page, pageSize),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(PagedResponse.From(result.Value, ToResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid customerId,
        GetCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(customerId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateCustomerCommand command,
        CreateCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        var slug = httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName];

        return Results.Created(
            $"/api/workspaces/{slug}/customers/{result.Value.Id}",
            ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid customerId,
        [FromBody] UpdateCustomerCommand command,
        UpdateCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(customerId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ArchiveAsync(
        Guid customerId,
        ArchiveCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(customerId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(
        Guid customerId,
        RestoreCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(customerId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static CustomerListItemResponse ToResponse(CustomerListItem customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Company,
            customer.Status,
            customer.IsArchived,
            customer.CreatedAt,
            customer.UpdatedAt);

    private static CustomerDetailResponse ToResponse(CustomerDetail customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Company,
            customer.Status,
            customer.Notes,
            customer.IsArchived,
            customer.CreatedAt,
            customer.UpdatedAt);
}
