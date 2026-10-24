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
dotnet run -c Release --framework net10.0 --project benches/EmlExtraction.Bench -- large [--max-input-bytes N] [--sample-ms N] [--sweep <sweep.json>] [--out <dir>] [--stream]
```

Defaults: `--max-input-bytes` = `EmlExtractor.DefaultMaxInputBytes` (500 MiB; a full run allocates up to about 500 MiB x M, roughly 2-2.5 GB, and a forced GC per pass. Pass an explicit smaller `--max-input-bytes` for CI or smoke runs), `--sweep` = newest `sweep-results/sweep-*.json` (Workstation GC run, set `mix`, else `public`, else `synthetic`). Smoke run: `-- large --max-input-bytes 8388608`. Exit codes: `0` ok, `1` a pass or the limit+1 guard check failed, `2` bad arguments. Output goes to the console and `sweep-results/large-<stamp>.md`, and ends with the formula filled with T(1), E(n_best), n_best, P(n_best) and M, plus a "how the team reruns this" section:

```
cores_needed   = ceil(R / (T(1) x E(n)))
servers_needed = ceil(R / T(n_best))
ram_per_server = P(n_best) + n_best x max_input_bytes x M
```

Without a measured sweep the T/E/n_best/P fields print `n/a`; run `sweep` first. The provisional values above (50 ms sampling) are placeholders per ADR-015 until the first full sweep.

## Stream input vs bytes (ADR-024, #148)

`large --stream` writes each generated message to a temp file before the pre-run working set is read, then extracts from a `FileStream`, so the whole input is never resident; the default `large` keeps the `byte[]` resident (M includes it). Both modes print `Alloc MB`, the bytes allocated on the extracting thread during `Extract` (the `byte[]` build is outside it). Run 2026-10-24, net10.0 Release, Workstation GC, `--max-input-bytes 104857600` (100 MiB), same machine, back to back:

| Input | Mode | Pre WS MB | Peak WS MB | M | Alloc MB | Seconds | Result |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| 25% | byte[] | 34.9 | 80.3 | 1.82 | 18.3 | 0.19 | ok |
| 25% | stream | 60.8 | 104.6 | 1.75 | 43.9 | 0.25 | ok |
| 50% | byte[] | 80.6 | 142.9 | 1.25 | 36.6 | 0.28 | ok |
| 50% | stream | 166.3 | 221.7 | 1.11 | 87.7 | 0.32 | ok |
| 100% | byte[] | 143.0 | 316.3 | 1.73 | 73.1 | 0.45 | ok |
| 100% | stream | 329.5 | 428.4 | 0.99 | 175.4 | 0.60 | ok |
| limit+1 | byte[] | 279.9 | 380.0 | n/a | 0.0 | 0.00 | throws, no parse |
| limit+1 | stream | 431.4 | 501.2 | n/a | 102.3 | 0.12 | throws after limit + 1 bytes |

Reading: at the limit the stream path's peak growth is 0.99 x input against 1.73 x for a resident `byte[]` (the saving is the whole-input copy). It allocates about 2.4 x more: MimeKit's non-persistent parse buffers each part's encoded content, where the persistent bytes parse reads it as a view over the caller's array. An oversized stream is read (and buffered) up to the limit before it is refused; an oversized `byte[]` is refused on its length. The working set does not shrink fully between passes (see above), so compare M, not the absolute WS columns. Callers reading from disk or the network gain from the stream path; callers already holding the bytes should keep the bytes path.

## Committed baseline and regression rule (ADR-015)

`docs/perf-baseline.json` is a sweep JSON (schema v1, hardware block included), refreshed 2026-10-23 at commit `dc6525e` (task #75, after the ADR-022 optimizations; previous baseline `675b4eb`), measured on the dev machine: Intel Core Ultra 7 155H, 22 logical cores, .NET 10.0.12, Windows, `net10.0`, both GC modes. Command (short windows, so provisional):

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

All values below are **PROVISIONAL**. Data caveat: synthetic set only (no `corpus/internal`), short windows (300 ms warm-up, 1.5 s window, 2 passes), one Windows machine (Intel Core Ultra 7 155H, 22 logical cores, .NET 10.0.12). Re-set them from a full-window run on the public corpus and on target hardware. Sources: the pre-batch `docs/perf-baseline.json` (commit 675b4eb, replaced by the 2026-10-23 refresh) plus local re-runs of the same command (2026-10-18, 3 invocations giving 6 sweep files; 7 sweeps in total with the baseline; the re-runs are not committed).

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

Streaming stays DEFERRED (`docs/specs/scope.md`): measured M (max 4.2 at the time, 1.76 after the optimizations) is below the ceiling (6). Owner-set: the 500 MiB default.

Closing re-check (#75, 2026-10-23, `docs/perf/profile-2026-10-23.md`): no limit changed. After the optimizations M max is 1.76 at the 500 MiB default (100% pass 1.73, peak WS 1,459 MB), A is 19.07 to 19.81 (was 22.5 to 26.3), A spread under 1%. Headroom: M ceiling 6 about 70%, A ceiling 32 about 38%. The 20% threshold stays; Server GC P(22) varied 394 to 683 MB between runs and Workstation T is bimodal (7,425 vs 20,057 files/s at DOP 1), so throughput warnings remain noise. Streaming stays DEFERRED (M 1.76 < 6).

Optimization deltas and ADR-022 target results: `docs/perf/profile-2026-10-23.md`.

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

## Optimization log (ADR-022)

One row per optimization task (metric, before, after, threshold, kept or reverted, commit). The closing re-profile (#75) builds its delta table from these rows. Protocol: `docs/plans/stage-02-opt-batch.yaml` header (same machine, before measured fresh on the base commit, after measured twice, named metric must beat the before by more than the after-run spread).

| Task | Metric | Before | After | Threshold | Result | Commit |
| --- | --- | --- | --- | --- | --- | --- |
| #129 attachments: one exact-size array | `AttachmentsStage.Collect(synthetic/attach-1mb)` B/op, copy factor (both TFMs, BDN dry job, exact) | 4,067,400 B (4.07) | 1,007,816 B (1.01), two after runs identical | copy factor <= 1.5 | kept | PR #137 |
| #129 | M of the 100% pass at 524288000 (net10.0, Workstation, DOP 1) | 2.14, 2.32 (two runs) | 1.38, 1.37 (two runs) | at least 0.5 lower, beyond after-run spread | kept (-0.76 to -0.95) | PR #137 |
| #131 body text direction | BodyStage.Body(synthetic/small-20kb) B/op, net8.0 and net10.0 | 211,664 | 131,544 (two runs, identical; -37.9%) | >= 25% lower | kept | PR #135 |
| #132 detect span-based header check | DetectStage.Detect B/op, small-20kb (net8.0 and net10.0) | 66,795 | 0 | <= 4,000 | kept | this PR |
| #132 | DetectStage.Detect B/op, tiny | 1,986 | 0 | not higher than before | kept | this PR |
| #132 | sweep detect A (net10.0, synthetic) | 2.10 | 0.00 (runs 1 and 2: 0.00, 0.00; spread 0) | < 0.5 and below before beyond the after spread | kept | this PR |
| #130 parse: persistent MimeKit load | `ParseStage.Parse(synthetic/attach-1mb)` B/op (net8.0 and net10.0, BDN dry job, exact) | 1,424,960 | 20,112 (two runs, identical; -98.6%) | <= 150,000 | kept | this PR |
| #130 | Extract bytes allocated per message, full-window synthetic sweep (net10.0, Workstation, DOP 1) | 36,269, 36,371 (two runs) | 31,530, 31,527, 31,572 (three runs; -13.1% to -13.3%) | at least 15% lower, beyond after-run spread | missed by about 2 points (beyond spread); owner decides | this PR |
| #130 | M of the 100% pass at 524288000 (net10.0, Workstation, DOP 1) | 1.40 | 1.73, 1.73 (two runs) | <= 2.0 (provisional) | met; see detail on the rise | this PR |
| #133 encoding cache | `ReaderWriterLockSlim` frames, % of extraction CPU (profile-trace, ws-dop22 / server-dop22) | net8.0 37.0 / 47.9; net10.0 30.3 / 45.2 | 0 / 0 in both TFMs, both after runs | < 2% | kept | this PR |
| #133 | Lock contentions per run, counters CSV (ws-dop22 / server-dop22) | net8.0 138 / 156; net10.0 137 / 155 | run 1: net8.0 241 / 14388, net10.0 261 / 1316; run 2: net8.0 284 / 4707, net10.0 331 / 4124 | none (owner decision 2026-10-19: recorded only) | rose; see detail | this PR |
| #133 | `Monitor.Enter` frames, % of extraction CPU (ws-dop22 / server-dop22) | net8.0 0.0 / 0.0; net10.0 1.2 / 1.3 | run 1: net8.0 0.0 / 0.0, net10.0 0.5 / 5.5; run 2: net8.0 0.0 / 0.0, net10.0 0.7 / 16.8 | none (recorded only) | rose on net10.0; see detail | this PR |
| #133 | T(22) files/s, full-window synthetic sweep (Workstation / Server), net10.0 | 29,169 / 61,382 | 32,228 / 99,332 and 32,898 / 98,856 | none (reported) | Server +61%; Workstation +10 to +13% (bimodal range) | this PR |
| #133 | T(22) files/s (Workstation / Server), net8.0 | 27,097 / 51,839 | 27,646 / 100,362 and 33,833 / 101,239 | none (reported) | Server +94 to +95%; Workstation +2 to +25%, see detail | this PR |
| #133 | P(22) peak working set, MB (Workstation / Server), net10.0 / net8.0 | 93 / 170; 88 / 657 | 114 / 593 and 152 / 454; 125 / 1,213 and 112 / 1,117 | no new `compare` warning | unmet: P(22) warns in all 4 after runs; owner decides | this PR |

#129 detail:
- Per-pass M at 157286400 (25 / 50 / 100%): before 4.77 / 2.65 / 2.81 and 4.75 / 2.59 / 3.34; after 2.82 / 1.81 / 2.17 and 2.81 / 1.81 / 2.15. At 524288000: before 4.26 / 2.50 / 2.14 and 4.26 / 3.00 / 2.32; after 2.78 / 1.73 / 1.38 and 2.78 / 1.73 / 1.37. Reported max of the three: 4.77 -> 2.82 (150 MiB), 4.26 -> 2.78 (500 MiB).
- Sweep A (synthetic set, extract allocated bytes per input byte, both GC modes): net10.0 before 23.86 (workstation) / 21.80 (server), after 22.35 / 22.28 and 22.33 / 22.31; net8.0 before 23.73 / 23.85, after 22.34 / 22.38 and 22.39 / 22.40. Four of five like-for-like comparisons fall about 6%. The exception is the net10.0 server-GC before figure (21.80), which is 2.1 B/B lower than the same commit's workstation figure and than net8.0 server (23.85); the exact per-stage BDN figure is the reliable allocation evidence, and the after server figure (22.3) is consistent with the other three after figures. Reported as an apparent +2% on that one cell, not hidden. `compare` before vs after-run-2: no regression beyond 20% on either TFM. Before vs after-run-1 flagged T(22) -55.8% on net8.0 only; tasks #131 and #132 ran sweeps on the same machine at the time, so throughput figures in this entry are informational (the after-run-2 compare is clean).
- gcdump with roots at the 100% large-pass peak (before, net10.0, 157286400 input): the two largest objects are a `byte[]` of 157,286,424 B (the input buffer, rooted by the caller's local variable) and a `byte[]` of 114,939,840 B (`ExtractedAttachment.BinaryData`, the decoded base64, 75% of the input). There is no second input-sized array and no input-sized MimeKit content copy at the peak; the "two 157,286,424-byte arrays" reading in `docs/perf/profile-2026-10-18.md` was wrong. The M saving here comes from removing the transient `MemoryStream` growth buffers and the `ToArray` copy, so none of it is attributable to T-opt-parse.
- Cost: each attachment part is decoded (or an rfc822 part serialized) twice, first into a counting sink, then into the exact-size array. CPU per attachment roughly doubles for that stage; T was not gated (owner priority 3).

#132 detail: verdicts identical (frozen legacy copy in `DetectorEquivalenceTests`: edge cases, boundary windows, all corpus files plus truncations, 20,000 seeded mutations). `compare` before vs after: no regression beyond 20%, no new Extract warning (Extract A unchanged, about 23.8 B/B). Wall time was not a threshold (tasks #129 and #131 ran benchmarks concurrently on the same machine). The net8.0 sweep was not repeated; the profile run covers both runtimes.

#130 detail:
- Base 5f0fbaa (after #129, before #131/#132 merged); after = this branch's parse commit on the same base. Same machine, nothing else benchmarking. `ParseStage.Parse` B/op, all 5 samples, before -> after (identical on net8.0 and net10.0): attach-1mb 1,424,960 -> 20,112; cid-image 25,576 -> 21,336; headers-heavy 15,488 -> 13,368; small-20kb 46,544 -> 19,272; tiny 15,224 -> 13,104 (-14% on tiny, inside the 10-40% the triage expected). `RequestStage.ExtractBytes` attach-1mb 2,443,464 -> 1,038,616; tiny 24,416 -> 22,296.
- Sweep A (B/B, synthetic, DOP 1): net10.0 workstation 22.27 / 22.33 -> 19.36 / 19.35 / 19.38, server 22.28 / 22.31 -> 19.35 / 19.36 / 19.35; net8.0 workstation 22.45 / 22.46 -> 19.44 / 19.52 / 19.52, server 22.37 / 22.39 -> 19.40 / 19.45 / 19.43. Per message on net8.0 workstation: 36,573 / 36,578 -> 31,664 / 31,803 / 31,789 (-13.0% to -13.4%). The per-message criterion (-15%) is missed: the parse copy was about 4.7 KB of the 36 KB a synthetic message allocates; the rest (MimeKit parser buffers and header objects, body text, metadata) is unchanged.
- Large pass per-pass M (25 / 50 / 100%), net10.0: at 524288000 before 2.78 / 2.21 / 1.40, after 1.75 / 1.73 / 1.73 (two runs identical); at 157286400 before 2.81 / 1.70 / 2.28, after 1.78 / 1.74 / 1.73 (two runs identical). Reported max: 2.81 -> 1.78. The 100% pass at the default rises 1.40 -> 1.73, but the absolute peak working set is unchanged (1451.7 MB before, 1458.4 / 1458.3 MB after); the before figure was lower because its pre-run working set (751.7 MB vs 592 MB) still held pages freed by the 50% pass, which the parse copy then reused. After the change every pass sits at about 1.74, the floor the triage named (resident input 1.0 plus the decoded attachment 0.74), so the before 100% figure (and #129's 1.38) was below that floor and not a like-for-like number.
- `compare` before vs after: no warning on net10.0 for runs 1 and 2; net8.0 runs 1 and 2 warned on workstation T(22) (-33%) and run 2 on T(1). Workstation T at n >= 4 is bimodal on this machine on both commits (plateau about 17-19k or 27-30k files/s: before run 2 on net8.0 sat at 17k, after run 3 on net8.0 at 28-30k), so a third pair was run: before run 1 vs after run 3 is clean on both TFMs; before run 2 vs after run 3 warns once (net10.0 workstation T(22) -25.6%, where after run 3 is at 34k for n = 8 and 21-23k for n >= 12). Server-GC T(22) is flat to +19% in every run. Against docs/perf-baseline.json: no regression beyond 20%.
- Lifetime: the parse is persistent, so part content is a view over the input stream until decoded. `Extract` opens the stream before the parse stage (so the parse stage activity no longer covers the copy made for an offset or non-array input) and disposes it after the result is built; the result holds only strings and byte arrays (`InputLifetimeTests`: overwrite-after-Extract, offset slice, mbox, TNEF fixture, weak-reference collectability, and a test that the parsed content changes when the input is overwritten).

#133 detail:
- Change: `CachingEncodingProvider` (internal) wraps `CodePagesEncodingProvider.Instance` and caches `GetEncoding(int)` and `GetEncoding(string)` results, null included, in `ConcurrentDictionary` (name lookup ignores case, like the wrapped provider; each dictionary stops growing at 1024 entries so attacker-supplied charset names cannot grow it). The fallback overloads are not overridden: the base class clones the cached encoding, so MimeKit's fallback lookups skip the code-pages lock too. `EmlExtractor`'s static constructor registers it. Tests assert the same instance as `CodePagesEncodingProvider` for every code page, every name and every charset in the golden set, null caching, the bound, and `Parallel.For`.
- Base 4c83b0d, same machine, nothing else heavy. Protocol: `profile-trace --window-ms 10000 --warmup-ms 2000 --min-passes 3` once before and twice after (both TFMs); full-window synthetic `sweep` once before and twice after per TFM; zero lock frames is read from the speedscope-converted traces as the share of `EmlExtractor.Extract(EmlExtractionRequest)` inclusive time spent under the frame.
- Owner decision 2026-10-19: the pass criterion is `ReaderWriterLockSlim` < 2% of extraction CPU. Before it was 30 to 48% of extraction time (the issue's 43% of samples); after it is 0.0% in all eight after runs (both TFMs, both configurations, both runs).
- Contentions rose: before 137 to 156 per ws-dop22 / server-dop22 run, after 241 to 14,388. The first measurement run (before the owner decision) saw the same. Cause: MimeKit's own `CharsetUtils.GetCodePage` takes a `Monitor` lock on every charset lookup (`ContentType.CharsetEncoding` through `TextPart.GetText`, and RFC 2047 token decoding in `HeaderMapper.Map`). The old `ReaderWriterLockSlim` serialized threads before they reached that lock; removing it let them reach it concurrently. That lock is inside MimeKit, so no provider-level cache removes it. `Monitor.Enter` frames were 0.5 to 16.8% of extraction CPU on net10.0 after (net10.0 Server worst, and varied between runs: 5.5% and 16.8%), 0 on net8.0. A follow-up could stop calling MimeKit's charset lookup per part (an own cached charset map for `GetText` and header decode); it is out of scope here.
- Throughput (reported, no threshold): Server-GC T(22) rose 61 to 95% on both TFMs and is stable across the two after runs. Workstation-GC T(22) is bimodal on this machine on old and new code (about 17 to 19k or 27 to 30k files/s in earlier tasks; here 27 to 34k, the net10.0 cells slightly above that band), so the Workstation difference is not attributed to this change. T(1) is within +/-7% on net10.0 and rose on net8.0 (Workstation 19,442 before, 24,832 and 22,666 after; Server 25,139 before, 33,135 and 32,781 after, outside the pattern of the other cells and not explained by this change), A fell about 1% (19.28 -> 19.07 B/B).
- `compare` before vs after: no T or A warning, but each of the four runs warns twice on P(22) (peak working set): net10.0 Workstation +23% and +64%, Server +248% and +167%; net8.0 Workstation +42% and +27%, Server +85% and +70% (net8.0 Server 657 MB -> 1,213 and 1,117 MB). P(22) is a warn-metric (ADR-015) and the acceptance line says no new warning, so this is unmet and left for the owner: it follows the throughput gain (more messages in flight per second, so more live garbage between collections), and absolute values stay far below the 500 MiB-input large-file case.
- Contention counter per run (sum of the lock-contention rate column): ws-dop1 is 0 before and after on both TFMs.
