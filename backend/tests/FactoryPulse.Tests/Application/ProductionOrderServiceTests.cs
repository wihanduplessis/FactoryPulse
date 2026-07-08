using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Services;
using FactoryPulse.Domain.Entities;
using FactoryPulse.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FactoryPulse.Tests.Application;

public class ProductionOrderServiceTests
{
    private readonly IProductionOrderRepository _repository = Substitute.For<IProductionOrderRepository>();
    private readonly IMachineRepository _machineRepository = Substitute.For<IMachineRepository>();
    private readonly IValidator<CreateProductionOrderRequest> _createValidator = Substitute.For<IValidator<CreateProductionOrderRequest>>();
    private readonly IValidator<UpdateProductionOrderRequest> _updateValidator = Substitute.For<IValidator<UpdateProductionOrderRequest>>();
    private readonly ILogger<ProductionOrderService> _logger = Substitute.For<ILogger<ProductionOrderService>>();

    private ProductionOrderService CreateService()
    {
        return new ProductionOrderService(_repository, _machineRepository, _createValidator, _updateValidator, _logger);
    }

    private static CreateProductionOrderRequest ValidCreateRequest()
    {
        return new CreateProductionOrderRequest
        {
            OrderNumber = "ORD-1",
            ProductName = "Widget",
            Quantity = 5,
            StartDate = DateTime.UtcNow,
            MachineId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProductionOrder?)null);
        var service = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.ShouldBeTrue();
        result.FirstError.ShouldBe(Errors.ProductionOrder.NotFound);
    }

    [Fact]
    public async Task CreateAsync_WhenMachineRetired_ShouldReturnMachineRetired()
    {
        var request = new CreateProductionOrderRequest
        {
            OrderNumber = "ORD-1",
            ProductName = "Widget",
            Quantity = 5,
            StartDate = DateTime.UtcNow,
            MachineId = Guid.NewGuid()
        };

        _createValidator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var retiredMachine = new Machine { Name = "Old", Status = MachineStatus.Retired };
        _machineRepository.GetByIdAsync(request.MachineId, Arg.Any<CancellationToken>())
            .Returns(retiredMachine);

        var service = CreateService();

        var result = await service.CreateAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.FirstError.ShouldBe(Errors.ProductionOrder.MachineRetired);
    }

    [Fact]
    public async Task CreateAsync_WhenMachineNotFound_ShouldReturnMachineNotFound()
    {
        var request = ValidCreateRequest();
        _createValidator.ValidateAsync(request, Arg.Any<CancellationToken>()).Returns(new ValidationResult());
        _machineRepository.GetByIdAsync(request.MachineId, Arg.Any<CancellationToken>()).Returns((Machine?)null);

        var result = await CreateService().CreateAsync(request);

        result.FirstError.ShouldBe(Errors.ProductionOrder.MachineNotFound);
    }

    [Fact]
    public async Task CreateAsync_WhenOrderNumberExists_ShouldReturnDuplicate()
    {
        var request = ValidCreateRequest();
        _createValidator.ValidateAsync(request, Arg.Any<CancellationToken>()).Returns(new ValidationResult());
        _machineRepository.GetByIdAsync(request.MachineId, Arg.Any<CancellationToken>())
            .Returns(new Machine { Name = "M", Status = MachineStatus.Idle });
        _repository.OrderNumberExistsAsync(request.OrderNumber, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateAsync(request);

        result.FirstError.ShouldBe(Errors.ProductionOrder.DuplicateOrderNumber);
    }

    [Fact]
    public async Task CreateAsync_WhenValidationFails_ShouldReturnValidationErrors()
    {
        var request = ValidCreateRequest();
        var failures = new List<ValidationFailure> { new("Quantity", "Quantity must be greater than zero.") };
        _createValidator.ValidateAsync(request, Arg.Any<CancellationToken>()).Returns(new ValidationResult(failures));

        var result = await CreateService().CreateAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ShouldSaveAndSucceed()
    {
        var request = ValidCreateRequest();
        _createValidator.ValidateAsync(request, Arg.Any<CancellationToken>()).Returns(new ValidationResult());
        _machineRepository.GetByIdAsync(request.MachineId, Arg.Any<CancellationToken>())
            .Returns(new Machine { Name = "M", Status = MachineStatus.Idle });
        _repository.OrderNumberExistsAsync(request.OrderNumber, Arg.Any<CancellationToken>()).Returns(false);

        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ProductionOrder.Create("ORD-1", "Widget", 5, DateTime.UtcNow, request.MachineId));

        var result = await CreateService().CreateAsync(request);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<ProductionOrder>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenPlanned_ShouldSucceedAndBeRunning()
    {
        var order = ProductionOrder.Create("ORD-1", "Widget", 5, DateTime.UtcNow, Guid.NewGuid());
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(order);

        var result = await CreateService().StartAsync(Guid.NewGuid());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("Running");
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldReturnInvalidTransition()
    {
        var order = ProductionOrder.Create("ORD-1", "Widget", 5, DateTime.UtcNow, Guid.NewGuid());
        order.Start();
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(order);

        var result = await CreateService().StartAsync(Guid.NewGuid());

        result.FirstError.ShouldBe(Errors.ProductionOrder.InvalidTransition);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_WhenEndDateBeforeStart_ShouldReturnEndDateBeforeStart()
    {
        var order = ProductionOrder.Create("ORD-1", "Widget", 5, new DateTime(2026, 5, 1), Guid.NewGuid());
        order.Start();
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(order);
        var request = new CompleteProductionOrderRequest { EndDate = new DateTime(2026, 4, 1) };

        var result = await CreateService().CompleteAsync(Guid.NewGuid(), request);

        result.FirstError.ShouldBe(Errors.ProductionOrder.EndDateBeforeStart);
    }

}
