using FactoryPulse.Domain.Enums;

namespace FactoryPulse.Application.Common;

public class ProductionOrderQueryParameters
{
    private const int _maxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get { return _page; }
        set
        {
            if (value < 1)
            {
                _page = 1;
            }
            else
            {
                _page = value;
            }
        }
    }

    public int PageSize
    {
        get { return _pageSize; }
        set
        {
            if (value < 1)
            {
                _pageSize = 1;
            }
            else if (value > _maxPageSize)
            {
                _pageSize = _maxPageSize;
            }
            else
            {
                _pageSize = value;
            }
        }
    }

    public ProductionOrderStatus? Status { get; set; }
    public Guid? MachineId { get; set; }
    public string? Product { get; set; }
}
