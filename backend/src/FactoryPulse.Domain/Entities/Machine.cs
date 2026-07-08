using FactoryPulse.Domain.Common;
using FactoryPulse.Domain.Enums;

namespace FactoryPulse.Domain.Entities;

public class Machine : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MachineStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

}
