using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tenancy.CreateWorkspace;

/// <summary>
/// Creates a workspace and makes the caller its owner.
/// </summary>
/// <remarks>
/// The workspace and the owner membership are written in one save. A workspace
/// with no owner would be unmanageable — nobody could invite, change settings
/// or delete it — so the two must land together or not at all.
/// </remarks>
public sealed class CreateWorkspaceHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public CreateWorkspaceHandler(IFlowDeskDbContext dbContext, ICurrentUser currentUser, IClock clock)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<WorkspaceSummary>> HandleAsync(
        CreateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestedSlug = string.IsNullOrWhiteSpace(command.Slug)
            ? WorkspaceSlug.SuggestFrom(command.Name)
            : command.Slug.Trim();

        if (!WorkspaceSlug.TryCreate(requestedSlug, out var slug) || slug is null)
        {
            return Result.Failure<WorkspaceSummary>(ApplicationError.Validation(
                "workspace.invalid_slug",
                "Çalışma alanı adresi oluşturulamadı. Lütfen bir adres girin."));
        }

        var slugValue = slug.Value;

        var slugTaken = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(tenant => tenant.Slug == slugValue, cancellationToken);

        if (slugTaken)
        {
            // Checked up front for a clear message. The unique index is still
            // the authority: two simultaneous creates would both pass this
            // check and the second save is the one that fails.
            return Result.Failure<WorkspaceSummary>(TenancyErrors.SlugAlreadyTaken);
        }

        var now = _clock.UtcNow;
        var tenant = Tenant.Create(command.Name, slug, now);
        var membership = Membership.CreateOwner(_currentUser.Id, tenant.Id, now);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Memberships.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSlugConflict(exception))
        {
            return Result.Failure<WorkspaceSummary>(TenancyErrors.SlugAlreadyTaken);
        }

        return Result.Success(new WorkspaceSummary(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            membership.Role,
            tenant.CreatedAt));
    }

    /// <summary>
    /// Recognises the unique-slug violation without depending on a specific
    /// database provider's error codes, which would put PostgreSQL knowledge in
    /// the application layer (ADR-0021).
    /// </summary>
    private static bool IsSlugConflict(DbUpdateException exception) =>
        exception.Entries.Any(entry => entry.Entity is Tenant);
}
