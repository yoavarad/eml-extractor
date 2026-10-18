# ADR-017: MIT license with a third-party notices file

## Status
Accepted (owner, 2026-10-18): MIT, copyright line "Copyright (c) 2026 Yoav Arad". `LICENSE`, `THIRD-PARTY-NOTICES.md` and the CI audit (`tools/license-audit.py`) are implemented.

## Context
The brief requires everything to be open source. At proposal time the repo had no `LICENSE` file, so legally it is "all rights reserved". No dependency license audit exists, and one runtime dependency, `BouncyCastle.Cryptography` (pulled in by MimeKit), is not documented anywhere.

Audit of the packages restored today (versions from the project files and `project.assets.json`):

| Package | Version | Used by | License |
| --- | --- | --- | --- |
| MimeKit | 4.18.0 | library (runtime) | MIT |
| BouncyCastle.Cryptography | 2.7.0 | library (runtime, transitive via MimeKit) | MIT |
| System.Security.Cryptography.Pkcs | 10.0.13 | library (runtime, pinned transitive) | MIT |
| xunit | 2.9.3 | tests | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | tests | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | tests | MIT |
| coverlet.collector | 6.0.4 | tests | MIT |
| BenchmarkDotNet | 0.15.8 | bench | MIT |

## Decision
- License the repo under MIT (accepted by the owner, 2026-10-18).
- Add `LICENSE` (MIT text) and `THIRD-PARTY-NOTICES.md` at the repo root. The notices file lists every shipped runtime dependency with version, license, and copyright line, and separately lists test and bench dependencies and the source and license of every committed sample file (ADR-018).
- CI (`tools/license-audit.py`) checks that every package in the restore graph appears in `THIRD-PARTY-NOTICES.md` and that its license is MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause or MS-PL. MS-PL is allowed because the test-only `Xunit.SkippableFact` (and its dependency `Validation`) use it.
- Corpus samples under the gitignored `corpus/internal/` are never redistributed; their sources are listed in `docs/edge-cases.md`.

## Alternatives
- Apache-2.0. Viable (explicit patent grant). Not chosen as the proposal because every runtime dependency is MIT and the team's other extractor's choice is unknown; the owner may still pick it.
- No license until the team decides. Rejected: the brief requires open source, and an unlicensed repo is not open source.

## Consequences
- All runtime dependencies are MIT, so MIT for the library adds no compatibility constraints.
- Library manifest added for the transitive BouncyCastle package (`ydk:library:deps/bouncycastle-cryptography`). Requirement: `ydk:req:quality/license-notices`.

## Evidence
`src/EmlExtraction/EmlExtraction.csproj`, `tests/EmlExtraction.Tests/EmlExtraction.Tests.csproj`, `benches/EmlExtraction.Bench/EmlExtraction.Bench.csproj`, `src/EmlExtraction/obj/project.assets.json` (BouncyCastle.Cryptography 2.7.0 via MimeKit 4.18.0).
