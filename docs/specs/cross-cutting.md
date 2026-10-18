# Cross-cutting concerns

The rules on this page apply to the whole library. Feature narratives do not repeat them.

## Runtime surface

The deliverable is a class library loaded into the caller's process. It exposes no HTTP routes and no authentication, and it has no frontend, CORS policy, health check, pagination, persistence, configuration file, or environment variable. It generates no identifiers of its own. The full statement is [ydk:crosscut:core/library-surface]. The only public entry points are [ydk:contract:api/eml-detector] and [ydk:contract:api/eml-extractor]. Per the shared team contract (ADR-012), the target public surface carries no parser-library types; this lands in Stage 02. How the library plugs into the team's extraction template and factory is the team's work and is not specified in this repo ([docs/team-questions.md](../team-questions.md)).

All input arrives as one in-memory buffer, and all output, attachment bytes included, is returned in memory. [ydk:nfr:performance/extract-1mb-attachment-throughput] shows why memory, not CPU, limits attachment-heavy mail. The memory each message needs is to be measured and capped ([ydk:nfr:performance/memory-footprint]), and inputs over a host-set limit are refused ([ydk:req:extraction/max-input-size]). Streaming stays deferred unless that measurement shows the in-memory model cannot meet its ceiling.

## Diagnostics

There is one diagnostic channel: the ordered warnings list on [ydk:entity:extraction/EmlExtractionResult], in the format defined by [ydk:crosscut:core/warning-format]. Today the library writes no logs, traces, or metrics; the target runtime telemetry is described under Observability below. Seven kinds of warning exist:

- [ydk:warning:extraction/parse-failed] - the message could not be parsed; the result is empty.
- [ydk:warning:extraction/body-decode-failed] - the chosen body could not be decoded; there is no main content.
- [ydk:warning:extraction/part-decode-failed] - one attachment could not be decoded and is skipped.
- [ydk:warning:extraction/rfc822-extract-failed] - one nested message could not be serialised and is skipped.
- [ydk:warning:extraction/tnef-extract-failed] - an Outlook wrapper could not be unpacked and is kept whole.
- [ydk:warning:extraction/internal-error] - an unexpected failure in one extraction stage, which means a library bug; that stage comes back empty and the others keep their output. Not recorded by the code yet (Stage 02).
- [ydk:warning:extraction/input-too-large] - the input is longer than the configured limit and was not parsed. Target; not implemented (Stage 02).

A caller tells warnings apart by their leading code, never by the free text after it. The policy that turns failures into warnings is [ydk:req:extraction/no-throw-on-malformed] (ADR-004).

## Observability

Target; not implemented (Stage 02). Per the project owner (ADR-021), the library also reports how it is doing at run time, through the standard measurement and tracing facilities built into .NET, so the host can see call durations, input sizes, attachment counts, warning rates by kind, detection outcomes, size-guard refusals, and library bugs by stage without wrapping every call. The library adds no telemetry package of its own: the host decides whether and where to send the data. The rules are [ydk:crosscut:core/observability] and the exact list of measurements, with their names and labels, is [ydk:req:observability/runtime-telemetry].

Mail is personal data, so nothing from a message ever appears in telemetry: no subject, body, header value, address, attachment name, or path, and not the caller's file name either. Every label comes from a small fixed set. With nobody listening, the cost must be negligible; the budget is provisional until measured ([ydk:nfr:observability/metrics-overhead]). The names may still change once if the team already has naming rules or a shared measurement source ([docs/team-questions.md](../team-questions.md), question 6).

Profiling, which finds where time and memory go, is a developer activity and adds nothing to the shipped library ([quality.md](quality.md)).

## Timestamps and text

The message date is the only timestamp. It is re-emitted in ISO 8601 round-trip form ([ydk:entity:extraction/Metadata]). All text is returned as .NET strings decoded by MimeKit ([ydk:library:deps/mimekit]). The code-page encoding provider is registered once per process so legacy charsets decode. The library never strips bidi marks or rewrites HTML.

## Failure and abuse cases

Adversarial review of the fixed design. Each answer describes current behaviour, not a proposal.

- **Malformed or hostile MIME.** The handled exception kinds become [ydk:warning:extraction/parse-failed]. Per-part failures skip only that part. Any other failure is caught and recorded as [ydk:warning:extraction/internal-error] (ADR-004). Today the code still lets such failures propagate to the caller; this is Stage 02 work.
- **Path traversal in attachment names.** The display name is reduced to its last path segment. The full path is built only from those reduced names, so it never contains a parent-directory step ([ydk:req:attachments/container-path]). The sender's raw path and declared content location are reported separately and untouched ([ydk:req:attachments/attachment-record]). Callers must not use either to write files.
- **Disguised non-mail files.** Leading-byte checks reject OLE, PDF, PNG, JPEG, ZIP, JSON, and markup before any header sniffing ([ydk:req:detection/reject-non-email-magic]).
- **Very large inputs.** Today nothing caps input size; throughput falls roughly in proportion to decoded bytes, and memory grows with them. Target: inputs over the host-set limit are refused with a warning before parsing ([ydk:req:extraction/max-input-size]), and the peak memory of a maximum-size input is measured against a fixed ceiling ([ydk:nfr:performance/memory-footprint]). No public sample of 50 MB or more has been tested ([scope.md](scope.md), DEFERRED).
- **Header blocks over 16 KB.** The detector inspects only the first 16 KB. A message whose header block runs past that window can still be accepted if the lines it does see qualify, but this has not been tested on a real sample.
- **Header collisions.** Two headers whose names differ only by a dash versus an underscore get separate metadata keys, and a header that imitates the computed custom-header key is stored under a key no real header can produce ([ydk:req:metadata/canonical-keys]). Target; today such headers merge (Stage 02).
- **Concurrent use.** Neither public class holds per-instance mutable state, and the shared parser options are read-only. The repo documents no thread-safety guarantee.
- **External service outage, rate limits, decimal precision.** Not applicable. The library makes no network calls and handles no monetary values.
