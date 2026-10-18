# ADR-022: Data-driven optimization process

## Status
Accepted (per project owner, 2026-10-18). Target; not implemented (Stage 02). Performance targets live in the NFR component manifests, not in this ADR; none is set yet and they come from the first profile report (ADR-020).

## Context
ADR-015 gives a measured baseline and ADR-020 a hotspot report. The owner decided (2026-10-18) that optimization follows from those data, not from intuition. Optimizations in a parser library carry real risk: a faster path that changes a decoded byte breaks the golden set, and a wider public surface (for example span-returning attachment data) is a contract change.

## Decision
Per project owner, 2026-10-18.

### Targets
- The source of targets is the NFR component manifests: `ydk:nfr:performance/memory-footprint`, the throughput NFRs under `ydk:nfr:performance/`, and `ydk:nfr:observability/metrics-overhead`. This ADR holds the process only and no target numbers.
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

## Alternatives
- Optimize the known suspects now. Rejected: unmeasured; the suspects may not be the hotspots.
- One large optimization task. Rejected: no per-change attribution, and a regression could not be pinned to a change.
- Fixed percentage targets set today. Rejected: there is no profile yet to base them on.

## Consequences
- Requirement: `ydk:req:quality/optimization-evidence`.
- Optimization tasks are created by the triage task (`ydk task create` under the optimization story), not in the initial plan; the closing task is linked to them as they are created.
- The baseline is refreshed once at the end of the loop, not after every optimization.

## Evidence
ADR-011, ADR-012, ADR-015, ADR-019, ADR-020. Project owner decision, 2026-10-18.
