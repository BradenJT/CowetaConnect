using Hangfire.Dashboard;

namespace CowetaConnect.API.Middleware;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated Admin users.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
