# ADR-019: Multi-target net8.0 and net10.0

## Status
Accepted (per project owner, 2026-10-18). Implemented (task #30). Amends ADR-001. Amended by ADR-020: the bench project also multi-targets net8.0 and net10.0 (GoldenKit stays net10.0 only).

## Context
ADR-001 fixes the stack at C# on .NET 10, as the brief says. The team's extraction template, which will host this component, may still run on .NET 8 LTS. A net10.0-only library cannot be referenced from a net8.0 host, so the component would be unusable there until the template moves.

.NET 8 LTS support ends on 2026-11-10, so the net8.0 target is a bridge, not a long-term commitment.

Package-level facts checked on 2026-10-18 in the local NuGet cache (nuspec dependency groups):
- MimeKit 4.18.0 ships `net8.0` and `net10.0` assets (also netstandard2.0/2.1 and .NET Framework).
- System.Security.Cryptography.Pkcs 10.0.13 ships `net8.0`, `net9.0`, and `net10.0` assets.
- BouncyCastle.Cryptography 2.7.0 ships a `net6.0` asset, which net8.0 consumes.

## Decision
Per project owner, 2026-10-18:
- `src/EmlExtraction` and `tests/EmlExtraction.Tests` target both `net8.0` and `net10.0` (`TargetFrameworks`, set in `Directory.Build.props` through the `EmlMultiTarget` property; it also covers the bench per ADR-020 and the `RealisticSamples` project the tests reference). Every other project defaults to `net10.0`, so `tools/GoldenKit` stays net10.0 only.
- CI builds and runs the tests on both target frameworks, either as a matrix or as one multi-target `dotnet test` run. CI installs the .NET 8 runtime next to the SDK pinned by `global.json`.
- The golden score gate (ADR-011) runs on net10.0; running it on net8.0 too is optional.
- The load test (ADR-015) reports the target framework; its committed baseline is for net10.0.
- The public API, behaviour, and warnings are identical on both targets. Any difference is a defect.

### Risks the Stage 02 task must verify
- The `System.Security.Cryptography.Pkcs` 10.0.13 pin (ADR-009) restores and passes the vulnerability audit on net8.0; if not, the pin gets per-target handling (a conditional package reference).
- MimeKit 4.18.0 behaves the same on net8.0 (golden seed and unit tests pass on both targets).
- No .NET 9/10-only API is used by the library or tests; any that is gets an `#if NET9_0_OR_GREATER` branch or a rewrite. With `LangVersion` `latest`, language features that need newer runtime support must compile and run on net8.0, or be avoided.
- The .NET 10 SDK pinned by `global.json` builds the net8.0 target (it does, as a supported down-level target; to be confirmed in CI).
- The net8.0 test run needs the .NET 8 runtime installed on CI runners and developer machines.

## Alternatives
- net10.0 only (status quo). Rejected for now: the component could not be loaded by a net8.0 template.
- net8.0 only. Rejected: the brief names .NET 10, and .NET 8 support ends in weeks.
- netstandard2.0. Rejected: loses modern APIs (span overloads, `ReadOnlyMemory<byte>` ergonomics) and adds a compatibility surface nobody asked for.
- Multi-target GoldenKit and the bench too. Rejected: developer tools, not shipped; doubling their runtime buys nothing. (Bench part reversed by ADR-020 for profiling.)

## Consequences
- Every library and test change must build and pass on both targets.
- CI time grows by one test run.
- Drop net8.0 once the team template is on net10 (`docs/team-questions.md`); the removal is a one-line project change plus removing CI's .NET 8 runtime step.
- Requirement: `ydk:req:quality/net8-multi-targeting`.

## Evidence
`Directory.Build.props` (`net10.0`, `LangVersion latest`), `global.json` (SDK 10.0.401), `.github/workflows/ci.yml` (`setup-dotnet` from `global.json`), nuspecs of MimeKit 4.18.0, System.Security.Cryptography.Pkcs 10.0.13, BouncyCastle.Cryptography 2.7.0. Project owner decision, 2026-10-18.
