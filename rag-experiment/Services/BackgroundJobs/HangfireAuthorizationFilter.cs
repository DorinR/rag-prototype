using Hangfire.Dashboard;

namespace rag_experiment.Services.BackgroundJobs
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // In development, allow access to dashboard
            if (httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                return true;
            }

            // In production, require authentication
            return httpContext.User?.Identity?.IsAuthenticated == true;
        }
    }
}