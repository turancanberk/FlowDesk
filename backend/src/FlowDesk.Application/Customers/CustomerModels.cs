using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers;

/// <summary>A customer as it appears in a list.</summary>
/// <remarks>
/// Deliberately narrower than the detail view: notes can run to thousands of
/// characters and a list of twenty-five would carry them all for nothing.
/// </remarks>
public sealed record CustomerListItem(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerDetail(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    string? Notes,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Sort orders a client may ask for.
/// </summary>
/// <remarks>
/// An enum rather than a free-text column name: accepting a raw field name
/// would let a caller sort by anything the table happens to contain, and would
/// tie the API to column naming.
/// </remarks>
public enum CustomerSort
{
    /// <summary>Most recently updated first. What a team usually wants to see.</summary>
    RecentlyUpdated = 0,
    NameAscending = 1,
    NameDescending = 2,
    RecentlyCreated = 3,
}
