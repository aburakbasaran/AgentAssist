# ADR-0001: .NET 10 LTS Runtime

Phase A uses .NET 10 and C# 14 because the project is intended as a current long-term reference architecture for ASP.NET Core Minimal APIs, built-in validation, OpenAPI, and modern language defaults. The solution centralizes nullable context, warning policy, implicit usings, and language version through `Directory.Build.props` so all projects share the same runtime assumptions.
