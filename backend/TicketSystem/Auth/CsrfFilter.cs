using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TicketSystem.Auth
{
    // Every POST, PUT and DELETE must carry the antiforgery token (X-XSRF-TOKEN header, from GET
    // /api/auth/csrf) that matches the antiforgery cookie. A cross-site page can make the browser
    // send the cookies but can't read the token. MVC's AutoValidateAntiforgeryToken does the same
    // but needs the view services, which an API-only app doesn't register.
    public class CsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var method = context.HttpContext.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
                return;

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Missing or expired security token",
                    Detail = "Reload the page and try again."
                });
            }
        }
    }
}
