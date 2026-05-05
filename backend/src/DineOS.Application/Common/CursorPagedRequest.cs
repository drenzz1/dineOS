namespace DineOS.Application.Common;

/// <summary>Cursor-based pagination request — use for high-frequency feeds (orders, activity).</summary>
public class CursorPagedRequest
{
    private int _pageSize = 20;

    /// <summary>Opaque cursor returned by the previous page. Null fetches the first page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Number of items per page (1–100).</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : value > 100 ? 100 : value;
    }

    /// <summary>Scroll direction relative to the cursor.</summary>
    public CursorDirection Direction { get; set; } = CursorDirection.Forward;
}

public enum CursorDirection { Forward, Backward }
