using FactoryPulse.Application.DTOs;
using FluentValidation;

namespace FactoryPulse.Application.Validators;

public class CreateMachineRequestValidator : AbstractValidator<CreateMachineRequest>
{
    public CreateMachineRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(request => request.Description)
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");
    }
}
