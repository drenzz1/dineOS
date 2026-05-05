namespace DineOS.Application.Common;

/// <summary>Cursor-based pagination response envelope.</summary>
public class CursorPagedResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];

    /// <summary>Pass as <c>Cursor</c> on the next forward request to get the following page.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Pass as <c>Cursor</c> on a backward request to get the preceding page.</summary>
    public string? PreviousCursor { get; init; }

    public bool HasNextPage => NextCursor is not null;
    public bool HasPreviousPage => PreviousCursor is not null;
}
