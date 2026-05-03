# Agent Assist Enterprise .NET Azure Project

**Status:** Phase A delivered (mock vertical slice). Phase B (Azure AI Search adapter) is the next milestone.

Production-grade reference architecture for regulated-industry RAG, in progress, building in public. Phase A is a mock-first vertical slice: it uses deterministic in-memory knowledge, a mock `IChatClient`, embedded prompt templates, and no Azure SDK calls.

The healthcare-flavored documents in this repository are illustrative. They use placeholder names such as Acme Sağlık Grubu, Şube A/B/C, and Doktor X.

## Testing Stack

The test stack uses xUnit v3, NSubstitute, Microsoft `FakeTimeProvider`, and AwesomeAssertions (MIT-licensed FluentAssertions community fork).

## Run

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
dotnet run --project src/AgentAssist.Api
```

## Example Requests

```powershell
curl.exe -X POST "https://localhost:5001/api/v1/assistant/query" -H "Content-Type: application/json" -H "X-Correlation-Id: demo-123" -d "{\"question\":\"MR randevu hazırlık bilgisi nedir?\",\"roles\":[\"agent\"]}"
```

```powershell
curl.exe -X POST "https://localhost:5001/api/v1/assistant/query" -H "Content-Type: application/json" -d "{\"question\":\"tamamen alakasız bilinmeyen konu\",\"roles\":[\"agent\"]}"
```
## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.