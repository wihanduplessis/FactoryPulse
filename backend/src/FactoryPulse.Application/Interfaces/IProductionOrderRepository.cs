using FactoryPulse.Application.Common;
using FactoryPulse.Domain.Entities;

namespace FactoryPulse.Application.Interfaces;

public interface IProductionOrderRepository
{
    Task<ProductionOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProductionOrder> Items, int TotalCount)> GetPagedAsync(ProductionOrderQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task AddAsync(ProductionOrder order, CancellationToken cancellationToken = default);
    void Update(ProductionOrder order);
    void Remove(ProductionOrder order);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
