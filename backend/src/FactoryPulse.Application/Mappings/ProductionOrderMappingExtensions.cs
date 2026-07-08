using FactoryPulse.Application.DTOs;
using FactoryPulse.Domain.Entities;

namespace FactoryPulse.Application.Mappings;

public static class ProductionOrderMappingExtensions
{
    public static ProductionOrderDto ToDto(this ProductionOrder order)
    {
        return new ProductionOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            StartDate = order.StartDate,
            EndDate = order.EndDate,
            Status = order.Status.ToString(),
            MachineId = order.MachineId,
            MachineName = order.Machine?.Name,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    public static ProductionOrder ToEntity(this CreateProductionOrderRequest request)
    {
        return ProductionOrder.Create(request.OrderNumber,request.ProductName,request.Quantity,request.StartDate,request.MachineId);
    }
}
