using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EmployeeManagement.Api.ErrorHandling;

// [RequestSizeLimit]/[RequestFormLimits] do enforce the limit, but when it is hit while MVC reads the form,
// model binding reports it as a generic 400. Checking Content-Length before anything is read gives a proper 413;
// a chunked body without Content-Length is still stopped by those limits (as a 400).
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectOversizedRequestAttribute(long maxBytes) : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var httpContext = context.HttpContext;
        if (httpContext.Request.ContentLength <= maxBytes)
        {
            return;
        }

        var problem = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
            httpContext,
            StatusCodes.Status413PayloadTooLarge,
            title: "The request is too large.",
            detail: $"The request body must not exceed {maxBytes} bytes.");

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
