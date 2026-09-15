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

```
files_per_sec_total ≈ extract_per_sec_single_thread × cores_used × 0.7
```

The `0.7` is a contention fudge (GC + allocator) — re-measure under parallel load before buying hardware.

**Worked examples on this M1 Pro (10 cores, 0.7 factor):**

| Mix | 1-thread extract/s | Rough 10-core capacity |
| --- | ---: | ---: |
| Tiny / header-only mail | ~50,000 | ~350,000 files/s |
| 20 KB HTML mail | ~17,000 | ~120,000 files/s |
| Mail + 1 MB attachment (decode to RAM) | ~260 | ~1,800 files/s |

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
