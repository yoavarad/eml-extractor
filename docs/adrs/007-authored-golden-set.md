# ADR-007: Authored golden set as the quality bar

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
The original ask covers detection, metadata, body, attachments, and correctness for unicode and for attachment mixes (`docs/golden.md`, "Why 1,200"). A golden set derived from the parser's own output would only prove the parser agrees with itself.

## Decision
- Golden cases are authored: each `expected.json` is written from the same spec the generator used to build `input.eml`. When `tools/GoldenKit gen` writes expected values it "must not call `EmlExtractor`".
- There are two sets: seed (12 cases under `corpus/golden/seed/`, run by `dotnet test`) and bulk (1,000 core + 200 RFC extras under `corpus/golden/bulk/`, run by `GoldenKit score`).
- The attachment quality bar is name + decoded size + SHA-256 of tiny typed stubs. Real Office/TNEF/CID files are covered by the gitignored public harvest instead.
- `GoldenKit score` exits non-zero on any failed case. The last recorded result is 1212/1212 cases and 11333/11333 checks (`docs/golden-score.txt`).
- Not golden-tested: full `Received` traces, warning strings, recursive Office content, latency.

## Alternatives
- Parser-dump goldens, rejected by the "extractor is never the source of truth" rule.
- Unlabeled TREC volume (`docs/golden.md`: "Unlabeled TREC volume is not this set").

## Consequences
- After a change to extraction behaviour, bulk cases are regenerated from the catalog, not re-recorded from output.
- CI (`ci.yml`) runs restore, build with warnings as errors, format verification, `dotnet test`, and a vulnerable-package audit. Of the golden set, only the 12 seed cases run there, inside `dotnet test` alongside the unit and internal-corpus tests. The 1200-case bulk score is a manual command.
- Whether the bulk score should gate CI is decided in ADR-011.

## Evidence
`docs/golden.md`, `docs/golden-score.txt`, `README.md` ("Golden set"), `tools/GoldenKit/Generator.cs`, `tools/GoldenKit/Scorer.cs`, `tests/EmlExtraction.Tests/GoldenTests.cs`.
