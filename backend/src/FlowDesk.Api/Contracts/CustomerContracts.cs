using FlowDesk.Application.Common;
using FlowDesk.Domain.Customers;

namespace FlowDesk.Api.Contracts;

public sealed record CustomerListItemResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerDetailResponse(
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
/// A page of results with the counts a client needs to render pagination.
/// </summary>
public sealed record PagedResponse<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Builds a <see cref="PagedResponse{TItem}"/> from an application result.
/// </summary>
/// <remarks>
/// A separate non-generic class so that call sites read as
/// <c>PagedResponse.From(result, ToResponse)</c> and infer both type arguments,
/// rather than naming the item type twice (CA1000).
/// </remarks>
public static class PagedResponse
{
    public static PagedResponse<TResponse> From<TSource, TResponse>(
        PagedResult<TSource> result,
        Func<TSource, TResponse> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return new PagedResponse<TResponse>(
            result.Items.Select(map).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);
    }
}
