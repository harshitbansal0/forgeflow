using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Api.Errors;

/// <summary>Maps application exceptions to RFC 7807 problem details; unexpected errors never leak internals.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ConflictException or DomainException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "This record was changed by someone else. Reload it and try again."),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflict", "The change conflicts with existing data."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (exception is DbUpdateException)
        {
            logger.LogWarning(exception, "Database update conflict");
        }

        var problem = exception is RequestValidationException validation
            ? new ValidationProblemDetails(validation.Errors)
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
