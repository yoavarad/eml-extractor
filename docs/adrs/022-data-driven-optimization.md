# ADR-022: Data-driven optimization process

## Status
Accepted (per project owner, 2026-10-18). Target; not implemented (Stage 02). Performance targets live in the NFR component manifests; provisional targets were set by the triage (#74, `docs/perf/triage-2026-10-18.md`) from the first profile report (ADR-020) and are mirrored in the "Targets (provisional)" section below.

## Context
ADR-015 gives a measured baseline and ADR-020 a hotspot report. The owner decided (2026-10-18) that optimization follows from those data, not from intuition. Optimizations in a parser library carry real risk: a faster path that changes a decoded byte breaks the golden set, and a wider public surface (for example span-returning attachment data) is a contract change.

## Decision
Per project owner, 2026-10-18.

### Targets
- The source of targets is the NFR component manifests: `ydk:nfr:performance/memory-footprint`, the throughput NFRs under `ydk:nfr:performance/`, and `ydk:nfr:observability/metrics-overhead`. The manifests hold the numbers; the "Targets (provisional)" section at the end of this ADR mirrors them so the closing task can mark each met or not met.
- Targets are set from the profile report, relative to the committed ADR-015 baseline (`docs/perf-baseline.json`). The triage task records each one in the relevant NFR manifest, marked provisional until the closing re-profile confirms it.
- A new ADR is needed only if the contract (this process or its merge rules) changes, not when a target changes.

### One optimization, one task
Each optimization is its own Stage 02 task under the optimization story and states:
- the hypothesis: hotspot row in the report, suspected cause, change, expected gain in a named metric;
- a before/after measurement on the same machine, same TFMs, same GC mode: the ADR-020 per-stage benchmark for the stage, plus the ADR-015 sweep T(1), T(cores), A, and P(cores);
- the result, kept or reverted: a change that does not beat run-to-run noise (spread measured by the load-limits task) is not merged.

### Merge rules
- Golden score stays 1212/1212 (ADR-011); all tests pass on net8.0 and net10.0 (ADR-019).
- No public API change without its own ADR (ADR-012 is the contract).
- The ADR-015 regression rule applies across metrics: an optimization of one metric may not regress throughput, allocation per input byte, or peak working set past the warn threshold on the comparable baseline.
- Telemetry (ADR-021) and its overhead budget stay intact.
- Never optimize without a profile that shows the hotspot (ADR-020).

### Candidate areas (hypotheses only, not commitments)
Each becomes a task only if the report shows it as a hotspot:
- attachment `byte[]` copies: decode straight into the final array instead of via an intermediate stream copy (attachment copy factor above 1.0);
- MimeKit parse options: parser and format options that skip work the library does not use;
- string allocations in header mapping: repeated lower-casing, concatenation, and dictionary rebuilds;
- TNEF decode buffers: per-attachment buffer allocation in the TNEF unwrap;
- regex in direction detection: compiled or source-generated regex, or a span scan instead.

### Streaming
Streaming input or output stays DEFERRED (`docs/specs/scope.md`). It moves to IN scope only if the measured memory ceiling is exceeded (the load-limits task sets the ceiling, `ydk:nfr:performance/memory-footprint`); an optimization task never introduces it on its own.

### Closing
After the optimization tasks merge, a closing task re-runs the sweep and the profile, refreshes the baseline (ADR-015 refresh rule), re-checks the limits, and records the deltas per optimization in a new profile report.

## Targets (provisional)
PROVISIONAL (#74, 2026-10-18). Source: `docs/perf/profile-2026-10-18.md` (synthetic set only, one run per configuration), owner triage rules on #74, ranking in `docs/perf/triage-2026-10-18.md`. Recorded in `ydk:nfr:performance/memory-footprint` and `ydk:nfr:performance/throughput-dop-sweep`. The closing task (#75) marks each met / not met with numbers.

| Target | Value | Report row | Task |
| --- | --- | --- | --- |
| Large-input M of the 100% pass at the 500 MiB `maxInputBytes` default (reported max of 25/50/100% recorded too) | <= 2.0 (measured 3.33 at the 100% pass, 4.71 max, at 150 MiB) | 4, 8 | T-opt-attach, T-opt-parse |
| Attachment copy factor, `synthetic/attach-1mb` | <= 1.5 (measured 4.07) | 4 | T-opt-attach |
| Bytes allocated per message, synthetic sweep | -30% vs the pre-optimization measurement; A <= 32 stays the regression guard | 3, 7 | T-opt-parse, T-opt-body-dir |
| Detect allocation, `synthetic/small-20kb` | <= 4,000 B/op (measured 66,795) | 5 | T-opt-detect |
| Lock contentions at DOP 22, full-window profile-trace | <= 10 per run (measured 93-99) | 2 | T-opt-encoding |
| Throughput T(1), T(cores) | none until full-window runs (owner rule); reported only, must not trip the `compare` warning | - | all |

## Alternatives
- Optimize the known suspects now. Rejected: unmeasured; the suspects may not be the hotspots.
- One large optimization task. Rejected: no per-change attribution, and a regression could not be pinned to a change.
- Fixed percentage targets set before any profile. Rejected: there was no profile to base them on. The provisional targets above were set after the first profile (#74).

## Consequences
- Requirement: `ydk:req:quality/optimization-evidence`.
- Optimization tasks come from the triage task (#74) as a batch file under the optimization story (`docs/plans/stage-02-opt-batch.yaml`), created with `ydk task create-batch` after the owner approves the triage, not in the initial plan; the closing task gets each new task as a related dependency once they are created.
- The baseline is refreshed once at the end of the loop, not after every optimization.

## Evidence
ADR-011, ADR-012, ADR-015, ADR-019, ADR-020. Project owner decision, 2026-10-18.
