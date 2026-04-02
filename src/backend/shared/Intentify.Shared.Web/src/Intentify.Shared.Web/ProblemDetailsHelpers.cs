using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Intentify.Shared.Web;

public static class ProblemDetailsHelpers
{
    private const string ProductionMessage = "An unexpected error occurred.";

    public static ProblemDetails CreateInternalErrorProblemDetails()
    {
        return new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = ProductionMessage
        };
    }

    public static ProblemDetails CreateExceptionProblemDetails(Exception exception, bool isDevelopment)
    {
        return new ProblemDetails
        {
            Title = "Unhandled exception",
            Status = StatusCodes.Status500InternalServerError,
            Detail = isDevelopment ? exception.Message : ProductionMessage
        };
    }

    public static ProblemDetails CreateValidationProblemDetails(IReadOnlyDictionary<string, string[]> errors)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest
        };

        problemDetails.Extensions["errors"] = errors;
        return problemDetails;
    }
}
