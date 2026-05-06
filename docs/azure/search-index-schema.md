# Azure AI Search Index Schema (Production Pilot)

> **Note:** the resource names in the CLI snippets below (`<your-rg>`, `<your-search-service>`, `<your-openai-account>`, `<your-sql-server>`) are intentionally placeholder strings. None of them refers to a live tenant; substitute your own values before running any command.

The DevCloud retrieval adapter ([`src/AgentAssist.Infrastructure/Azure/Search/`](../../src/AgentAssist.Infrastructure/Azure/Search)) targets a single index with the schema below. Field names use camelCase to match the JSON serialised form of `AzureSearchDocument`.

## Field Definitions

| Field | Type | Searchable | Filterable | Facetable | Retrievable | Notes |
|---|---|---|---|---|---|---|
| `id` | `Edm.String` (key) | yes | yes | no | yes | Document key. Convention: `<documentId>::<chunkId>`. |
| `documentId` | `Edm.String` | no | yes | no | yes | Source document identifier. |
| `chunkId` | `Edm.String` | no | yes | no | yes | Chunk identifier within the document; **citation whitelist key** consumed by `CitationValidator` in Slice 3. |
| `title` | `Edm.String` | yes | no | no | yes | Document title; analyzer `tr.microsoft` if Turkish content dominates the corpus. |
| `content` | `Edm.String` | yes | no | no | yes | Chunk text; analyzer matches `title`. |
| `allowedRoles` | `Collection(Edm.String)` | no | yes | yes | yes | Roles that may retrieve the chunk; matched via `search.in(...)` against the safe filter builder. Allow-list values: `agent`, `supervisor`. |
| `documentType` | `Edm.String` | no | yes | yes | yes | Domain enum: `Procedure`, `Campaign`, `Guidance`, `Administrative`. |
| `riskLevel` | `Edm.String` | no | yes | yes | yes | Domain enum: `Low`, `Medium`, `High`. |
| `isActive` | `Edm.Boolean` | no | yes | yes | yes | Soft-delete flag; the filter builder always applies `isActive eq true`. |
| `location` | `Edm.String` | no | yes | yes | yes | Optional location filter. Allow-list values: `branch-a`, `branch-b`, `branch-c`. |
| `updatedAt` | `Edm.DateTimeOffset` | no | yes | yes | yes | Last update timestamp (UTC). |
| `contentVector` | `Collection(Edm.Single)` | n/a | n/a | n/a | yes | Vector embedding of the chunk content. Default dimension matches `text-embedding-3-large` (3072). Switch to 1536 dimensions for `text-embedding-3-small`. |

## Vector Profile and Algorithm

```jsonc
{
  "vectorSearch": {
    "algorithms": [
      { "name": "agentassist-hnsw", "kind": "hnsw" }
    ],
    "profiles": [
      { "name": "agentassist-vector-profile", "algorithm": "agentassist-hnsw" }
    ]
  }
}
```

`contentVector` uses the `agentassist-vector-profile` profile and `cosine` distance.

## Semantic Configuration

```jsonc
{
  "semantic": {
    "configurations": [
      {
        "name": "agentassist-semantic",
        "prioritizedFields": {
          "titleField": { "fieldName": "title" },
          "prioritizedContentFields": [{ "fieldName": "content" }],
          "prioritizedKeywordsFields": []
        }
      }
    ]
  }
}
```

The Infrastructure adapter sets `SemanticSearchOptions.SemanticConfigurationName = "agentassist-semantic"` when the option is configured.

## Sample Index Creation Payload

Save the following as `index.json` and create the index:

```bash
az search service show -g <your-rg> -n <your-search-service> --query "name" -o tsv

az rest \
  --method POST \
  --url "https://<your-search-service>.search.windows.net/indexes?api-version=2024-07-01" \
  --body @index.json \
  --resource "https://search.azure.com"
```

```json
{
  "name": "agentassist-knowledge",
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true, "searchable": false, "filterable": true, "retrievable": true },
    { "name": "documentId", "type": "Edm.String", "filterable": true, "retrievable": true, "searchable": false },
    { "name": "chunkId", "type": "Edm.String", "filterable": true, "retrievable": true, "searchable": false },
    { "name": "title", "type": "Edm.String", "searchable": true, "retrievable": true, "analyzer": "tr.microsoft" },
    { "name": "content", "type": "Edm.String", "searchable": true, "retrievable": true, "analyzer": "tr.microsoft" },
    { "name": "allowedRoles", "type": "Collection(Edm.String)", "filterable": true, "facetable": true, "retrievable": true },
    { "name": "documentType", "type": "Edm.String", "filterable": true, "facetable": true, "retrievable": true },
    { "name": "riskLevel", "type": "Edm.String", "filterable": true, "facetable": true, "retrievable": true },
    { "name": "isActive", "type": "Edm.Boolean", "filterable": true, "facetable": true, "retrievable": true },
    { "name": "location", "type": "Edm.String", "filterable": true, "facetable": true, "retrievable": true },
    { "name": "updatedAt", "type": "Edm.DateTimeOffset", "filterable": true, "facetable": true, "retrievable": true },
    {
      "name": "contentVector",
      "type": "Collection(Edm.Single)",
      "searchable": true,
      "retrievable": true,
      "dimensions": 3072,
      "vectorSearchProfile": "agentassist-vector-profile"
    }
  ],
  "vectorSearch": {
    "algorithms": [
      { "name": "agentassist-hnsw", "kind": "hnsw" }
    ],
    "profiles": [
      { "name": "agentassist-vector-profile", "algorithm": "agentassist-hnsw" }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "agentassist-semantic",
        "prioritizedFields": {
          "titleField": { "fieldName": "title" },
          "prioritizedContentFields": [{ "fieldName": "content" }]
        }
      }
    ]
  }
}
```

## Sample Document (Fictional)

```json
{
  "value": [
    {
      "@search.action": "upload",
      "id": "DOC-MR-001::CHK-001",
      "documentId": "DOC-MR-001",
      "chunkId": "CHK-001",
      "title": "MR randevu öncesi hazırlık",
      "content": "Acme Sağlık Grubu için MR randevusu öncesinde metal aksesuarlar çıkarılır...",
      "allowedRoles": ["agent", "supervisor"],
      "documentType": "Guidance",
      "riskLevel": "Medium",
      "isActive": true,
      "location": "branch-a",
      "updatedAt": "2026-04-01T08:00:00Z",
      "contentVector": [0.013, -0.007, 0.0042]
    }
  ]
}
```

The vector field is truncated for readability; the real value carries either 1536 or 3072 dimensions depending on the embedding deployment.

## Knowledge Ingestion

Automated ingestion is **out of scope** for this sprint. Push documents manually via:

```bash
az rest \
  --method POST \
  --url "https://<your-search-service>.search.windows.net/indexes/agentassist-knowledge/docs/index?api-version=2024-07-01" \
  --body @documents.json \
  --resource "https://search.azure.com"
```

A future hardening sprint may introduce Azure AI Search indexers, Azure Functions, or `azd` push pipelines.
