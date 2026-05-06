using System.Text.Json;

using AgentAssist.Domain.Exceptions;
using global::Azure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AgentAssist.Api.Errors;

internal sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);
        if (statusCode is null)
        {
            return false;
        }

        logger.LogWarning(exception, "Mapped exception {ExceptionType} to status {StatusCode}.", exception.GetType().Name, statusCode);
        httpContext.Response.StatusCode = statusCode.Value;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private const string AuthenticationContextDetail =
        "Caller identity (user, roles, location) is supplied via the X-Agent-User / X-Agent-Roles / X-Agent-Location headers in the InternalPilot environment, or by the authenticated principal in production. Submitting these fields in the request body is not allowed.";

    private static (int? StatusCode, string Title, string Detail) MapException(Exception exception) => exception switch
    {
        DomainException domain => (StatusCodes.Status400BadRequest, "Domain rule violation", domain.Message),
        BadHttpRequestException bad when IsUnmappedMember(bad) => (StatusCodes.Status400BadRequest, "Invalid request body", AuthenticationContextDetail),
        BadHttpRequestException bad => (bad.StatusCode, "Invalid request", bad.Message),
        JsonException json when IsUnmappedMember(json) => (StatusCodes.Status400BadRequest, "Invalid request body", AuthenticationContextDetail),
        JsonException json => (StatusCodes.Status400BadRequest, "Invalid request body", json.Message),
        RequestFailedException => (StatusCodes.Status503ServiceUnavailable, "Upstream Azure service unavailable", "An Azure dependency is currently unavailable. Please retry shortly."),
        OperationCanceledException => (499, "Client closed request", "The request was cancelled before completion."),
        _ => (null, string.Empty, string.Empty)
    };

    private static bool IsUnmappedMember(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (current.Message.Contains("unmapped", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
