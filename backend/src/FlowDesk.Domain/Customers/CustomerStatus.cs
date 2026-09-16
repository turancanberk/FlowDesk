namespace FlowDesk.Domain.Customers;

/// <summary>
/// Whether a customer relationship is currently live.
/// </summary>
/// <remarks>
/// Distinct from archiving. Status describes the business relationship and is
/// something a team sets deliberately; archiving removes a record from everyday
/// lists without destroying the context its tickets and tasks depend on
/// (ADR-0012).
/// </remarks>
public enum CustomerStatus
{
    Active = 0,
    Inactive = 1,
}
