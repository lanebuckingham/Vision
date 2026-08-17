using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Vision.CredentialService.API.ExceptionHandling;

/// <summary>
/// Maps known exceptions to RFC Problem Details responses.
/// Unknown exceptions produce a generic 500 without leaking internals.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationEx => CreateValidationProblem(validationEx),
            KeyNotFoundException notFoundEx => CreateNotFoundProblem(notFoundEx),
            InvalidOperationException domainEx => CreateConflictProblem(domainEx),
            ArgumentException argEx => CreateBadRequestProblem(argEx),
            _ => CreateServerErrorProblem(exception)
        };

        if (problemDetails.Status >= 500)
        {
            logger.LogError(exception, "Unhandled exception occurred");
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };
    }

    private static ProblemDetails CreateNotFoundProblem(KeyNotFoundException exception) => new()
    {
        Title = "Resource not found",
        Detail = exception.Message,
        Status = StatusCodes.Status404NotFound,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
    };

    private static ProblemDetails CreateConflictProblem(InvalidOperationException exception) => new()
    {
        Title = "Business rule violation",
        Detail = exception.Message,
        Status = StatusCodes.Status409Conflict,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
    };

    private static ProblemDetails CreateBadRequestProblem(ArgumentException exception) => new()
    {
        Title = "Invalid request",
        Detail = exception.Message,
        Status = StatusCodes.Status400BadRequest,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
    };

    private static ProblemDetails CreateServerErrorProblem(Exception _) => new()
    {
        Title = "An unexpected error occurred",
        Status = StatusCodes.Status500InternalServerError,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };
}
