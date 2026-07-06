using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.API.Controllers;

[Route("api/[controller]")]
public class MachinesController : ApiController
{
    private readonly IMachineService _machineService;

    public MachinesController(IMachineService machineService)
    {
        _machineService = machineService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result =  await _machineService.GetAllAsync(cancellationToken);
        return result.Match(machines => Ok(machines), HandleFailure);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _machineService.GetByIdAsync(id, cancellationToken);
        return result.Match(machine => Ok(machine), HandleFailure);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMachineRequest request, CancellationToken cancellationToken)
    {
        var result = await _machineService.CreateAsync(request, cancellationToken);
        return result.Match(
            machine => CreatedAtAction(nameof(GetById), new { id = machine.Id }, machine),
            HandleFailure);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMachineRequest request, CancellationToken cancellationToken)
    {
        var result = await _machineService.UpdateAsync(id, request, cancellationToken);
        return result.Match(machine => Ok(machine), HandleFailure);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _machineService.DeleteAsync(id, cancellationToken);
        return result.Match(() => NoContent(), HandleFailure);
    }
}
