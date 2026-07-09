using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FactoryPulse.API.Controllers;

[Route("api/orders")]
public class ProductionOrdersController : ApiController
{
    private readonly IProductionOrderService _service;

    public ProductionOrdersController(IProductionOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ProductionOrderQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(query, cancellationToken);
        return result.Match(paged => Ok(paged), HandleFailure);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result.Match(order => Ok(order), HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return result.Match(
            order => CreatedAtAction(nameof(GetById), new { id = order.Id }, order),
            HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return result.Match(order => Ok(order), HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.StartAsync(id, cancellationToken);
        return result.Match(order => Ok(order), HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CompleteAsync(id, request, cancellationToken);
        return result.Match(order => Ok(order), HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.CancelAsync(id, cancellationToken);
        return result.Match(order => Ok(order), HandleFailure);
    }

    [Authorize(Policy = "CanWrite")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, cancellationToken);
        return result.Match(() => NoContent(), HandleFailure);
    }
}
