# ADR-0008: Azure OpenAI / Foundry IChatClient with Structured Citation Validation

The production pilot replaces `MockChatClient` with `AzureOpenAIClient.GetChatClient(deployment).AsIChatClient()` from `Microsoft.Extensions.AI`. Application code keeps consuming the vendor-neutral `IChatClient` abstraction; no custom `IAnswerGenerator` interface is introduced (ADR-0002).

The chat client is used with strict, citation-first contracts:

- The model is asked to return JSON only (`ChatOptions.ResponseFormat = ChatResponseFormat.Json`) matching `AssistantAnswerEnvelope = { answerText, citations[], confidence, refused, refusalReason }`.
- `AssistantAnswerEnvelope` is annotated with `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` — equivalent to setting `JsonSerializerOptions.UnmappedMemberHandling = Disallow` for that contract — so any unknown property in the model output is rejected at deserialisation time. `ChatResponseParser` catches the resulting `JsonException` (and treats malformed payloads identically) and triggers a structured refusal (`model_returned_malformed_response`).
- `CitationValidator` requires every returned citation ID to be a member of the retrieved chunk whitelist. Empty citations on a non-refused answer, missing IDs, or whitelist-foreign IDs trigger a refusal (`model_returned_invalid_citation`).
- Text-marker matches such as `[1]` are **not** treated as grounding proof; only structured citation IDs count.
- `Temperature = 0` for deterministic outputs; `MaxOutputTokens` is configured. Authentication uses `DefaultAzureCredential`.
- Embedding generation goes through `IEmbeddingGenerator<string, Embedding<float>>` registered via `AzureOpenAIClient.GetEmbeddingClient(deployment).AsIEmbeddingGenerator()`.
- The mock chat client is updated to also return structured JSON so the validation path runs in mock mode.

## Cache deliberately excluded; role-aware key required

Response caching is intentionally **not** implemented in this slice. The retrieval result, prompt template, citations, and refusal decision all depend on the caller's allow-listed roles and location (see ADR-0007 and ADR-0010). A naive cache keyed on the raw question text would happily return a `supervisor`-only answer to an `agent`, leaking restricted content. Any future cache layer therefore MUST:

1. Key on `{ tenantId?, hashedQuestion, normalizedRoles, normalizedLocation, providerMode, promptVersion }` — not just the question.
2. Validate the cached answer's citation list still resolves through `CitationValidator` against the freshly retrieved chunks (so revoked / re-classified documents do not stay reachable through the cache).
3. Be invalidated whenever the prompt template, allow-list, or risk classifier rules change.

Until those invariants are explicit and tested, every request goes end-to-end (retrieve → prompt → chat → validate citations) so role-restricted leakage cannot occur via cache pollution.

This ADR is finalised in Slice 3; implementation lives in `src/AgentAssist.Infrastructure/Azure/OpenAI/` and `src/AgentAssist.Application/Ai/`.
