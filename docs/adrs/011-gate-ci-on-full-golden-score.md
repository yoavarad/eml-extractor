# ADR-011: Gate CI on the full golden score

## Status
Accepted (per project owner, 2026-10-17). Implementation pending, tracked as Stage 02 work.

## Context
ADR-007 makes the authored golden set the quality bar. Today CI runs only the 12 seed cases, inside `dotnet test`. The 1,200 bulk cases are scored by hand with `GoldenKit score`, and the report is committed to `docs/golden-score.txt`. A regression in the bulk set is therefore caught only if someone remembers to re-run the score. The last recorded result is 1212/1212 cases and 11333/11333 checks.

## Decision
Per project owner, 2026-10-17:
- CI runs the full GoldenKit score on every change: all 1,200 bulk cases plus the 12 seed cases.
- CI fails when the score is below the current baseline of 1212/1212 cases, i.e. on any failed case.

## Alternatives
- Keep the bulk score manual (status quo). Rejected: bulk regressions would go unnoticed until the next manual run.

## Consequences
- `.github/workflows/ci.yml` needs a step that runs `GoldenKit score` over the committed `corpus/golden/` cases. `GoldenKit score` already exits non-zero on any failed case.
- CI time grows by one full scoring run.
- Until the step exists, the specs describe this as target behaviour, and CI still runs only the seed set (`docs/specs/quality.md`, `docs/specs/testing.md`, `docs/specs/scope.md`, `ydk:nfr:reliability/golden-pass-rate`).

## Evidence
Project owner decision, 2026-10-17. Baseline: `docs/golden-score.txt`. Current CI: `.github/workflows/ci.yml`.
