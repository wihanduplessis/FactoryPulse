using FactoryPulse.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.API.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult HandleFailure(IReadOnlyList<Error> errors)
    {
        Error error = errors[0];

        int statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode:  statusCode, title: error.Code, detail: error.Description);
    }
}
