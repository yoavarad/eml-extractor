# ADR-020: Profiling method - per-stage CPU, allocation, and GC hotspots on the load-test corpus

## Status
Accepted (per project owner, 2026-10-18). Target; not implemented (Stage 02). Every numeric parameter below (top-N size, run lengths, sampling rates) is provisional until the first profile, like ADR-015. Amended by ADR-023 (2026-10-18): the stages are no longer guarded; a library bug throws `EmlExtractionException` naming the stage.

## Context
ADR-015 measures how fast the library is (throughput, latency, memory per degree of parallelism) but not where the time and memory go. Without that, any optimization is a guess. The owner decided (2026-10-18) to add dev-time profiling and runtime metrics (ADR-021), then optimize only from data (ADR-022).

Facts that shape the method:
- `docs/perf-results.md` shows allocation, not CPU, as the visible cost on attachment mail: about 3.9 allocated bytes per input byte on the 1 MB attachment message and about 6.8 on the 20 KB HTML message (`ydk:nfr:performance/memory-footprint`).
- Attachment bytes are decoded into `byte[]` and returned in memory (`ydk:entity:extraction/ExtractedAttachment`); a decoded part of 85,000 bytes or more lands on the large object heap (LOH), which is collected only with gen2.
- The library ships for net8.0 and net10.0 (ADR-019); GC and JIT differ between the two runtimes.

## Decision
Per project owner, 2026-10-18.

### Stages profiled
Each stage is measured on its own and as a share of one whole extraction:

| Stage | Covers |
| --- | --- |
| request | the request wrapper: `Extract(EmlExtractionRequest)` and the bytes overload, size guard, stage orchestration (ADR-012) |
| detect | `EmlDetector.Detect` / `IsEml` |
| parse | MimeKit parse of the input buffer, including mbox "From " and BOM handling |
| metadata | header mapping: canonical keys, classic fields, xfields (ADR-014, ADR-016) |
| body | main-content selection and subject/body direction (ADR-003, ADR-006) |
| attachments | attachment collection, TNEF unwrap, container path (ADR-008, ADR-013) |

`request` and `detect` are profiling-only stages. `parse`, `metadata`, `body`, and `attachments` are the extraction stages and share their names with the `eml_extraction.stage` tag of ADR-021 (which keeps only those four values), so a hotspot in one of them maps to one `EmlExtractionException.Stage` value (ADR-023).

### What is measured
- CPU time per stage (inclusive and exclusive), from sampling.
- Allocated bytes per operation and per input byte, per stage.
- GC: gen0, gen1, gen2 collection counts per window, total GC pause time (`GC.GetTotalPauseDuration`, available on net8.0 and net10.0), and % time in GC.
- LOH: bytes allocated on the LOH and LOH size over the window.
- Attachment copy factor: `byte[]` bytes allocated in the attachments stage divided by decoded attachment bytes returned. 1.0 means each decoded byte is allocated once; more means extra copies.
- Lock contention and thread-pool work items (expected near zero; the library holds no shared mutable state).

### Tools
All open source. Each tool's license is recorded in the profile report.

| Tool | Use | License |
| --- | --- | --- |
| BenchmarkDotNet `MemoryDiagnoser` | allocated bytes/op, gen0/1/2 per 1,000 ops, per stage | MIT |
| BenchmarkDotNet `ThreadingDiagnoser` | lock contentions and completed work items per op | MIT |
| BenchmarkDotNet `EventPipeProfiler` | CPU sampling and allocation stacks per benchmark (speedscope / nettrace) | MIT |
| `dotnet-trace` | corpus-level CPU sampling and GC events (`gc-verbose` for allocation ticks, LOH) during a sweep run | MIT |
| `dotnet-counters` | `System.Runtime` counters to CSV: GC counts, time in GC, heap and LOH size, allocation rate | MIT |
| `dotnet-gcdump` | heap snapshot after the large-file pass: what holds memory | MIT |
| PerfView (optional, Windows only) | deeper analysis of the same nettrace files (GC stats, allocation stacks) | MIT |

No commercial profiler is required to reproduce a report.

### Inputs
- The ADR-015 corpus mix: the public-mail corpus (`--corpus <path>`, default `corpus/internal/`, every file, mbox split per message) and the synthetic set (5 bench messages + 1,212 golden inputs), reported per set and as a mix.
- The ADR-015 large-file set (25%, 50%, 100% of `maxInputBytes`), profiled separately so a few huge files do not dominate the mix.
- Per-stage benchmarks use fixed representative messages: the five bench messages plus the largest real samples by header count, attachment count, and TNEF presence, picked once and listed in the report.

### Run rules
- Release build, no debugger, inputs loaded into memory before measuring (as ADR-015).
- Both target frameworks: net8.0 and net10.0. The bench project multi-targets for this (see Consequences). A report that covers only one framework states why.
- Corpus-level traces run the ADR-015 sweep harness at DOP 1 and DOP = `Environment.ProcessorCount`, Workstation GC (the .NET default), and Server GC at DOP = cores.
- Hardware block exactly as ADR-015 (CPU model from the OS, cores, RAM, OS, runtime, TFM, GC mode, date, commit); a run that cannot read the CPU model fails.
- Never optimize without a profile that shows the hotspot (ADR-022).

### Output
- `docs/perf/profile-<YYYY-MM-DD>.md`, committed. It holds: the hardware block per run; the tool list with versions and licenses; per-stage CPU share and allocation share per TFM; GC counts, pause time, LOH bytes, and the attachment copy factor; and a top-N hotspot table (provisional N = 10):

| Rank | Stage | % CPU | Alloc share | Suspected cause | Evidence |
| --- | --- | --- | --- | --- | --- |

  "% CPU" is the hotspot's inclusive share of extraction CPU on the corpus mix; "alloc share" is its share of allocated bytes; "evidence" names the trace and frame or benchmark row.
- Raw traces, counters CSVs, and heap dumps are not committed (size); they stay in a gitignored artifacts folder and the report names the files.
- The report compares against the ADR-015 baseline (`docs/perf-baseline.json`) only when that baseline is comparable (same CPU model, logical cores, runtime major.minor, GC mode).

## Alternatives
- Commercial profilers (dotTrace, ANTS, Visual Studio Enterprise). Rejected: the brief asks for open source, and the team must be able to rerun the profile.
- BenchmarkDotNet only. Rejected: it isolates per-call cost well but cannot show where time goes across the real 12,853-message corpus under parallel load.
- `dotnet-trace` only. Rejected: corpus traces mix all message shapes; per-stage benchmarks give stable, comparable numbers per stage.
- Profile net10.0 only. Rejected as the default: the template may host on net8.0 (ADR-019), and GC behaviour differs by runtime.
- Commit raw traces. Rejected: tens to hundreds of MB per run; the report keeps the conclusions.

## Consequences
- `benches/EmlExtraction.Bench` multi-targets net8.0 and net10.0 for profiling. This amends ADR-019, which kept the bench on net10.0 only; `tools/GoldenKit` stays net10.0 only.
- Per-stage benchmarks need internal access to the stages: the bench gets `InternalsVisibleTo`, as tests and GoldenKit already may (ADR-012).
- Requirement: `ydk:req:quality/profiling-report`. The optimization loop that consumes the report is ADR-022.
- The first report needs the ADR-015 baseline (Stage 02 load-test baseline task) so its numbers are read against shipped code.

## Evidence
`docs/perf-results.md` (allocation rates), ADR-015, ADR-019. Tool licenses: BenchmarkDotNet (github.com/dotnet/BenchmarkDotNet, MIT), dotnet-trace / dotnet-counters / dotnet-gcdump (github.com/dotnet/diagnostics, MIT), PerfView (github.com/microsoft/perfview, MIT). Project owner decision, 2026-10-18.
