using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Mappings;
using FactoryPulse.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FactoryPulse.Application.Services;

public class MachineService : IMachineService
{
    private readonly IMachineRepository _repository;
    private readonly IValidator<CreateMachineRequest> _createValidator;
    private readonly IValidator<UpdateMachineRequest> _updateValidator;
    private readonly ILogger<MachineService> _logger;

    public MachineService(IMachineRepository repository, IValidator<CreateMachineRequest> createValidator, IValidator<UpdateMachineRequest> updateValidator, ILogger<MachineService> logger)
    {
        _repository = repository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
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
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<MachineDto>(validationResult.ToErrors());
        }

        var machine = request.ToEntity();

        await _repository.AddAsync(machine, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created machine {MachineId} ({MachineName})", machine.Id, machine.Name);
        return machine.ToDto();
    }

    public async Task<Result<MachineDto>> UpdateAsync(Guid id, UpdateMachineRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<MachineDto>(validationResult.ToErrors());
        }

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

        _repository.Update(machine);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated machine {MachineId}", machine.Id);
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

        _logger.LogInformation("Deleted machine {MachineId}", id);
        return Result.Success();
    }
}
