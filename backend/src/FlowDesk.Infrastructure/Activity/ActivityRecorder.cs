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
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ActivityRecorder(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
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

        _dbContext.ActivityEvents.Add(ActivityEvent.Record(
            _tenantContext.TenantId,
            _tenantContext.UserId,
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
