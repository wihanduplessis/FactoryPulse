namespace FactoryPulse.Application.DTOs;

public class UpdateProductionOrderRequest
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
