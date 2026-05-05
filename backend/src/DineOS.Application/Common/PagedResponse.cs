namespace DineOS.Application.Common;

/// <summary>Standard offset-based pagination response envelope.</summary>
public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResponse<T> From(IEnumerable<T> items, int totalCount, PagedRequest request) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
}
