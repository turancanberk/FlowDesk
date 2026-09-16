using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers.ListCustomers;

/// <param name="Search">Matches name, company or e-mail. Case-insensitive.</param>
/// <param name="Status">Filters by relationship status; null means any.</param>
/// <param name="IncludeArchived">
/// Archived customers are hidden unless asked for. Seeing them is a deliberate
/// act, not the default view.
/// </param>
public sealed record ListCustomersQuery(
    string? Search,
    CustomerStatus? Status,
    bool IncludeArchived,
    CustomerSort Sort,
    int? Page,
    int? PageSize);
