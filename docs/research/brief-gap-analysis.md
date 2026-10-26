# Brief gap analysis - EML extraction vs the team brief

Date: 2026-10-17. Hand-off document for the team. It maps every item of the team brief (Hebrew original, translated in `docs/requirements.md`) to its status in this repo, the gap found, and the decision or plan. It also records what was learned from the sibling repo `yoavarad/ole-extractor`, built against the same brief for OLE files.

Status legend: **Met** (in code, tested) / **Partial** (in code, gaps listed) / **Planned** (decided, target for Stage 02) / **Open** (needs the team).

## Brief item to status

| # | Brief item | Status | Gap | Decision / plan |
| --- | --- | --- | --- | --- |
| 1 | Detection function: bytes in, is it EML | Met; typed result Planned | Returns only `bool`; OLE returns a typed result | Keep `IsEml` (bool, matches the brief); add `Detect` returning `EmlDetectionResult { IsSupported, MimeType }` (ADR-012) |
| 2 | Extract: classic header metadata | Met; cleanup Planned | Duplicate keys (`reply_to` + `replyto`, `message_id` + `messageid`); `from`/`to`/`cc`/`bcc` missing on `parse_failed` | One canonical key per header, overlay rewrites in place, address keys always present (ADR-016) |
| 3 | Extract: as many headers as possible | Met; keying Planned | `-` folded to `_`, so `X-Foo` and `X_Foo` collide on `x_foo` | Key = lower-cased name only; `-` and `_` kept (ADR-016) |
| 4 | Extract: all `X_` headers concatenated into field `xfields` | Partial | Matches `X-` or `X_` case-insensitively; exposed as a separate `XFields` property, not a metadata field | Literal `X_` (case-sensitive) only; `Metadata["xfields"]`; `Name: value` lines joined by `\n`; `X-` headers stay in general metadata (ADR-014). Team to confirm internal mail uses `X_` (`docs/team-questions.md`) |
| 5 | Extract: main content | Met | None | HTML preferred (ADR-003); direction reported (ADR-006) |
| 6 | Sub-files: name, size, binary | Met; renames Planned | Names differ from OLE (`Name`/`Size`/`Data` vs `FileName`/`SizeInBytes`/`BinaryData`) | Shared names `FileName`, `SizeInBytes`, `BinaryData` (ADR-012) |
| 7 | Sub-files: full path if it exists | Partial | Set only when the filename contains `/` or `\`; `Content-Location` ignored; no position in the message | `FullPath` = container path, always set, unique per result (`winmail.dat/budget.xlsx`); raw path kept as `OriginalPath`; `ContentLocation` added (ADR-013) |
| 8 | Sub-files: no deeper extraction | Met; one inconsistency resolved | TNEF unpacked but nested `.eml` opaque (ADR-008 open question) | Keep nested messages opaque; TNEF is a transport wrapper, a nested message is a mail file the factory extracts itself (ADR-013) |
| 9 | Load test: max files/sec on available hardware, to derive resources | Partial | Single-thread only; synthetic mail; M1 laptop; 0.7 multi-core factor guessed, never measured (context: OLE saw 19.8 items/s serial vs 43.5 at 22 cores, 2.2x, ole-extractor ADR-007 lines 64-76); no RAM measured | DOP sweep 1..cores on real public mail + synthetic, P50/P95, peak working set, alloc bytes per input byte, measured scaling efficiency, Workstation and Server GC both reported, large-file pass near the input limit, hardware block (no "Unknown processor"), sizing formula, committed baseline with warn-only regression rule; all thresholds provisional until the first sweep (ADR-015). Then load test -> sizing -> profiling: per-stage CPU, allocation, GC and LOH profile with a committed hotspot report (ADR-020), optimization only from that report with before/after per change (ADR-022), and a closing re-run that refreshes the baseline |
| 10 | Correctness A: emoji, exotic languages | Partial | One ZWJ emoji row; no skin-tone, flag, keycap, or combining-mark cases | Exact-equality cases for each form and encoding (`ydk:req:content/unicode-fidelity`) |
| 11 | Correctness B: Office, other emails, attached and pasted images | Partial | CI uses typed stubs; real Office/`.msg`/TNEF samples only in gitignored `corpus/internal/`; those tests pass trivially when the files are absent | Committed realistic samples with provenance and license per sample, run in CI; absent-sample tests report Skipped, never Passed (ADR-018) |
| 12 | Document edge cases for internal continuation | Met | None for documentation; real-sample gaps listed | `docs/edge-cases.md`, DEFERRED list in `docs/specs/scope.md` |
| 13 | All open source | Met | Was: no `LICENSE`, no dependency license audit, transitive `BouncyCastle.Cryptography` undocumented | MIT (owner-accepted 2026-10-18) with `THIRD-PARTY-NOTICES.md` and a CI audit; all runtime deps are MIT (ADR-017) |
| 14 | C# / .NET 10 | Met; net8.0 Planned | Template may still run on .NET 8 LTS (support ends 2026-11-10); net10.0-only library cannot load there | Multi-target net8.0 + net10.0 for library and tests, CI on both; tools stay net10.0; drop net8.0 once the template is on net10 (ADR-019) |
| 15 | If big capability gap: assess improving C# libs vs another language | Planned | No assessment written (ADR-001 alternatives "not recorded") | `docs/research/csharp-capability-assessment.md`, Stage 02 task (`ydk:req:research/capability-assessment`) |
| 16 | Component goes into the team's template; team does factory integration | Open | Neither repo specifies the template integration | Shared contract proposed (ADR-012); integration questions in `docs/team-questions.md` |

## Other gaps found (not brief items)

| Gap | Decision / plan |
| --- | --- |
| Public `Extract(MimeMessage)` leaks MimeKit types into the API | Made internal; `Extract(EmlExtractionRequest)` primary, `Extract(ReadOnlyMemory<byte>)` kept as a thin wrapper (ADR-012) |
| No input-size guard; every attachment held in memory | configurable `maxInputBytes` (provisional default 150 MiB, matching Exchange Online's documented 150 MB message limit; final value set from measurement); oversized input throws `EmlInputTooLargeException` (ADR-023; #38 shipped it as an `input_too_large` warning); memory ceiling measured (`ydk:req:extraction/max-input-size`, `ydk:nfr:performance/memory-footprint`). Streaming DEFERRED unless measurement demands it |
| Direction values stored as metadata keys | Removed from metadata; result properties only (ADR-016) |
| No runtime visibility: the host cannot see durations, warning rates, size-guard refusals, or per-stage library bugs without wrapping every call | BCL `Meter` + `ActivitySource` named `EmlExtraction`, no OpenTelemetry dependency, no message content in tags, provisional overhead budget (ADR-021, `ydk:req:observability/runtime-telemetry`, `ydk:nfr:observability/metrics-overhead`); naming to align with the team (`docs/team-questions.md` question 6) |
| Unexpected exceptions can still escape `Extract` | Decided in ADR-004 (`internal_error`, done in #37); superseded by ADR-023: they throw `EmlExtractionException` naming the stage; Stage 02 |

## Breaking API change summary (Stage 02)

All of the following change at once, while the library has no consumers: `Extract(EmlExtractionRequest)` becomes the primary method, the bytes overload becomes a thin wrapper over it, and the `MimeMessage` overload becomes internal; `EmlDetector.Detect` added; attachment fields renamed and `FullPath` redefined; `OriginalPath` and `ContentLocation` added; `XFields` property removed in favour of `Metadata["xfields"]`; metadata keys change to the canonical form. Golden expectations are regenerated from the catalog, never re-recorded from extractor output (ADR-007).

## OLE lessons

What the sibling extractor does, and whether this repo adopts it.

### Adopt

| OLE practice | Adopted as |
| --- | --- |
| Request classes so fields can be added later | `EmlExtractionRequest { FileBytes, InputStream, FileName }` (ADR-012, ADR-024) |
| Typed detection result | `EmlDetectionResult { IsSupported, MimeType }`; `IsEml` kept (ADR-012) |
| Sub-file record `{ BinaryData, FileName, PackagePath, SizeInBytes }` with location inside the container | Shared names `FileName`, `SizeInBytes`, `FullPath`, `BinaryData`; `FullPath` = container path (ADR-012, ADR-013) |
| BenchmarkDotNet P50/P95 and alloc-bytes-per-input-byte | P50/P95 and alloc bytes per input byte reported per DOP in the sweep (ADR-015) |
| Committed perf baseline, warn-only 20% regression rule, nightly | Same rule; nightly synthetic run against a CI baseline (ADR-015) |
| ZWJ / skin-tone / flag / combining emoji fixtures with exact-equality asserts | `ydk:req:content/unicode-fidelity` |
| Expected values recorded from observed runs, never from prose | Expected values from generator inputs or published contents; never from prose and never from this extractor (keeps ADR-007) (ADR-018) |
| Provenance / license field on every sample | `corpus/realistic/manifest.json` entry per sample (ADR-018) |

### Avoid

| OLE weakness | How this repo avoids it |
| --- | --- |
| Spec names differ from DTO names | Manifests use the exact target DTO names; project rule: spec and DTO names must match |
| "Unknown processor" in hardware reports | Sweep fails if the CPU model cannot be read (ADR-015) |
| Stale baselines after a runtime retarget | Baseline compared only when CPU, core count, and runtime major.minor match; refreshed in the same commit as a runtime/MimeKit/machine change (ADR-015) |
| No template integration specified | Raised as an explicit team question; shared contract proposed (ADR-012) |

### Not adopted

| OLE practice | Why not |
| --- | --- |
| Typed exceptions | Was: EML keeps no-throw + warnings (ADR-004). Now (ADR-023, owner 2026-10-18): EML throws typed exceptions for misuse, oversized input and bugs; content problems stay warnings |
| Batch API `ExtractManyAsync(maxDOP)` | Rejected by the owner (2026-10-18, ADR-012): the factory owns parallelism (YAGNI); each call is independent and stateless, and the factory catches per item (EML throws only for misuse, oversized input and bugs, ADR-023) |
| `InputStream` / `FilePath` request sources | DEFERRED; the request record leaves room for them |
| `DetectedFormat` enum | EML handles one format |
