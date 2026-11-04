# ADR-015: Load-test method - DOP sweep on real mail, measured memory, committed baseline

## Status
Implemented (accepted per project owner, 2026-10-17). Per project owner (2026-10-18), the memory ceiling, the A ceiling, the regression threshold and the sampling interval were checked against the first measurements in task #51 (synthetic set only, one Windows machine, 22 cores). They stay PROVISIONAL; values and sources are in `docs/perf-results.md`, section "Provisional limits (#51)". Run settings amended by the project owner on 2026-11-04 (see "What is run"). Streaming stays DEFERRED: M 1.75 at the 500 MiB default (large-file pass, 2026-11-04) is under the ceiling of 6.

## Context
The brief's load test asks for the maximum files per second on the available hardware, so the team can derive resource needs. Today's figures (`docs/perf-results.md`):
- are single-thread only;
- use five synthetic messages, not real mail;
- come from an Apple M1 Pro laptop;
- turn into multi-core capacity through a guessed contention factor of 0.7, which nobody measured;
- measure no RAM, although the same report calls memory "usually the real cap".

Context only, not an input to any decision here: the sibling OLE extractor measured 19.8 items/s serial versus 43.5 items/s at MaxDOP 22 on a 22-core machine, a 2.2x speedup, and 1.6x on a second run (`yoavarad/ole-extractor` `docs/adrs/007-batch-extraction-api.md`, lines 64-76). That is a different workload, but it shows multi-core scaling can sit far below a fixed 0.7-per-core assumption, so the factor has to be measured.

## Decision
Per project owner, 2026-10-17.

### What is run
- A degree-of-parallelism (DOP) sweep: n = 1, 2, ..., `Environment.ProcessorCount`. Each worker thread takes the next message from a shared in-memory list and calls `Extract`; detection runs too and is reported separately.
- Two input sets, reported separately and as a mix:
  - the real public-mail corpus under the gitignored `corpus/internal/` (12,853 messages at the last harvest; mbox files split per message; every file is included, with no size cut-off);
  - the synthetic set: the five existing bench messages plus the 1,212 golden inputs.
- A separate large-file pass on one thread: generated messages at 25%, 50%, and 100% of the configured input limit (`ydk:req:extraction/max-input-size`), each holding one base64 attachment, plus one message one byte over the limit to confirm the guard. This pass feeds the memory ceiling (`ydk:nfr:performance/memory-footprint`) and is kept out of the DOP mix so a few huge files do not dominate throughput figures.
- All inputs are loaded into memory before timing; no disk I/O is timed.
- Per DOP: 300 ms warm-up, then a measured window of at least 1.5 s and at least 2 full passes over the set. Amended by the project owner, 2026-11-04: the short settings are kept for both the development baseline and the nightly CI job, replacing the earlier 2 s / 10 s / 3 passes. Reason: the nightly job stays at about one minute (run 41818826405 took 52 s), and the extra run-to-run noise is accepted because the regression rule is warn-only with a 20% threshold. T(1) and T(ProcessorCount) vary a lot between short runs (24-80% in #51), so a throughput warning is a prompt to re-run, not a verdict; longer windows (`--warmup-ms`, `--window-ms`, `--min-passes`) remain available for one-off runs.

### Garbage collector
- Workstation GC is the .NET default for console and library hosts; Server GC must be opted into. The harness sets `ServerGarbageCollection` explicitly for each run, never relying on the default.
- The sweep runs twice, once with Workstation GC and once with Server GC, and reports both. The factory's host GC mode is unknown (`docs/team-questions.md`), and the two modes trade memory for throughput differently, so the team needs both curves.

### What is reported per DOP and GC mode
- Throughput T(n), files per second.
- Per-message latency P50 and P95.
- Peak working set P(n): the process working set sampled at a fixed interval (PROVISIONAL, kept in #51: 50 ms), maximum over the window.
- Allocated bytes per input byte A: allocated-bytes delta over the window divided by input bytes processed.
- Scaling efficiency E(n) = T(n) / (n x T(1)). E(n) replaces the 0.7 guess.

### Hardware block
- CPU model string, physical and logical core counts, total RAM, OS description, .NET runtime version and target framework, GC mode, date, git commit.
- The CPU model comes from the OS (Windows registry `ProcessorNameString`, Linux `/proc/cpuinfo` `model name`, macOS `machdep.cpu.brand_string`). If it cannot be read, the run fails with a non-zero exit. "Unknown processor" is never written.

### Resource-derivation formula
The team reruns the sweep on its own server class, in its GC mode, and applies:
- cores_needed = ceil(R / (T(1) x E(n))), with R the target files per second and n the cores per server;
- servers_needed = ceil(R / T(n_best)), with n_best the DOP of the highest measured T(n) on that server;
- ram_per_server = P(n_best) + n_best x max_input_bytes x M, with M the peak working-set growth per input byte from the large-file pass and max_input_bytes the configured input limit.

### Baseline and regression rule
- The results of a sweep on the project owner's development machine, over all three sets (synthetic, public, mix) and both GC modes, are committed as `docs/perf-baseline.json`, with the hardware block.
- Each run compares itself with the baseline when CPU model, logical core count, runtime major.minor, and GC mode all match. Otherwise it prints `baseline not comparable: <field>` and skips the comparison, so a retargeted runtime or new machine never compares against a stale baseline.
- Warn-only rule (threshold 20%, PROVISIONAL, kept in #51: A and P run-to-run spread is 2-11%, T spread 24-80% at short windows): a warning is printed when T(1) or T(ProcessorCount) drops, or A or P(ProcessorCount) rises, by more than the threshold. It never fails a build.
- A nightly CI job runs the synthetic set only (the public corpus is not in CI) against its own `docs/perf-baseline-ci.json`, with the same warn-only rule. That file is the sweep JSON of a nightly run on the GitHub ubuntu runner (run 41818826405, 2026-11-04).
- The baseline is refreshed, in the same commit, whenever the runtime, the parser library version, or the measuring machine changes.

## Alternatives
- Keep the single-thread bench with the 0.7 factor. Rejected: unmeasured.
- BenchmarkDotNet only. Rejected as the primary tool: it measures per-call cost well but not throughput across a DOP sweep on a 12,853-message set. Per-message P50/P95 come from the sweep instead.
- Fail CI on regression. Rejected: shared CI runners are noisy; warn-only avoids flaky builds while still surfacing drift.
- Synthetic input only. Rejected: the brief asks for real hardware sizing, and real mail has larger headers and attachments.
- One GC mode only. Rejected: the factory's host GC mode is not known.
- Mix the maximum-size messages into the DOP set. Rejected: a handful of very large files would dominate throughput; they get their own pass.

## Consequences
- `ydk:nfr:performance/extract-tiny-throughput` drops the 0.7 formula. New NFRs: `ydk:nfr:performance/throughput-dop-sweep`, `ydk:nfr:performance/memory-footprint`.
- `docs/perf-results.md` keeps the 2026-09-15 single-thread table as history.
- The first sweep needs `corpus/internal/` populated on the measuring machine.
- With multi-targeting (ADR-019) the sweep reports the target framework; the baseline is kept for net10.0.

## Evidence
`docs/perf-results.md`, `benches/EmlExtraction.Bench/Program.cs`, `yoavarad/ole-extractor` `docs/adrs/007-batch-extraction-api.md` (lines 64-76). Project owner decisions, 2026-10-17 and 2026-10-18.
