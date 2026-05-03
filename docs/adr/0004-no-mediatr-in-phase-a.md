# ADR-0004: No MediatR In Phase A

Phase A uses a small `IRequestHandler<TRequest, TResponse>` contract instead of MediatR because the current slice has one use case and no cross-cutting pipeline requirements. If later phases add behaviors such as authorization policies, retries, metrics, or validation pipelines that justify a mediator, it can be introduced without changing Domain models.
