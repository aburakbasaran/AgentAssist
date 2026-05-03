using AgentAssist.Api.Contracts;
using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Common;
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
            .WithDescription("Returns a deterministic Phase A mock answer grounded in retrieved chunks.")
            .Produces<AssistantAnswer>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<Results<Ok<AssistantAnswer>, ValidationProblem, ProblemHttpResult>> HandleQueryAsync(
        AssistantQueryRequest request,
        IRequestHandler<AssistantQuery, Result<AssistantAnswer>> handler,
        CancellationToken ct)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var query = new AssistantQuery
        {
            Question = request.Question,
            UserId = request.UserId,
            Roles = request.Roles
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

    private static Dictionary<string, string[]> Validate(AssistantQueryRequest request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        if (Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            return [];
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            var members = result.MemberNames.Any()
                ? result.MemberNames
                : [string.Empty];

            foreach (var member in members)
            {
                errors[member] = [result.ErrorMessage ?? "Validation failed."];
            }
        }

        return errors;
    }
}
