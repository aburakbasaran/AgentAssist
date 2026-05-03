# ADR-0002: Microsoft.Extensions.AI Foundation

Application code consumes `Microsoft.Extensions.AI.IChatClient` directly because it is a vendor-neutral abstraction and keeps answer generation replaceable without adding a custom answer-generator interface. Microsoft.Agents.AI is deferred until a later phase because Phase A only needs a deterministic mock chat client and Phase C can register a real Azure OpenAI-backed `IChatClient` without changing Application or Domain code.
