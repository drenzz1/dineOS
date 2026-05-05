namespace DineOS.Application.Common;

/// <summary>Standard offset-based pagination request parameters.</summary>
public class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>1-based page number.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Number of items per page (1–100).</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : value > 100 ? 100 : value;
    }

    public int Skip => (Page - 1) * PageSize;
}
