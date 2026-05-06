# ADR-0010: Pilot User Context — Header-based; Production Auth Out of Scope

This reference architecture targets an internal pilot; **production-grade authentication is intentionally out of scope** for this sprint. Endpoints stay `.AllowAnonymous()`; user identity flows through an `IUserContextProvider` abstraction so the production swap to Entra ID is purely an Infrastructure-layer concern.

## Decision

The Application layer depends on `IUserContextProvider` for `UserId`, `Roles`, and `Location`. The handler ignores `userId` and `roles` fields submitted in the request body — they are tolerated for backward compatibility but the retrieval filter is fed exclusively from the user context provider.

Two `IUserContextProvider` implementations live in Infrastructure:

- `HeaderUserContextProvider` — reads pilot headers `X-Agent-User`, `X-Agent-Roles` (comma-separated), `X-Agent-Location`. Header values are validated against length and character allow-lists, and roles are filtered through the same allow-list used by the search filter builder. This provider is registered **only** when `IHostEnvironment.IsDevelopment()` or the environment name equals `InternalPilot`, and only when `AgentAssistOptions.AllowHeaderUserContext` is `true`.
- `MockUserContextProvider` — returns a fixed pilot identity (`pilot-user`, `["agent"]`, `branch-a`) and is the fallback when the header provider cannot be registered. Startup logs a warning so operators notice that pilot context is not active.

There is **no** production-mode anonymous fail-fast guard; the app is reference architecture only and is not intended for unattended public exposure.

## Out of Scope (Production Hardening Gaps)

When this reference architecture is taken to production, the following must be added before exposure to untrusted networks:

1. **Entra ID** — app registration, JWT bearer (e.g. `Microsoft.Identity.Web`), and policy-based authorization (`AddAuthorizationBuilder().AddPolicy("AssistantAccess", ...)`).
2. **APIM** — rate limiting, WAF, JWT validation, IP allowlist.
3. **Private networking** — Private Endpoints / VNet integration for App Service, Azure SQL, Azure AI Search, Azure OpenAI, and Key Vault.
4. **Production-mode anonymous fail-fast guard** — startup-time check that rejects `.AllowAnonymous()` endpoints in `Production` environment.
5. **Dedicated identity provider for the header path** — once Entra ID is wired, `HeaderUserContextProvider` should be retired or kept behind a stricter feature flag.

These gaps are tracked in [`docs/quality-gates/production-pilot-quality-gate.md`](../quality-gates/production-pilot-quality-gate.md) and the Azure setup guide's "Optional production hardening" section.

## Rationale

- The article-driven goal is to demonstrate "designing a system, not a chatbot": citation-first answers, structured refusal, audit, and clean architecture boundaries — not authentication.
- A header-based provider lets pilot operators exercise role/location-based retrieval without the complexity of Entra ID, while preserving the abstraction so production migration is a registration swap.
- Documenting auth gaps explicitly is more honest than implementing a partial auth layer that gives a false sense of security.

This ADR is finalised in Slice 6; implementation lives in `src/AgentAssist.Infrastructure/Identity/` and the registration logic lives in the Mock and Azure DI extension files.
