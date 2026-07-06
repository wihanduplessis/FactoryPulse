using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;

namespace FactoryPulse.Application.Interfaces;

public interface IMachineService
{
    Task<Result<MachineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MachineDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<MachineDto>> CreateAsync(CreateMachineRequest request, CancellationToken cancellationToken = default);
    Task<Result<MachineDto>> UpdateAsync(Guid id, UpdateMachineRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
