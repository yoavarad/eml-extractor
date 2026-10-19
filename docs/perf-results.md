# Throughput results

Measured on the machine that built this repo. Numbers are **single-threaded** extract of already-buffered `byte[]` (no disk I/O). Detector is sniff-only; extract parses MIME and copies decoded attachment bytes.

## Hardware

| | |
| --- | --- |
| CPU | Apple M1 Pro |
| Cores (logical) | 10 |
| Arch | arm64 |
| OS | macOS 27.0.0 |
| Runtime | .NET 10.0.12 |
| Config | `Release` |
| Date | 2026-09-15 |

Command: `dotnet run --project benches/EmlExtraction.Bench -c Release`

## Files / second (1 thread)

| Case | Input size | Detect / s | Extract / s | Alloc MB/s |
| --- | ---: | ---: | ---: | ---: |
| tiny (plain headers + 1 char body) | 221 B | 774,668 | 48,663 | 1,127 |
| headers-heavy (~600 char body) | 878 B | 360,520 | 61,352 | 1,738 |
| cid-image (HTML + 1×1 PNG) | 675 B | 404,156 | 36,107 | 1,283 |
| small-20kb HTML alternative | 25,433 B | 23,675 | 16,881 | 2,893 |
| attach-1mb (`blob.bin` 1,000,000 bytes, base64) | 1,351,406 B | 21,814 | 259 | 1,363 |

## How to scale resources

Treat extract/s as the limiter. Detect is 5–15× cheaper on small mail and can share the same core.

> Superseded by ADR-015 (2026-10-17): the former single-thread × cores × 0.7 estimate is withdrawn and no longer used. Sizing uses the measured DOP sweep below: scaling efficiency E(n) and the memory figures defined in `ydk:nfr:performance/throughput-dop-sweep` and `ydk:nfr:performance/memory-footprint`. The table above stays as the single-thread history.

Memory is usually the real cap for attachment-heavy mail: each extract holds the raw EML plus decoded parts. Peak ≈ `input_bytes + decoded_attachment_bytes` per in-flight file.

```
concurrency = ram_budget_bytes / peak_bytes_per_file
needed_cores = target_files_per_sec / extract_per_sec_single_thread
```

If the factory streams attachments to disk instead of `byte[]`, expect extract/s to drop (I/O) but RAM to flatten.

## What this does not include

- Disk read of the EML
- Downstream type-detection / nested extract of Office
- Multi-thread BenchmarkDotNet (`--bdn`) — optional, slower to run, same order of magnitude

Re-run on target servers; M1 Pro ≠ production Xeon/AMD.

## DOP sweep (ADR-015)

Multi-core throughput and memory are measured, not estimated:

```
dotnet run -c Release --project benches/EmlExtraction.Bench -- sweep [options]
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--set synthetic\|public\|mix\|all` | `all` | Input set(s). `synthetic` = the 5 bench messages + every `corpus/golden/**/input.eml`; `public` = every non-empty file under `--corpus` (mbox files split per message); `mix` = both, shuffled with a fixed seed. |
| `--corpus <path>` | `<repo>/corpus/internal` | Public-mail corpus. A task worktree has no `corpus/internal`; pass the main checkout's path. Missing or empty: `SKIPPED: public corpus not found at <path>` is printed and the `public` and `mix` sets are reported with `status: "skipped"`. |
| `--warmup-ms N` | `2000` | Warm-up per DOP (provisional). |
| `--window-ms N` | `10000` | Minimum measured window per DOP (provisional). |
| `--min-passes N` | `3` | Minimum full passes over the set per DOP (provisional). The window ends when both minimums are met. |
| `--sample-ms N` | `50` | Working-set sampling interval (provisional). |
| `--out <dir>` | `<repo>/sweep-results` | Where `sweep-<utc stamp>.json` and `.md` are written (gitignored). |
| `--simulate-cpu-unreadable` | off | Test hook: forces the CPU-model lookup to fail. |

Smoke run (short window): `-- sweep --set synthetic --warmup-ms 100 --window-ms 200 --min-passes 1`.

How it runs: all inputs are loaded into memory first. For n = 1..`Environment.ProcessorCount`, n threads take the next message from a shared index and call `Extract`; detection (`IsEml`) is swept the same way and reported separately. A full blocking GC runs before each DOP. A counts every allocation in the window, so ops in flight at the window edges add a small bias that grows with message size. The sweep runs twice, each in a child process with `DOTNET_gcServer` set explicitly (`0` = Workstation, `1` = Server); the child exits non-zero if `GCSettings.IsServerGC` does not match.

Exit codes: `0` ok, `2` bad arguments, `3` hardware block unreadable (CPU model, physical cores, target framework, or git commit; "Unknown processor" is never written), `4` GC mode mismatch, `5` golden set missing.

### DOP sweep JSON schema (v1)

Consumed by the baseline/regression task. Fields may be added; any rename, removal, or change of meaning bumps `schemaVersion`.

```
{
  "schemaVersion": 1,
  "hardware": {
    "cpuModel": string,           // Windows registry ProcessorNameString, Linux /proc/cpuinfo "model name", macOS machdep.cpu.brand_string
    "physicalCores": int,
    "logicalCores": int,          // Environment.ProcessorCount
    "totalMemoryBytes": long,     // GC.GetGCMemoryInfo().TotalAvailableMemoryBytes: physical RAM, or the container/GC hard limit when one is set
    "os": string, "osArchitecture": string,
    "runtime": string,            // e.g. ".NET 10.0.12"
    "targetFramework": string,    // e.g. "net10.0"
    "date": string,               // UTC, yyyy-MM-ddTHH:mm:ssZ
    "gitCommit": string
  },
  "parameters": { "warmupMs", "windowMs", "minPasses", "sampleIntervalMs", "maxDop": int, "set", "corpus": string },
  "runs": [ {
    "gcMode": "workstation" | "server",
    "sets": [ {
      "name": "synthetic" | "public" | "mix",
      "status": "measured" | "skipped",
      "skipReason": string | null,
      "messages": int, "inputBytes": long,
      "extract": [ point ], "detect": [ point ]     // one point per n = 1..maxDop; empty when skipped
    } ]
  } ]
}

point = {
  "dop": int,                     // n
  "filesPerSec": double,          // T(n)
  "p50Ms": double, "p95Ms": double,          // per-message latency, nearest rank (reservoir sample of up to 131,072 per worker)
  "peakWorkingSetBytes": long,    // P(n): max process working set sampled over the window
  "allocBytesPerInputByte": double,          // A: GC allocated-bytes delta / input bytes completed in the window
  "efficiency": double,           // E(n) = T(n) / (n x T(1))
  "messages": long, "elapsedSec": double, "passes": double
}
```

## Large-file pass and sizing formula (ADR-015)

Single thread, kept out of the DOP mix. Generates (at run time, nothing committed) messages at 25%, 50% and 100% of `maxInputBytes`, each with one base64 attachment, plus one at limit+1 that must throw `EmlInputTooLargeException` (no parse). A forced full GC runs before each input; the working set is sampled every 50 ms; M = (peak WS - pre-run WS) / input bytes, with the pre-run WS read before the input is generated, so M includes the resident input. The reported M is the largest of the three in-limit passes. The working set does not shrink fully after a GC, so later passes start from a higher baseline and their M reads lower: the max is the conservative figure.

```
dotnet run -c Release --framework net10.0 --project benches/EmlExtraction.Bench -- large [--max-input-bytes N] [--sample-ms N] [--sweep <sweep.json>] [--out <dir>]
```

Defaults: `--max-input-bytes` = `EmlExtractor.DefaultMaxInputBytes` (500 MiB; a full run allocates up to about 500 MiB x M, roughly 2-2.5 GB, and a forced GC per pass. Pass an explicit smaller `--max-input-bytes` for CI or smoke runs), `--sweep` = newest `sweep-results/sweep-*.json` (Workstation GC run, set `mix`, else `public`, else `synthetic`). Smoke run: `-- large --max-input-bytes 8388608`. Exit codes: `0` ok, `1` a pass or the limit+1 guard check failed, `2` bad arguments. Output goes to the console and `sweep-results/large-<stamp>.md`, and ends with the formula filled with T(1), E(n_best), n_best, P(n_best) and M, plus a "how the team reruns this" section:

```
cores_needed   = ceil(R / (T(1) x E(n)))
servers_needed = ceil(R / T(n_best))
ram_per_server = P(n_best) + n_best x max_input_bytes x M
```

Without a measured sweep the T/E/n_best/P fields print `n/a`; run `sweep` first. The provisional values above (50 ms sampling) are placeholders per ADR-015 until the first full sweep.

## Committed baseline and regression rule (ADR-015)

`docs/perf-baseline.json` is a sweep JSON (schema v1, hardware block included) measured on the dev machine: Intel Core Ultra 7 155H, 22 logical cores, .NET 10.0.12, Windows, `net10.0`, both GC modes. Command (short windows, so provisional):

```
dotnet run -c Release --framework net10.0 --project benches/EmlExtraction.Bench -- sweep --set all --warmup-ms 300 --window-ms 1500 --min-passes 2 --out <dir>
```

The `public` and `mix` sets are recorded as `skipped`: no `corpus/internal` existed on the machine at baseline time. Refresh the baseline with the full-window command (and the corpus) when it exists.

Compare a run against a baseline:

```
dotnet run -c Release --framework net10.0 --project benches/EmlExtraction.Bench -- compare --baseline docs/perf-baseline.json --current <sweep.json> [--threshold 0.2]
```

- Comparable only when CPU model, logical cores, OS and architecture, runtime major.minor and GC mode all match. Otherwise it prints `baseline not comparable: <field>` (`cpuModel`, `logicalCores`, `osArchitecture`, `os`, `runtime`, `gcMode`) and compares nothing.
- Warn-only: for each GC mode and measured set it warns when extract T(1) or T(ProcessorCount) drops, or A or P(ProcessorCount) rises, by more than 20% (provisional). Exit code is always 0 (2 only for bad arguments or unreadable files).
- Refresh rule: re-run the sweep and overwrite `docs/perf-baseline.json` in the same PR whenever a change intentionally moves these numbers, when the dev hardware or runtime major.minor changes, or after a batch of error-model changes (ADR-023, #104-#106) if they touch the hot path. Do not refresh to silence an unexplained warning.

## Nightly synthetic job

`.github/workflows/perf-nightly.yml` (schedule + `workflow_dispatch`, ubuntu-latest, no secrets) runs the `synthetic` set only with short windows, uploads `sweep-results/` as the `perf-nightly-report` artifact, and compares against `docs/perf-baseline-ci.json` (warn-only, GitHub annotations). That file does not exist until seeded: take the sweep JSON from the first nightly run's artifact, commit it as `docs/perf-baseline-ci.json`. Until then the job prints a notice. Runner CPUs vary between runs, so a `baseline not comparable` result is expected now and then.

## Provisional limits (#51)

All values below are **PROVISIONAL**. Data caveat: synthetic set only (no `corpus/internal`), short windows (300 ms warm-up, 1.5 s window, 2 passes), one Windows machine (Intel Core Ultra 7 155H, 22 logical cores, .NET 10.0.12). Re-set them from a full-window run on the public corpus and on target hardware. Sources: `docs/perf-baseline.json` (commit 675b4eb) plus local re-runs of the same command (2026-10-18, 3 invocations giving 6 sweep files; 7 sweeps in total with the baseline; the re-runs are not committed).

| Limit | Value | Source and headroom |
| --- | --- | --- |
| `maxInputBytes` default | 500 MiB (524,288,000), **OWNER-SET**, not data-derived | Project owner decision (2026-10-18), raised from 150 MiB. Measured M 1.3-4.2 (#49, ran at 150 MiB, about 730 MB peak WS) implies up to about 2-2.5 GB peak WS per extraction at 500 MiB; M at 500 MiB is not measured. The 6 x ceiling is 3 GB per extraction. The host can set its own. |
| Memory ceiling | peak WS growth <= 6 x `maxInputBytes` per extraction | M max 4.2 (#49); about 40% headroom. Replaces the placeholder 10. |
| A ceiling | <= 32 allocated bytes per input byte (synthetic set) | Baseline + re-runs: A 22.5-26.3 (max 26.27, both GC modes); about 20% headroom. Replaces the placeholder 8, which sat below every measurement. A is high here because the set is mostly tiny messages; the 1 MB attachment message allocates about 3.9 per byte (table above). |
| Regression threshold | 20%, **unchanged** | Run-to-run spread (max/min - 1): A 2-10%, P(22) 4-11%, so 20% is about 2x the noise. T(1) and T(22) varied 24-80% between short sweeps (workstation T(1) 5,549-9,110 files/s), so throughput warnings are noise at these windows. Revisit after a quiet full-window run. |
| Warm-up / window / passes | 2000 ms / 10000 ms / 3, **unchanged** | The short windows used for the baseline show the T spread above; longer windows are the remedy, not shorter. |
| Sampling interval | 50 ms, **unchanged** | P(n) spread 4-11% at 50 ms; no data argues for a change. |

Re-run values (not committed as JSON; extract, synthetic set; 6 re-run sweep files, per GC mode, DOP 1 and DOP 22):

| Metric | Workstation GC | Server GC |
| --- | --- | --- |
| A at DOP 1 | 22.5-24.7 | 23.9-24.9 |
| A at DOP 22 | 24.3-26.3 | 23.7-24.3 |
| P(22) MB | 103-114 | 181-189 |
| T(1) files/s | 5,721-9,110 | 6,714-10,681 |
| T(22) files/s | 10,007-15,578 | 34,044-42,072 |

The committed baseline alone has A 23.3-24.5 (workstation) and 23.7-24.1 (server); the A ceiling of 32 is 30% above that and 22% above the re-run max. M and the 730 MB peak come from the #49 large-file pass (PR #93, `sweep-results/large-*.md` of that run, not committed; rerun with `-- large`). The ceilings are per extraction: at DOP n on near-limit inputs expect up to n times that growth, so use the sizing formula above (`ram_per_server`), not the ceiling alone. A <= 32 is set on a tiny-message mix and would not catch a regression on large messages; the large-file pass (M) covers those.

Streaming stays DEFERRED (`docs/specs/scope.md`): measured M (max 4.2) is below the ceiling (6). Owner-set: the 500 MiB default. Still to confirm: the A and M ceilings above.

## Telemetry overhead (#73)

Command: `dotnet run -c Release --project benches/EmlExtraction.Bench -f net10.0 -- overhead --filter '*'` (BenchmarkDotNet 0.15.8 runs both the net8.0 and net10.0 jobs from the one command, `MemoryDiagnoser`, default `SimpleJob` auto-tuned iterations; ADR-021). Run twice (2026-10-18), synthetic bench messages only (no `corpus/internal`). Machine: Intel Core Ultra 7 155H, 1 CPU, 22 logical / 16 physical cores, Windows 11 25H2, .NET SDK 10.0.401, runtimes .NET 10.0.12 and .NET 8.0.30, X64 RyuJIT x86-64-v3, Release. A laptop with other processes running, so run-to-run noise is large: the same configuration moved by up to about 16 points between the two runs (small-20kb Activity net10.0 +8.6% then -7.3%). Overhead % = (mean with listener - mean no listener) / mean no listener, per run; shown as run 1 / run 2.

Extract time overhead, listener vs none:

| Case | TFM | MeterListener (all instruments) | ActivityListener (AllData, with stage children) |
| --- | --- | ---: | ---: |
| tiny (221 B) | net10.0 | -5.1% / -6.9% | +7.8% / +0.8% |
| tiny (221 B) | net8.0 | -1.2% / +0.5% | +9.2% / +9.7% |
| small-20kb | net10.0 | -5.5% / -4.0% | +8.6% / -7.3% |
| small-20kb | net8.0 | -2.7% / -0.2% | +0.4% / +3.5% |
| attach-1mb | net10.0 | -16.0% / -2.2% | -9.0% / -5.3% |
| attach-1mb | net8.0 | -10.8% / -4.4% | -10.0% / -2.3% |
| Detect tiny | net10.0 | -2.2% / -10.0% | -4.7% / -7.5% |
| Detect tiny | net8.0 | -3.4% / -7.1% | -1.7% / -8.9% |

Negative values are noise (a listener cannot make the call faster). Allocation per op (MemoryDiagnoser, identical in both runs):

| Case | TFM | None | MeterListener | ActivityListener |
| --- | --- | ---: | ---: | ---: |
| Extract tiny | net10.0 | 23.74 KB | +0.00 KB | +2.24 KB (+9.4%) |
| Extract tiny | net8.0 | 23.83 KB | +0.01 KB | +2.25 KB |
| Extract small-20kb | net10.0 / net8.0 | 258.17 / 258.32 KB | +0.00 / +0.01 KB | +2.24 KB |
| Extract attach-1mb | net10.0 / net8.0 | 5375.16 / 5375.28 KB | -0.07 / -0.05 KB (noise) | +2.2 KB |
| Detect tiny | both | 1.94 KB | +0.00 KB | +0.00 KB |

Reading:
- MeterListener on every instrument: no measurable added time or allocation (all deltas inside the noise band, mean about -5%; the instruments record into pre-sized storage and the callbacks here are empty). Budget 5% on tiny holds.
- ActivityListener sampling AllData with the four stage children: +2.2 KB per Extract (one parent plus four child activities and tags) and +0.8 to +9.7% on the tiny message (tiny net8.0 +9.2% / +9.7% in both runs, the only case near the limit), within noise for the larger cases. Under the ADR-021 limit of 10%, so the stage children are kept; headroom is thin on tiny/net8.0.
- Zero listener: `TelemetryNoListenerAllocationTests` asserts 0 allocated bytes across 1000 iterations of every Extract and Detect telemetry hook (a normal unit test, runs in milliseconds in CI). On net8.0 it only runs when no earlier test in the process attached a `MeterListener`: the net8.0 BCL leaves `Instrument.Enabled` true after a listener is disposed (verified with a stand-alone probe; fixed in later runtimes) and the hooks then allocate about 240 B per Extract. So in the full net8.0 suite it skips and runs when filtered alone; net10.0 always runs it. The BDN `None` runs are unaffected (one process per benchmark, no listener ever attached). `Detect` itself allocates 1.94 KB/op without any telemetry (header sniffing), unchanged by listeners. No uninstrumented build exists to A/B the no-listener time against, so "time within noise" rests on the hooks being `Enabled` / null checks plus the 0-byte result, not a measured delta.
- Not done: the #48 sweep T(1) with and without a listener. The sweep has no listener switch and its T(1) varies 24-80% between short windows (above), which hides a difference of this size; the per-call BDN numbers above are the evidence.

Budget (**confirmed by the owner, 2026-10-18**; set in `ydk:nfr:observability/metrics-overhead` and ADR-021): no listener 0 added bytes per call (tested); MeterListener <= 5% on tiny (measured <= +0.5%, noise +-7%); ActivityListener with stage children <= 10% on tiny (measured +0.8 to +9.7%, headroom only about 0.3 points on net8.0, so a noisier run could cross 10%; owner kept the 10% limit and the child spans), +2.3 KB per Extract.

## Optimization log

One row per optimization task (ADR-022). Measured on the dev machine (Windows 11), dry profile job, bytes are exact.

| Task | Metric | Before | After | Threshold | Kept or reverted | Commit |
| --- | --- | ---: | ---: | --- | --- | --- |
| #131 body text direction | BodyStage.Body(synthetic/small-20kb) B/op, net8.0 and net10.0 | 211,664 | 131,544 (two runs, identical; -37.9%) | >= 25% lower | kept | TBD |
