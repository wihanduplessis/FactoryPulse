using FactoryPulse.Application.Common;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Domain.Entities;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Repositories;

public class ProductionOrderRepository : IProductionOrderRepository
{
    private readonly FactoryPulseDbContext _context;

    public ProductionOrderRepository(FactoryPulseDbContext context)
    {
        _context = context;
    }

    public async Task<ProductionOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProductionOrders
            .Include(order => order.Machine)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<ProductionOrder> Items, int TotalCount)> GetPagedAsync(
        ProductionOrderQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ProductionOrder> ordersQuery = _context.ProductionOrders
            .Include(order => order.Machine)
            .AsNoTracking();

        if (query.Status.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.Status == query.Status.Value);
        }

        if (query.MachineId.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.MachineId == query.MachineId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Product))
        {
            ordersQuery = ordersQuery.Where(order => order.ProductName.Contains(query.Product));
        }

        int totalCount = await ordersQuery.CountAsync(cancellationToken);

        List<ProductionOrder> items = await ordersQuery
            .OrderByDescending(order => order.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        return await _context.ProductionOrders
            .AnyAsync(order => order.OrderNumber == orderNumber, cancellationToken);
    }

    public async Task AddAsync(ProductionOrder order, CancellationToken cancellationToken = default)
    {
        await _context.ProductionOrders.AddAsync(order, cancellationToken);
    }

    public void Update(ProductionOrder order)
    {
        _context.ProductionOrders.Update(order);
    }

    public void Remove(ProductionOrder order)
    {
        _context.ProductionOrders.Remove(order);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
