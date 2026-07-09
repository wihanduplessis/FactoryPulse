using FactoryPulse.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.API.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult HandleFailure(IReadOnlyList<Error> errors)
    {
        Error primaryError = errors[0];

        if (primaryError.Type == ErrorType.Validation)
        {
            var errorDictionary = errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errorDictionary));
        }

        int statusCode = primaryError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode:  statusCode, title: primaryError.Code, detail: primaryError.Description);
    }
}
