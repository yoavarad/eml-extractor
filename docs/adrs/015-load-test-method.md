# ADR-015: Load-test method - DOP sweep on real mail, measured memory, committed baseline

## Status
Accepted (per project owner, 2026-10-17). Implementation pending, tracked as Stage 02 work.

## Context
The brief's load test asks for the maximum files per second on the available hardware, so the team can derive resource needs. Today's figures (`docs/perf-results.md`):
- are single-thread only;
- use five synthetic messages, not real mail;
- come from an Apple M1 Pro laptop;
- turn into multi-core capacity through a guessed contention factor of 0.7. The sibling OLE extractor measured roughly 0.07 to 0.1 of linear scaling per added core, so the guess may be off by a large factor;
- measure no RAM, although the same report calls memory "usually the real cap".

## Decision
Per project owner, 2026-10-17.

### What is run
- A degree-of-parallelism (DOP) sweep: n = 1, 2, ..., `Environment.ProcessorCount`. Each worker thread takes the next message from a shared in-memory list and calls `Extract`; detection runs too and is reported separately.
- Two input sets, reported separately and as a mix:
  - the real public-mail corpus under the gitignored `corpus/internal/` (12,853 messages at the last harvest; mbox files split per message; files over 40 MB skipped as in the probe);
  - the synthetic set: the five existing bench messages plus the 1,212 golden inputs.
- All inputs are loaded into memory before timing; no disk I/O is timed.
- Per DOP: 2 s warm-up, then a measured window of at least 10 s and at least 3 full passes over the set.
- The sweep runs under Server GC (the default for multi-core service hosts) and records the GC mode. A team running Workstation GC reruns with that setting.

### What is reported per DOP
- Throughput T(n), files per second.
- Per-message latency P50 and P95.
- Peak working set P(n): the process working set sampled every 50 ms, maximum over the window.
- Allocated bytes per input byte A: allocated-bytes delta over the window divided by input bytes processed.
- Scaling efficiency E(n) = T(n) / (n x T(1)). E(n) replaces the 0.7 guess.

### Hardware block
- CPU model string, physical and logical core counts, total RAM, OS description, .NET runtime version, GC mode, date, git commit.
- The CPU model comes from the OS (Windows registry `ProcessorNameString`, Linux `/proc/cpuinfo` `model name`, macOS `machdep.cpu.brand_string`). If it cannot be read, the run fails with a non-zero exit. "Unknown processor" is never written.

### Resource-derivation formula
The team reruns the sweep on its own server class and applies:
- cores_needed = ceil(R / (T(1) x E(n))), with R the target files per second and n the cores per server;
- servers_needed = ceil(R / T(n_best)), with n_best the DOP of the highest measured T(n) on that server;
- ram_per_server = P(n_best) + n_best x max_input_bytes x M, with M the peak working-set growth per input byte from the max-input case (`ydk:nfr:performance/memory-footprint`) and max_input_bytes the configured input limit (`ydk:req:extraction/max-input-size`).

### Baseline and regression rule
- The results of a sweep on the project owner's development machine are committed as `docs/perf-baseline.json`, with the hardware block.
- Each run compares itself with the baseline when CPU model, logical core count, and runtime major.minor all match. Otherwise it prints `baseline not comparable: <field>` and skips the comparison, so a retargeted runtime or new machine never compares against a stale baseline.
- Warn-only rule: a warning is printed when T(1) or T(ProcessorCount) is more than 20% below baseline, or A or P(ProcessorCount) is more than 20% above. It never fails a build.
- A nightly CI job runs the synthetic set only (the public corpus is not in CI) against its own `docs/perf-baseline-ci.json`, with the same warn-only rule.
- The baseline is refreshed, in the same commit, whenever the runtime, MimeKit, or the measuring machine changes.

## Alternatives
- Keep the single-thread bench with the 0.7 factor. Rejected: unmeasured, and OLE's numbers show the factor can be far off.
- BenchmarkDotNet only. Rejected as the primary tool: it measures per-call cost well but not throughput across a DOP sweep on a 12,853-message set. Per-message P50/P95 come from the sweep instead.
- Fail CI on regression. Rejected: shared CI runners are noisy; warn-only avoids flaky builds while still surfacing drift.
- Synthetic input only. Rejected: the brief asks for real hardware sizing, and real mail has larger headers and attachments.

## Consequences
- `ydk:nfr:performance/extract-tiny-throughput` drops the 0.7 formula. New NFRs: `ydk:nfr:performance/throughput-dop-sweep`, `ydk:nfr:performance/memory-footprint`.
- `docs/perf-results.md` keeps the 2026-09-15 single-thread table as history.
- The first sweep needs `corpus/internal/` populated on the measuring machine.

## Evidence
`docs/perf-results.md`, `benches/EmlExtraction.Bench/Program.cs`, `yoavarad/ole-extractor` perf baseline and nightly regression job. Project owner decision, 2026-10-17.
