using FactoryPulse.Domain.Entities;
using FactoryPulse.Domain.Enums;
using FactoryPulse.Domain.Exceptions;

namespace FactoryPulse.Tests.Domain;

public class ProductionOrderTests
{
    private static ProductionOrder CreateOrder()
    {
        return ProductionOrder.Create(
            orderNumber: "ORD-001",
            productName: "Widget",
            quantity: 10,
            startDate: DateTime.UtcNow,
            machineId: Guid.NewGuid());
    }

    [Fact]
    public void Create_ShouldStartInPlannedStatus()
    {
        var order = CreateOrder();

        order.Status.ShouldBe(ProductionOrderStatus.Planned);
    }

    [Fact]
    public void Create_ShouldSetProvidedValues()
    {
        var machineId = Guid.NewGuid();

        var order = ProductionOrder.Create("ORD-123", "Bolt", 500, new DateTime(2026, 1, 1), machineId);

        order.OrderNumber.ShouldBe("ORD-123");
        order.ProductName.ShouldBe("Bolt");
        order.Quantity.ShouldBe(500);
        order.MachineId.ShouldBe(machineId);
    }

    [Fact]
    public void Start_WhenPlanned_ShouldTransitionToRunning()
    {
        var order = CreateOrder();

        order.Start();

        order.Status.ShouldBe(ProductionOrderStatus.Running);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ShouldThrow()
    {
        var order = CreateOrder();
        order.Start();

        Should.Throw<InvalidProductionOrderTransitionException>(() => order.Start());
    }

    [Fact]
    public void Complete_WhenRunning_ShouldTransitionToCompletedAndSetEndDate()
    {
        var order = CreateOrder();
        order.Start();
        var endDate = new DateTime(2026, 2, 1);

        order.Complete(endDate);

        order.Status.ShouldBe(ProductionOrderStatus.Completed);
        order.EndDate.ShouldBe(endDate);
    }

    [Fact]
    public void Complete_WhenNotRunning_ShouldThrow()
    {
        var order = CreateOrder();

        Should.Throw<InvalidProductionOrderTransitionException>(() => order.Complete(DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_WhenPlanned_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();

        order.Cancel();

        order.Status.ShouldBe(ProductionOrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenRunning_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();
        order.Start();

        order.Cancel();

        order.Status.ShouldBe(ProductionOrderStatus.Cancelled);
    }

    [Fact]
    public void Start_AfterCancel_ShouldThrow()
    {
        var order = CreateOrder();
        order.Cancel();

        Should.Throw<InvalidProductionOrderTransitionException>(() => order.Start());
    }

    [Fact]
    public void UpdateDetails_WhenPlanned_ShouldChangeValues()
    {
        var order = CreateOrder();

        order.UpdateDetails("New Product", 42);

        order.ProductName.ShouldBe("New Product");
        order.Quantity.ShouldBe(42);
    }

    [Fact]
    public void UpdateDetails_WhenCompleted_ShouldThrow()
    {
        var order = CreateOrder();
        order.Start();
        order.Complete(DateTime.UtcNow);

        Should.Throw<InvalidProductionOrderTransitionException>(() => order.UpdateDetails("x", 1));
    }
}
