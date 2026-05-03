# ADR-0003: Mock-First Vertical Slice

Phase A implements the full assistant request path with in-memory search, keyword risk classification, embedded prompt templates, mock chat generation, and log-only audit events. This gives a working behavioral contract before any Azure AI Search, Azure OpenAI, persistence, or identity integration is introduced.
