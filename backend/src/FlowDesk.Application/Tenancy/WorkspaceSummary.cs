using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Tenancy;

/// <summary>
/// A workspace as it appears to one member, including their role in it.
/// </summary>
/// <param name="Role">
/// The caller's role. Bundled with the workspace because the two are always
/// needed together and fetching them separately would mean a second query per
/// row.
/// </param>
public sealed record WorkspaceSummary(
    Guid Id,
    string Name,
    string Slug,
    MembershipRole Role,
    DateTimeOffset CreatedAt);
