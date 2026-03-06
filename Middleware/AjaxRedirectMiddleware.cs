using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EmployeeManagementSystem.Middleware
{
    public class AjaxRedirectMiddleware
    {
        private readonly RequestDelegate _next;

        public AjaxRedirectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            // Check if it's an AJAX request and we're redirecting to the login page
            if (context.Response.StatusCode == 302 && 
                context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var location = context.Response.Headers["Location"].ToString();
                if (location.Contains("/Account/Login", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Clear the redirect and set a custom header so JS can handle it
                    context.Response.Clear();
                    context.Response.StatusCode = 401; // Unauthorized
                    context.Response.Headers.Append("X-Ajax-Redirect", location);
                }
            }
        }
    }
}
