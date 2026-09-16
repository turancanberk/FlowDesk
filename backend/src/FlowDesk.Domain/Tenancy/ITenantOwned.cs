namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// Marks an entity as belonging to exactly one workspace.
/// </summary>
/// <remarks>
/// Implementing this is what enrols an entity in the global query filter: the
/// persistence layer finds every type carrying this marker and scopes it to the
/// current workspace automatically, so a new entity cannot be added and have
/// the filter forgotten.
///
/// That filter is a safety net, not the security boundary. It can be bypassed
/// with <c>IgnoreQueryFilters</c> and it does not cover raw SQL. The boundary
/// is the membership check performed on every request (docs/SECURITY.md).
/// </remarks>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
