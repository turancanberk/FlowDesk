using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers;

/// <summary>
/// Turns a <see cref="Customer"/> into the shapes the API returns.
/// </summary>
/// <remarks>
/// Written by hand rather than generated. The mapping is a handful of
/// assignments, and a mapping library would add a dependency, a configuration
/// step and a class of runtime-only failures to replace code that a reader can
/// check at a glance (docs/DECISIONS.md, "Mapping").
/// </remarks>
internal static class CustomerMapper
{
    public static CustomerDetail ToDetail(Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Company,
            customer.Status,
            customer.Notes,
            customer.IsArchived,
            customer.CreatedAt,
            customer.UpdatedAt);
}
