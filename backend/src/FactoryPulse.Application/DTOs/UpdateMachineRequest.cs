namespace FactoryPulse.Application.DTOs;

public class UpdateMachineRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
}
