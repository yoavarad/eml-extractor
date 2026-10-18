# ADR-021: Runtime metrics and tracing with BCL Meter and ActivitySource

## Status
Accepted (per project owner, 2026-10-18). Implemented (metrics #71, activities #72). The overhead budget is provisional until measured. Instrument names may be renamed once if the team answers `docs/team-questions.md` question 6 before the first release that ships them. Amended by ADR-023 (2026-10-18): `internal_errors` and `input_too_large` count thrown exceptions, and those two codes leave the warning-code tag set.

## Context
Today the warnings list on the result is the only diagnostic channel; the library writes no logs, traces, or metrics (`ydk:crosscut:core/warning-format`). The factory sees each result, but it cannot see, without wrapping every call itself, how long extraction takes per message size, how often each warning kind fires across a fleet, how often the size guard refuses input, or which stage hit an `internal_error`. The owner decided (2026-10-18) to add runtime metrics in the library, next to dev-time profiling (ADR-020).

Constraints:
- The library is loaded into the caller's process (`ydk:crosscut:core/library-surface`). It must not force a telemetry stack on the host.
- Mail content is personal data. Nothing from a message may leave through telemetry.
- Throughput matters: a tiny message extracts in about 20 microseconds and detection in about 1.3 microseconds (`docs/perf-results.md`), so per-call overhead must be negligible when nobody listens.
- The library ships for net8.0 and net10.0 (ADR-019). `System.Diagnostics.Metrics` and `System.Diagnostics.ActivitySource` are in the shared framework on both, so no package is needed.

## Decision
Per project owner, 2026-10-18.

### Mechanism
- Only BCL APIs: one `System.Diagnostics.Metrics.Meter` and one `System.Diagnostics.ActivitySource`. No OpenTelemetry package, no `ILogger`, no `EventSource` in the library. The consumer wires OpenTelemetry (or `dotnet-counters`, or a `MeterListener`) by subscribing to the names below.
- Both are `static readonly` fields of one internal class, created once per process. `Meter`, its instruments, and `ActivitySource` are thread-safe; `EmlExtractor` and `EmlDetector` stay free of per-instance mutable state.
- No `IMeterFactory` and no DI: the library has no DI container, and the public constructor stays as ADR-012 defines it.

### Names and versioning
- Meter name and ActivitySource name: `EmlExtraction` (the assembly name).
- Version passed to both: the library's assembly informational version (SemVer).
- Instrument names, units, tag keys, tag values, and activity names are public contract (`ydk:req:observability/runtime-telemetry`). Adding an instrument or a tag value is a minor change. Renaming or removing one, or changing a unit, is breaking: it needs an ADR and a major version.
- Naming follows the OpenTelemetry semantic-convention style where it applies: lower-case, dot-separated namespaces, snake_case words, UCUM units (`s`, `By`, `{attachment}`), no `.total` suffix on counters, and the generic `error.type` key where an error is described.

### Instruments

| Name | Kind | Unit | Tags | Recorded |
| --- | --- | --- | --- | --- |
| `eml_extraction.extract.duration` | Histogram<double> | `s` | `eml_extraction.outcome` | once per Extract call, wall time of the whole call |
| `eml_extraction.extract.input.size` | Histogram<long> | `By` | - | once per Extract call, input length in bytes |
| `eml_extraction.extract.attachments` | Histogram<int> | `{attachment}` | - | once per Extract call that parsed, attachment count |
| `eml_extraction.extract.warnings` | Counter<long> | `{warning}` | `eml_extraction.warning.code` | once per warning added to a result |
| `eml_extraction.detect.calls` | Counter<long> | `{call}` | `eml_extraction.detect.result` | once per Detect or IsEml call |
| `eml_extraction.extract.internal_errors` | Counter<long> | `{error}` | `eml_extraction.stage`, `error.type` | once per Extract call that threw `EmlExtractionException` (ADR-023) |
| `eml_extraction.extract.input_too_large` | Counter<long> | `{request}` | - | once per Extract call refused by the size guard |

Tag values are closed sets:
- `eml_extraction.outcome`: `clean` (no warnings), `warnings` (at least one warning, content returned), `parse_failed`, `input_too_large`, `internal_error` (the last two: the call threw, ADR-023).
- `eml_extraction.warning.code`: the warning codes of `ydk:crosscut:core/warning-format` (`parse_failed`, `body_decode_failed`, `part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed`).
- `eml_extraction.detect.result`: `supported`, `not_supported`.
- `eml_extraction.stage`: `parse`, `metadata`, `body`, `attachments` (the ADR-004 guarded stages; ADR-020 profiles these four plus the profiling-only `request` and `detect`).
- `error.type`: the .NET exception type's full name (bounded by the code, not by input).

The `internal_errors` and `input_too_large` counters are separate from the warnings counter (both cases throw and record no warning): they are the two signals an operator alerts on, and the internal-error counter carries the stage.

No histogram bucket advice is set: the API for it is not in the net8.0 BCL. Consumers configure buckets with views.

### Activities (tracing)
- One activity per Extract call, named `eml_extraction.extract`, kind Internal.
- One child activity per guarded stage, named `eml_extraction.parse`, `eml_extraction.metadata`, `eml_extraction.body`, `eml_extraction.attachments`. Child activities are created only when a listener samples the parent; if the overhead benchmark shows they break the budget with a listener attached, they are dropped and only the parent activity stays.
- No activity for Detect: it is too cheap for a span to be useful; the detect counter covers it.
- Tags on `eml_extraction.extract`: `eml_extraction.input.size` (bytes), `eml_extraction.attachments.count`, `eml_extraction.warnings.count`, `eml_extraction.outcome`. On `internal_error` the stage activity gets status Error and `error.type`; the exception message is not recorded.

### PII and cardinality (hard rule)
- No tag, activity name, event, baggage, or status description may carry message content or anything derived from it: no subject, body, header value, address, display name, message ID, attachment file name, content ID, path, content type, or warning detail text.
- The request's `FileName` is caller data and is never recorded either.
- Exception messages are never recorded (MimeKit messages can quote input).
- Every tag value comes from a closed set fixed in code or is a number. A test asserts this on every instrument and activity (`ydk:test:observability/telemetry.unit`).

### Overhead budget (provisional)
`ydk:nfr:observability/metrics-overhead`. Placeholders until measured:
- No listener: added time per Extract within the run-to-run noise of the tiny-message benchmark and at most 1%, and zero added allocated bytes per call.
- MeterListener on every instrument (and ActivityListener sampling all, measured separately): at most 5% added time on the tiny message.
- Verified by a BenchmarkDotNet comparison of no listener versus listener attached, on both target frameworks. The measuring task replaces the placeholders with measured values plus headroom.

## Alternatives
- `EventSource` / EventCounters. Rejected: the older mechanism; `Meter` is what OpenTelemetry .NET and `dotnet-counters` consume natively, and it supports histograms and tags.
- `ILogger` (Microsoft.Extensions.Logging.Abstractions). Rejected: adds a package dependency and a logger parameter to the public constructor, produces logs rather than metrics, and log messages invite content leaks.
- OpenTelemetry API package in the library. Rejected: forces a dependency and version on every host; the BCL types are the OpenTelemetry .NET API.
- No telemetry (status quo). Rejected by the owner: per-stage failures, warning rates, and size-guard refusals stay invisible unless every host re-derives them.
- `IMeterFactory` with DI. Rejected: the library has no DI; a static meter is the documented pattern for libraries without it.

## Consequences
- `ydk:crosscut:core/library-surface` and `ydk:crosscut:core/warning-format` change from "no logging, tracing, or metrics" to "warnings plus BCL metrics and activities (target)". Cross-cutting rules: `ydk:crosscut:core/observability`.
- Telemetry tests share one static meter across the test process: they filter by meter name and run in one non-parallel test collection, so measurements from other tests do not leak in.
- The README documents the meter and source names and how to subscribe with OpenTelemetry.
- Open question for the team: `docs/team-questions.md` question 6 (factory naming conventions, existing meter).

## Evidence
`docs/perf-results.md` (per-call costs), ADR-004, ADR-012, ADR-019. .NET docs: "Creating metrics" (System.Diagnostics.Metrics, static Meter for libraries without DI), "Adding distributed tracing instrumentation" (ActivitySource). OpenTelemetry semantic conventions, general naming and `error.type`. Project owner decision, 2026-10-18.
