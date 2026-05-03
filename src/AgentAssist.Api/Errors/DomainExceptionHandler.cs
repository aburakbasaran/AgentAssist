using AgentAssist.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace AgentAssist.Api.Errors;

internal sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        logger.LogWarning(exception, "Domain exception handled.");
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Domain rule violation",
            Detail = domainException.Message,
            Type = "https://httpstatuses.com/400"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
