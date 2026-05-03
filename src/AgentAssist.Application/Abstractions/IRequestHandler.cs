namespace AgentAssist.Application.Abstractions;

/// <summary>
/// Handles an application request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}
