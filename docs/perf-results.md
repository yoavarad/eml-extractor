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

Defaults: `--max-input-bytes` = `EmlExtractor.DefaultMaxInputBytes` (150 MiB; needs roughly 1 GiB of free RAM), `--sweep` = newest `sweep-results/sweep-*.json` (Workstation GC run, set `mix`, else `public`, else `synthetic`). Smoke run: `-- large --max-input-bytes 8388608`. Exit codes: `0` ok, `1` a pass or the limit+1 guard check failed, `2` bad arguments. Output goes to the console and `sweep-results/large-<stamp>.md`, and ends with the formula filled with T(1), E(n_best), n_best, P(n_best) and M, plus a "how the team reruns this" section:

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
| `maxInputBytes` default | 150 MiB (157,286,400), **unchanged** | Large-file pass (#49): M 1.3-4.2, about 730 MB peak working set at 150 MiB. Under the memory ceiling, so nothing forces a change; one machine and synthetic data are too thin to justify another number. The host can set its own. |
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

Streaming stays DEFERRED (`docs/specs/scope.md`): measured M (max 4.2) is below the ceiling (6). Owner decision to confirm: the 150 MiB default and the A and M ceilings above.
