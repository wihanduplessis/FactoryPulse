using FactoryPulse.Domain.Enums;
using FactoryPulse.Domain.Exceptions;

namespace FactoryPulse.Domain.Entities;

public class ProductionOrder
{
    private ProductionOrder()
    {
    }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public ProductionOrderStatus Status { get; private set; }
    public Guid MachineId { get; private set; }
    public Machine? Machine { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool CanStart
    {
        get { return Status == ProductionOrderStatus.Planned; }
    }

    public bool CanComplete
    {
        get { return Status == ProductionOrderStatus.Running; }
    }

    public bool CanCancel
    {
        get { return Status == ProductionOrderStatus.Planned || Status == ProductionOrderStatus.Running; }
    }

    public static ProductionOrder Create(string orderNumber, string productName, int quantity, DateTime startDate, Guid machineId)
    {
        var now = DateTime.UtcNow;

        return new ProductionOrder
        {
            OrderNumber = orderNumber,
            ProductName = productName,
            Quantity = quantity,
            StartDate = startDate,
            Status = ProductionOrderStatus.Planned,
            MachineId = machineId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(string productName, int quantity)
    {
        if (Status == ProductionOrderStatus.Completed || Status == ProductionOrderStatus.Cancelled)
        {
            throw new InvalidProductionOrderTransitionException($"Cannot edit an order in status {Status}.");
        }

        ProductName = productName;
        Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        if (!CanStart)
        {
            throw new InvalidProductionOrderTransitionException($"Cannot start an order in status {Status}.");
        }

        Status = ProductionOrderStatus.Running;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(DateTime endDate)
    {
        if (!CanComplete)
        {
            throw new InvalidProductionOrderTransitionException($"Cannot complete an order in status {Status}.");
        }

        Status = ProductionOrderStatus.Completed;
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (!CanCancel)
        {
            throw new InvalidProductionOrderTransitionException($"Cannot cancel an order in status {Status}.");
        }

        Status = ProductionOrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}
