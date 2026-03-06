using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace UserRoles.Helpers
{
    /// <summary>
    /// Catches all unhandled exceptions, logs them properly, and returns
    /// a generic error response to the client (no stack trace leak).
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied."),
                KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
                TimeoutException => (HttpStatusCode.GatewayTimeout, "The request timed out. Please try again."),
                Microsoft.EntityFrameworkCore.DbUpdateException dbEx =>
                    HandleDbException(dbEx),
                OperationCanceledException => (HttpStatusCode.BadRequest, "The request was cancelled."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
            };

            _logger.LogError(exception, "Unhandled exception — {StatusCode}: {Path} {Method}",
                (int)statusCode, context.Request.Path, context.Request.Method);

            // For AJAX/API requests, return JSON
            if (IsApiRequest(context))
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)statusCode;

                var result = JsonSerializer.Serialize(new
                {
                    success = false,
                    message,
                    statusCode = (int)statusCode
                });

                await context.Response.WriteAsync(result);
            }
            else
            {
                // For regular page requests, redirect to error page
                context.Response.Redirect($"/Home/Error/{(int)statusCode}");
            }
        }

        private static (HttpStatusCode, string) HandleDbException(Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            // Check for concurrency issues
            if (ex.InnerException?.Message?.Contains("duplicate key") == true)
                return (HttpStatusCode.Conflict, "A record with the same key already exists.");

            if (ex.InnerException?.Message?.Contains("foreign key") == true)
                return (HttpStatusCode.BadRequest, "Cannot perform this operation due to related data constraints.");

            return (HttpStatusCode.InternalServerError, "A database error occurred. Please try again.");
        }

        private static bool IsApiRequest(HttpContext context)
        {
            return context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   context.Request.Headers.Accept.ToString().Contains("application/json") ||
                   context.Request.ContentType?.Contains("application/json") == true;
        }
    }

    /// <summary>
    /// Extension method to register the middleware cleanly.
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
