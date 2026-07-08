using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;

namespace FactoryPulse.Application.Interfaces;

public interface IProductionOrderService
{
    Task<Result<ProductionOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProductionOrderDto>>> GetPagedAsync(ProductionOrderQueryParameters query, CancellationToken cancellationToken = default);
    Task<Result<ProductionOrderDto>> CreateAsync(CreateProductionOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductionOrderDto>> UpdateAsync(Guid id, UpdateProductionOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductionOrderDto>> StartAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ProductionOrderDto>> CompleteAsync(Guid id, CompleteProductionOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductionOrderDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
