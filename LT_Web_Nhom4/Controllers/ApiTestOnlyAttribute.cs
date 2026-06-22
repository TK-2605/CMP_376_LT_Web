using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LT_Web_Nhom4.Controllers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ApiTestOnlyAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            if (environment.IsDevelopment() || configuration.GetValue<bool>("ApiTest:Enabled"))
            {
                await next();
                return;
            }

            context.Result = new NotFoundResult();
        }
    }
}
