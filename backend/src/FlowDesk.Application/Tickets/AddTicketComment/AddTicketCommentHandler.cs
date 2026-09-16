using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.AddTicketComment;

public sealed class AddTicketCommentHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public AddTicketCommentHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<TicketCommentItem>> HandleAsync(
        Guid ticketId,
        AddTicketCommentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.CommentOnTickets))
        {
            return Result.Failure<TicketCommentItem>(TicketErrors.CommentNotAllowed);
        }

        // Confirms the ticket exists in this workspace before writing a comment
        // against its id. Skipping it would let a caller attach a comment to a
        // ticket id from another organisation.
        var ticketExists = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (!ticketExists)
        {
            return Result.Failure<TicketCommentItem>(TicketErrors.NotFound);
        }

        var authorId = _tenantContext.UserId;

        var comment = TicketComment.Create(
            _tenantContext.TenantId,
            ticketId,
            authorId,
            command.Body,
            _clock.UtcNow);

        _dbContext.TicketComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var author = await _accountStore.FindByIdAsync(authorId, cancellationToken);

        return Result.Success(new TicketCommentItem(
            comment.Id,
            comment.AuthorUserId,
            author?.DisplayName ?? "Bilinmeyen kullanıcı",
            comment.Body,
            comment.CreatedAt));
    }
}
