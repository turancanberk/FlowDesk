using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Activity;
using FlowDesk.Domain.Activity;
using FlowDesk.Infrastructure.Messaging;

namespace FlowDesk.Infrastructure.Activity;

/// <summary>
/// Adds an activity row to the current unit of work.
/// </summary>
/// <remarks>
/// Scoped, because it writes through the request's own <c>DbContext</c> and
/// reads the workspace and actor from the request's tenant context. That is
/// what makes it impossible to record an event against the wrong workspace: the
/// caller does not get to say which one.
/// </remarks>
public sealed class ActivityRecorder : IActivityRecorder
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext? _tenantContext;
    private readonly IClock _clock;

    /// <param name="tenantContext">
    /// Null in the worker, which registers no workspace context. Required
    /// here, the worker failed its start-up validation from Phase 14 on;
    /// optional, it can still build everything, and only <see cref="Record"/>
    /// — which the worker never calls — needs a workspace.
    /// </param>
    public ActivityRecorder(
        IFlowDeskDbContext dbContext,
        ITenantContext? tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public void Record(
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (_tenantContext is not { IsResolved: true } tenantContext)
        {
            // A wiring mistake, not a user's: say so rather than record an
            // event against an empty workspace id.
            throw new InvalidOperationException(
                "Record needs a workspace in scope; use RecordOutsideWorkspace and name it.");
        }

        _dbContext.ActivityEvents.Add(ActivityEvent.Record(
            tenantContext.TenantId,
            tenantContext.UserId,
            type,
            subjectType,
            subjectId,
            JsonSerializer.Serialize(payload, payload.GetType(), FlowDeskMessageJson.Options),
            _clock.UtcNow));
    }

    public void RecordOutsideWorkspace(
        Guid tenantId,
        Guid? actorUserId,
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        _dbContext.ActivityEvents.Add(ActivityEvent.Record(
            tenantId,
            actorUserId,
            type,
            subjectType,
            subjectId,
            JsonSerializer.Serialize(payload, payload.GetType(), FlowDeskMessageJson.Options),
            _clock.UtcNow));
    }
}
