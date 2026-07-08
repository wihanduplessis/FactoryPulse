namespace FactoryPulse.Application.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public int TotalPages
    {
        get
        {
            if (PageSize <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }
}
