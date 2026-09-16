namespace FlowDesk.Application.Common;

/// <summary>
/// One page of a larger result set.
/// </summary>
/// <remarks>
/// List endpoints never return an unbounded collection: a workspace with ten
/// thousand customers would otherwise turn one request into a slow query, a
/// large payload and a browser that has to render all of it
/// (docs/API_CONVENTIONS.md).
/// </remarks>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Paging inputs, clamped to a usable range.
/// </summary>
/// <remarks>
/// Out-of-range values are clamped rather than rejected. A client asking for
/// page 0 or for ten thousand rows has made a mistake that should not break the
/// user's screen, while the server still has to be protected from the request.
/// </remarks>
public sealed record PageRequest
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public PageRequest(int? page, int? pageSize)
    {
        Page = page is null or < 1 ? 1 : page.Value;

        PageSize = pageSize switch
        {
            null or < 1 => DefaultPageSize,
            > MaximumPageSize => MaximumPageSize,
            _ => pageSize.Value,
        };
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}
