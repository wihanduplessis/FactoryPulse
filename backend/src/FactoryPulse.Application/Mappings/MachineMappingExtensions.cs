using FactoryPulse.Application.DTOs;
using FactoryPulse.Domain.Entities;
using FactoryPulse.Domain.Enums;

namespace FactoryPulse.Application.Mappings;

public static class MachineMappingExtensions
{
    public static MachineDto ToDto(this Machine machine)
    {
        return new MachineDto
        {
            Id = machine.Id,
            Name = machine.Name,
            Description = machine.Description,
            Status = machine.Status.ToString(),
            CreatedAt = machine.CreatedAt,
            UpdatedAt = machine.UpdatedAt
        };
    }

    public static Machine ToEntity(this CreateMachineRequest request)
    {
        return new Machine
        {
            Name = request.Name,
            Description = request.Description,
            Status = MachineStatus.Idle
        };
    }
}
