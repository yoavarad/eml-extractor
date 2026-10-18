# ADR-001: C# / .NET 10 class library stack

## Status
Accepted (recorded retroactively, Stage 01 Rewrite). Amended by ADR-019: the library and test projects also target net8.0 (target, Stage 02).

## Context
The original (Hebrew) specification, as translated in `docs/requirements.md`, states "Implementation: C# / .NET 10". The first commit (`dd83290`, "Add .NET 10 EML extraction core with tagged golden set") shipped the library on that stack.

## Decision
- Language/runtime: C# on .NET 10; SDK pinned by `global.json` to `10.0.401` with `rollForward: latestFeature`.
- Shape: a class library (`src/EmlExtraction`) consumed by project reference; no executable host, no HTTP surface.
- Tests: xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.
- Tooling: `tools/GoldenKit` console app for golden generation, scoring, and harvest; `benches/EmlExtraction.Bench` for throughput.
- CI gates (`.github/workflows/ci.yml`): `dotnet build -p:TreatWarningsAsErrors=true`, `dotnet format --verify-no-changes`, `dotnet test`, and a vulnerable-package audit that fails on High/Critical.

## Alternatives
The original specification set the stack, so alternatives were not weighed at the time. The brief asks for an assessment of improving C# libraries versus another language if a large capability gap exists. [`docs/research/csharp-capability-assessment.md`](../research/csharp-capability-assessment.md) compares MimeKit on .NET 10 with Python (`email`), Java (Jakarta Mail, Apache POI HMEF) and Go (`net/mail`, `enmime`) per capability, and concludes "no big gap - stay on C#".

## Consequences
- Consumers must target .NET 10 or later.
- CI enforces formatting and warnings; local `ydk verify` runs the same dotnet build/format/test plugins plus `dotnet-quality` and `dotnet-tdd-guard` (`.ydk/config.yaml`).

## Evidence
`global.json`, `docs/requirements.md` (header), `README.md` ("Setup and run"), `.github/workflows/ci.yml`, commit `dd83290`.
