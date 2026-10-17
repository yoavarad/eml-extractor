# ADR-001: C# / .NET 10 class library stack

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
The original (Hebrew) specification, as translated in `docs/requirements.md`, states "Implementation: C# / .NET 10". The first commit (`dd83290`, "Add .NET 10 EML extraction core with tagged golden set") shipped the library on that stack.

## Decision
- Language/runtime: C# on .NET 10; SDK pinned by `global.json` to `10.0.401` with `rollForward: latestFeature`.
- Shape: a class library (`src/EmlExtraction`) consumed by project reference; no executable host, no HTTP surface.
- Tests: xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.
- Tooling: `tools/GoldenKit` console app for golden generation, scoring, and harvest; `benches/EmlExtraction.Bench` for throughput.
- CI gates (`.github/workflows/ci.yml`): `dotnet build -p:TreatWarningsAsErrors=true`, `dotnet format --verify-no-changes`, `dotnet test`, and a vulnerable-package audit that fails on High/Critical.

## Alternatives
Not recorded in the repo. The original specification set the stack.

## Consequences
- Consumers must target .NET 10 or later.
- CI enforces formatting and warnings; local `ydk verify` runs the same dotnet build/format/test plugins.

## Evidence
`global.json`, `docs/requirements.md` (header), `README.md` ("Setup and run"), `.github/workflows/ci.yml`, commit `dd83290`.
