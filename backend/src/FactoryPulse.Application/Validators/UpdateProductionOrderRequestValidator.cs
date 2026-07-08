using FactoryPulse.Application.DTOs;
using FluentValidation;

namespace FactoryPulse.Application.Validators;

public class UpdateProductionOrderRequestValidator : AbstractValidator<UpdateProductionOrderRequest>
{
    public UpdateProductionOrderRequestValidator()
    {
        RuleFor(request => request.ProductName)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
    }
}
