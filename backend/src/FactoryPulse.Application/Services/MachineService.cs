using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Mappings;
using FactoryPulse.Domain.Enums;

namespace FactoryPulse.Application.Services;

public class MachineService : IMachineService
{
    private readonly IMachineRepository _repository;

    public MachineService(IMachineRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<MachineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var machine = await _repository.GetByIdAsync(id, cancellationToken);

        if (machine is null)
        {
            return Result.Failure<MachineDto>(Errors.Machine.NotFound);
        }

        return machine.ToDto();
    }

    public async Task<Result<IReadOnlyList<MachineDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var machines = await _repository.GetAllAsync(cancellationToken);
        IReadOnlyList<MachineDto> dtos = machines.Select(machine => machine.ToDto()).ToList();
        return Result.Success(dtos);
    }

    public async Task<Result<MachineDto>> CreateAsync(CreateMachineRequest request, CancellationToken cancellationToken = default)
    {
        var machine = request.ToEntity();
        machine.CreatedAt = DateTime.UtcNow;
        machine.UpdatedAt = DateTime.UtcNow;

        await _repository.AddAsync(machine, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return machine.ToDto();
    }

    public async Task<Result<MachineDto>> UpdateAsync(Guid id, UpdateMachineRequest request, CancellationToken cancellationToken = default)
    {
        var machine = await _repository.GetByIdAsync(id, cancellationToken);

        if (machine is null)
        {
            return Result.Failure<MachineDto>(Errors.Machine.NotFound);
        }

        if (!Enum.TryParse<MachineStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Result.Failure<MachineDto>(Errors.Machine.InvalidStatus);
        }

        machine.Name = request.Name;
        machine.Description = request.Description;
        machine.Status = status;
        machine.UpdatedAt = DateTime.UtcNow;

        _repository.Update(machine);
        await _repository.SaveChangesAsync(cancellationToken);

        return machine.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var machine = await _repository.GetByIdAsync(id, cancellationToken);

        if (machine is null)
        {
            return Result.Failure(Errors.Machine.NotFound);
        }

        _repository.Remove(machine);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
