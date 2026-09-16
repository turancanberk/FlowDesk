using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.ListTicketComments;

/// <summary>
/// Reads a ticket's comment thread, oldest first.
/// </summary>
/// <remarks>
/// Not paged. A thread is read as a conversation from the top, and splitting it
/// across pages would break that reading for no gain at the volumes a support
/// ticket reaches. If threads ever grow long enough to matter, paging can be
/// added then, with evidence.
/// </remarks>
public sealed class ListTicketCommentsHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;

    public ListTicketCommentsHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyCollection<TicketCommentItem>>> HandleAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTickets))
        {
            return Result.Failure<IReadOnlyCollection<TicketCommentItem>>(
                TenancyErrors.InsufficientRole("Talep görüntülemek"));
        }

        var ticketExists = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (!ticketExists)
        {
            return Result.Failure<IReadOnlyCollection<TicketCommentItem>>(TicketErrors.NotFound);
        }

        var rows = await _dbContext.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => new
            {
                comment.Id,
                comment.AuthorUserId,
                comment.Body,
                comment.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var authorNames = await TicketQueries.ResolveUserNamesAsync(
            _accountStore,
            rows.Select(row => row.AuthorUserId).Distinct().ToArray(),
            cancellationToken);

        IReadOnlyCollection<TicketCommentItem> comments = rows
            .Select(row => new TicketCommentItem(
                row.Id,
                row.AuthorUserId,
                authorNames.GetValueOrDefault(row.AuthorUserId, "Bilinmeyen kullanıcı"),
                row.Body,
                row.CreatedAt))
            .ToList();

        return Result.Success(comments);
    }
}
