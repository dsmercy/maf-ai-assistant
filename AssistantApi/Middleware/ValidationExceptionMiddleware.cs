using FluentValidation;
using System.Text.Json;

namespace AssistantApi.Middleware;

/// <summary>
/// Global exception handling middleware that intercepts unhandled exceptions
/// before they reach ASP.NET's default error handler.
///
/// Handles two cases:
///   - FluentValidation.ValidationException → 400 Bad Request with field-level error details
///   - Any other unhandled exception → 500 Internal Server Error with a generic message
///
/// Registered first in the middleware pipeline so it wraps all subsequent middleware
/// including routing, authentication, and controller execution.
/// </summary>
public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Passes the request to the next middleware and catches exceptions on the way back.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "An unexpected error occurred." }));
        }
    }
}
