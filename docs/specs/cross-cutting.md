# Cross-cutting concerns

The rules on this page apply to the whole library. Feature narratives do not repeat them.

## Runtime surface

The deliverable is a class library loaded into the caller's process. It exposes no HTTP routes and no authentication, and it has no frontend, CORS policy, health check, pagination, persistence, configuration file, or environment variable. It generates no identifiers of its own. The full statement is [ydk:crosscut:core/library-surface]. The only public entry points are [ydk:contract:api/eml-detector] and [ydk:contract:api/eml-extractor].

All input arrives as one in-memory buffer, and all output, attachment bytes included, is returned in memory. Peak memory per message is roughly the input size plus the decoded attachment size. [ydk:nfr:performance/extract-1mb-attachment-throughput] shows why memory, not CPU, limits attachment-heavy mail.

## Diagnostics

There is one diagnostic channel: the ordered warnings list on [ydk:entity:extraction/EmlExtractionResult], in the format defined by [ydk:crosscut:core/warning-format]. The library writes no logs, traces, or metrics. Six kinds of warning exist:

- [ydk:warning:extraction/parse-failed] - the message could not be parsed; the result is empty.
- [ydk:warning:extraction/body-decode-failed] - the chosen body could not be decoded; there is no main content.
- [ydk:warning:extraction/part-decode-failed] - one attachment could not be decoded and is skipped.
- [ydk:warning:extraction/rfc822-extract-failed] - one nested message could not be serialised and is skipped.
- [ydk:warning:extraction/tnef-extract-failed] - an Outlook wrapper could not be unpacked and is kept whole.
- [ydk:warning:extraction/internal-error] - an unexpected failure, which means a library bug; the result is empty. Not recorded by the code yet (Stage 02).

A caller tells warnings apart by their leading code, never by the free text after it. The policy that turns failures into warnings is [ydk:req:extraction/no-throw-on-malformed] (ADR-004).

## Timestamps and text

The message date is the only timestamp. It is re-emitted in ISO 8601 round-trip form ([ydk:entity:extraction/Metadata]). All text is returned as .NET strings decoded by MimeKit ([ydk:library:deps/mimekit]). The code-page encoding provider is registered once per process so legacy charsets decode. The library never strips bidi marks or rewrites HTML.

## Failure and abuse cases

Adversarial review of the fixed design. Each answer describes current behaviour, not a proposal.

- **Malformed or hostile MIME.** The handled exception kinds become [ydk:warning:extraction/parse-failed]. Per-part failures skip only that part. Any other failure is caught and recorded as [ydk:warning:extraction/internal-error] (ADR-004). Today the code still lets such failures propagate to the caller; this is Stage 02 work.
- **Path traversal in attachment names.** The display name is reduced to its last path segment. The raw path is reported separately and untouched ([ydk:req:attachments/attachment-record]). Callers must not use the raw path to write files.
- **Disguised non-mail files.** Leading-byte checks reject OLE, PDF, PNG, JPEG, ZIP, JSON, and markup before any header sniffing ([ydk:req:detection/reject-non-email-magic]).
- **Very large inputs.** Nothing caps input size. Throughput falls roughly in proportion to decoded bytes, and memory grows with them. No public sample of 50 MB or more has been tested ([scope.md](scope.md), DEFERRED).
- **Header blocks over 16 KB.** The detector inspects only the first 16 KB. A message whose header block runs past that window can still be accepted if the lines it does see qualify, but this has not been tested on a real sample.
- **Concurrent use.** Neither public class holds per-instance mutable state, and the shared parser options are read-only. The repo documents no thread-safety guarantee.
- **External service outage, rate limits, decimal precision.** Not applicable. The library makes no network calls and handles no monetary values.
