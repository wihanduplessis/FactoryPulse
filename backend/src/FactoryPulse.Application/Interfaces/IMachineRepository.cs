using FactoryPulse.Domain.Entities;

namespace FactoryPulse.Application.Interfaces;

public interface IMachineRepository
{
    Task<Machine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Machine machine, CancellationToken cancellationToken = default);
    void Update(Machine machine);
    void Remove(Machine machine);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
