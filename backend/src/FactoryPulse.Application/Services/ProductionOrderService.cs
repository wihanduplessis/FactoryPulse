using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Mappings;
using FactoryPulse.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FactoryPulse.Application.Services;

public class ProductionOrderService : IProductionOrderService
{
    private readonly IProductionOrderRepository _repository;
    private readonly IMachineRepository _machineRepository;
    private readonly IValidator<CreateProductionOrderRequest> _createValidator;
    private readonly IValidator<UpdateProductionOrderRequest> _updateValidator;
    private readonly ILogger<ProductionOrderService> _logger;

    public ProductionOrderService(IProductionOrderRepository repository, IMachineRepository machineRepository,
        IValidator<CreateProductionOrderRequest> createValidator, IValidator<UpdateProductionOrderRequest> updateValidator, ILogger<ProductionOrderService> logger)
    {
        _repository = repository;
        _machineRepository = machineRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    private static IReadOnlyList<Error> ToValidationErrors(FluentValidation.Results.ValidationResult validationResult)
    {
        return validationResult.Errors.Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage)).ToList();
    }

    public async Task<Result<ProductionOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.NotFound);
        }

        return order.ToDto();
    }

    public async Task<Result<PagedResult<ProductionOrderDto>>> GetPagedAsync(ProductionOrderQueryParameters query, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(query, cancellationToken);
        IReadOnlyList<ProductionOrderDto> dtos = items.Select(order => order.ToDto()).ToList();
        var pagedResult = new PagedResult<ProductionOrderDto>(dtos, query.Page, query.PageSize, totalCount);
        return Result.Success(pagedResult);
    }

    public async Task<Result<ProductionOrderDto>> CreateAsync(CreateProductionOrderRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ProductionOrderDto>(ToValidationErrors(validationResult));
        }

        var machine = await _machineRepository.GetByIdAsync(request.MachineId, cancellationToken);
        if (machine is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.MachineNotFound);
        }

        if (machine.Status == MachineStatus.Retired)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.MachineRetired);
        }

        var exists = await _repository.OrderNumberExistsAsync(request.OrderNumber, cancellationToken);
        if (exists)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.DuplicateOrderNumber);
        }

        var order = request.ToEntity();
        await _repository.AddAsync(order, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created production order {OrderId} ({OrderNumber})", order.Id, order.OrderNumber);

        var created = await _repository.GetByIdAsync(order.Id, cancellationToken);
        return created!.ToDto();
    }

    public async Task<Result<ProductionOrderDto>> UpdateAsync(Guid id, UpdateProductionOrderRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ProductionOrderDto>(ToValidationErrors(validationResult));
        }

        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.NotFound);
        }

        if (order.Status == ProductionOrderStatus.Completed || order.Status == ProductionOrderStatus.Cancelled)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.InvalidTransition);
        }

        order.UpdateDetails(request.ProductName, request.Quantity);
        _repository.Update(order);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated production order {OrderId}", order.Id);
        return order.ToDto();
    }

    public async Task<Result<ProductionOrderDto>> StartAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.NotFound);
        }

        if (!order.CanStart)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.InvalidTransition);
        }

        order.Start();
        _repository.Update(order);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started production order {OrderId}", order.Id);
        return order.ToDto();
    }

    public async Task<Result<ProductionOrderDto>> CompleteAsync(Guid id, CompleteProductionOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.NotFound);
        }

        if (!order.CanComplete)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.InvalidTransition);
        }

        var endDate = request.EndDate ?? DateTime.UtcNow;
        if (endDate < order.StartDate)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.EndDateBeforeStart);
        }

        order.Complete(endDate);
        _repository.Update(order);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed production order {OrderId}", order.Id);
        return order.ToDto();
    }

    public async Task<Result<ProductionOrderDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.NotFound);
        }

        if (!order.CanCancel)
        {
            return Result.Failure<ProductionOrderDto>(Errors.ProductionOrder.InvalidTransition);
        }

        order.Cancel();
        _repository.Update(order);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cancelled production order {OrderId}", order.Id);
        return order.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Errors.ProductionOrder.NotFound);
        }

        _repository.Remove(order);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted production order {OrderId}", id);
        return Result.Success();
    }
}
