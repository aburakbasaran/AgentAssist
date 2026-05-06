using AgentAssist.Api.Contracts;
using AgentAssist.Api.Middleware;
using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Common;
using AgentAssist.Application.Feedback;
using AgentAssist.Domain;

namespace AgentAssist.Api.Endpoints;

internal static class AssistantEndpoints
{
    internal static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/assistant")
            .WithTags("Assistant")
            .AllowAnonymous();

        group.MapPost("/query", HandleQueryAsync)
            .WithSummary("Answers an assistant query")
            .WithDescription("Returns a citation-grounded answer or a structured refusal.")
            .Produces<AssistantAnswer>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/feedback", HandleFeedbackAsync)
            .WithSummary("Records pilot feedback for an assistant answer")
            .WithDescription("Persists feedback for an answer identified by correlation id.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<Results<Ok<AssistantAnswer>, ProblemHttpResult>> HandleQueryAsync(
        AssistantQueryRequest request,
        HttpContext httpContext,
        IRequestHandler<AssistantQuery, Result<AssistantAnswer>> handler,
        CancellationToken ct)
    {
        var query = new AssistantQuery
        {
            Question = request.Question,
            Roles = Array.Empty<string>(),
            CorrelationId = CorrelationIdMiddleware.GetCurrent(httpContext)
        };

        var result = await handler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return TypedResults.Problem(result.Error);
        }

        return result.Value is { } answer
            ? TypedResults.Ok(answer)
            : TypedResults.Problem("Assistant answer was not produced.");
    }

    private static async Task<Results<Accepted, ValidationProblem>> HandleFeedbackAsync(
        FeedbackRequest request,
        HttpContext httpContext,
        IFeedbackSink sink,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(FeedbackRequest.CorrelationId)] = ["CorrelationId is required."]
            });
        }

        var record = new FeedbackRecord
        {
            CorrelationId = request.CorrelationId,
            UserId = null,
            Helpful = request.Helpful,
            Reason = request.Reason,
            Timestamp = timeProvider.GetUtcNow()
        };

        await sink.WriteAsync(record, ct);
        return TypedResults.Accepted($"/api/v1/assistant/feedback/{request.CorrelationId}");
    }
}
