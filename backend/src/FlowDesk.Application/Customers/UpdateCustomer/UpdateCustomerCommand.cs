using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers.UpdateCustomer;

/// <remarks>
/// Carries the full editable set rather than only the changed fields. A partial
/// update would need a way to distinguish "leave this alone" from "clear this",
/// and every optional field here can legitimately be cleared.
/// </remarks>
public sealed record UpdateCustomerCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    string? Notes);
