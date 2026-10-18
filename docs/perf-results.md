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
