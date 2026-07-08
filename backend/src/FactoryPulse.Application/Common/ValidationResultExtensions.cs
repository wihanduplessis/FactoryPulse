using FluentValidation.Results;

namespace FactoryPulse.Application.Common;

public static class ValidationResultExtensions
{
    public static IReadOnlyList<Error> ToErrors(this ValidationResult validationResult)
    {
        return validationResult.Errors
            .Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage))
            .ToList();
    }
}
