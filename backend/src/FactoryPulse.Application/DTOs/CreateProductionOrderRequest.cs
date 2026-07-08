namespace FactoryPulse.Application.DTOs;

public class CreateProductionOrderRequest
{
    public string OrderNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime StartDate { get; set; }
    public Guid MachineId { get; set; }
}
