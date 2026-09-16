using FlowDesk.Api.Common;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Api.Tenancy;

/// <summary>
/// Turns the <c>workspaceSlug</c> in the route into a verified workspace
/// context.
/// </summary>
/// <remarks>
/// This is the primary tenant isolation boundary (docs/SECURITY.md). The slug
/// comes from the URL, which the caller controls, so it is never trusted: the
/// filter looks up the workspace and the caller's membership of it, and the
/// request only proceeds when both exist.
///
/// A missing workspace and a workspace the caller does not belong to produce
/// the same 404. Distinguishing them would confirm that an organisation exists,
/// letting anyone enumerate workspaces by guessing slugs (ADR-0007).
/// </remarks>
public sealed class WorkspaceResolutionFilter : IEndpointFilter
{
    public const string RouteParameterName = "workspaceSlug";

    private readonly IFlowDeskDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TenantContext _tenantContext;

    public WorkspaceResolutionFilter(
        IFlowDeskDbContext dbContext,
        ICurrentUser currentUser,
        TenantContext tenantContext)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;

        if (!_currentUser.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var slug = httpContext.Request.RouteValues[RouteParameterName]?.ToString();

        if (string.IsNullOrWhiteSpace(slug))
        {
            return TenancyErrors.WorkspaceNotFound.ToProblem(httpContext);
        }

        var userId = _currentUser.Id;

        // One query answers both questions: does the workspace exist, and does
        // this caller belong to it? Asking separately would mean an extra round
        // trip and two chances to use one answer without the other.
        var resolved = await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Slug == slug)
            .Join(
                _dbContext.Memberships.Where(membership => membership.UserId == userId),
                tenant => tenant.Id,
                membership => membership.TenantId,
                (tenant, membership) => new
                {
                    tenant.Id,
                    tenant.Slug,
                    membership.Role,
                })
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (resolved is null)
        {
            return TenancyErrors.WorkspaceNotFound.ToProblem(httpContext);
        }

        _tenantContext.Resolve(resolved.Id, resolved.Slug, userId, resolved.Role);

        return await next(context);
    }
}
