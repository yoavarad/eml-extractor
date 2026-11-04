# ADR-011: Gate CI on the full golden score

## Status
Accepted (per project owner, 2026-10-17). Implemented: `.github/workflows/ci.yml` runs the GoldenKit score step.

## Context
ADR-007 makes the authored golden set the quality bar. Before this decision CI ran only the 12 seed cases, inside `dotnet test`, and the 1,200 bulk cases were scored by hand with `GoldenKit score` (report committed to `docs/golden-score.txt`), so a bulk regression was caught only if someone re-ran the score. The last recorded result is 1212/1212 cases and 11333/11333 checks.

## Decision
Per project owner, 2026-10-17:
- CI runs the full GoldenKit score on every change: all 1,200 bulk cases plus the 12 seed cases.
- CI fails when the score is below the current baseline of 1212/1212 cases, i.e. on any failed case.

## Alternatives
- Keep the bulk score manual (status quo). Rejected: bulk regressions would go unnoticed until the next manual run.

## Consequences
- `.github/workflows/ci.yml` has a step that runs `GoldenKit score` over the committed `corpus/golden/` cases. `GoldenKit score` already exits non-zero on any failed case.
- CI time grows by one full scoring run.
- The step exists ("GoldenKit score (1212/1212 gate, net10.0)" in `.github/workflows/ci.yml`), so CI runs the full score, not only the seed set. The specs describe it as implemented (`docs/specs/quality.md`, `docs/specs/testing.md`, `docs/specs/scope.md`, `ydk:nfr:reliability/golden-pass-rate`).

## Evidence
Project owner decision, 2026-10-17. Baseline: `docs/golden-score.txt`. Current CI: `.github/workflows/ci.yml`.
