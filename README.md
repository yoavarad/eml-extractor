# EML extraction

C# library for .NET 8 and .NET 10 (`net8.0`, `net10.0`) for RFC 822/2822/5322 mail and RFC 2045–2049 MIME. Parser: [MimeKit](https://github.com/jstedfast/MimeKit) 4.18.0 (MIT).

Repo: https://github.com/yoavarad/eml-extractor

---

## Interface

Public types live in four namespaces (ADR-012 amendment):

```csharp
using EmlExtraction.Abstractions; // IEmlDetector, IEmlExtractor
using EmlExtraction.Exceptions;   // EmlExtractionException, EmlInputTooLargeException
using EmlExtraction.Logics;       // EmlDetector, EmlExtractor
using EmlExtraction.Models;       // EmlExtractionRequest, EmlExtractionResult, EmlDetectionResult, ExtractedAttachment
```

### 1. Detect

Input: bytes. Output: whether the buffer is an EML (RFC 5322) message.

```csharp
EmlDetectionResult detection = new EmlDetector().Detect(bytes);
// detection.IsSupported: true for a message; detection.MimeType: "message/rfc822", otherwise ""

bool ok = new EmlDetector().IsEml(bytes); // same answer as Detect(bytes).IsSupported
```

`Detect` and `IsEml` take a `ReadOnlySpan<byte>` (a `byte[]` converts implicitly). They sniff headers (and mbox `From `), ignore the file name and the file extension, and reject OLE `.msg`, PDF, ZIP, PNG, JPEG, and markup/JSON (a first byte of `{`, `[` or `<`).

### 2. Extract

Input: a buffer already identified as EML. Output: metadata, main content, nested files.

```csharp
var extractor = new EmlExtractor(); // or new EmlExtractor(maxInputBytes: 50_000_000)

// Primary method: a request record.
EmlExtractionResult result = extractor.Extract(new EmlExtractionRequest
{
    FileBytes = bytes,         // set exactly one of FileBytes / InputStream; a byte[] converts implicitly
    FileName = "mail-17.eml"   // optional; traceability only, never changes the result
});

// Convenience wrapper: same result as a request with these bytes and no FileName.
EmlExtractionResult same = extractor.Extract(bytes);

// Stream input (ADR-024): parsed straight from the stream, without first copying the whole input.
// Read forward from the current position; need not be seekable; never seeked or disposed (you own it).
using var file = File.OpenRead("mail-17.eml");
EmlExtractionResult fromStream = extractor.Extract(file); // or new EmlExtractionRequest { InputStream = file }
```

Pass a stream when the message is on disk or the network: it saves the whole-input `byte[]`. If you already hold the bytes, use the bytes path, which allocates less (see `docs/perf-results.md`, stream input).

`maxInputBytes` is the largest input, in bytes, that is parsed (default 500 MiB). See Errors below for what is thrown and what stays a warning.

#### Metadata

`result.Metadata` is an ordinal `IReadOnlyDictionary<string, string>` with one key per header:

- Key = the header field name, trimmed and lower-cased; `-` and `_` are kept (`Message-ID` gives `message-id`, `X-Foo` gives `x-foo`, `X_Foo` gives `x_foo`). A repeated header is joined with `\n` in order.
- Canonical overlay: `from`, `to`, `cc`, `bcc`, `reply-to`, `sender` (formatted mailboxes), `date` (ISO 8601 when it parses, otherwise the raw value), `message-id`, `in-reply-to`, `references`. Every other header (`subject`, `content-type`, `received`, ...) keeps its decoded, unfolded value.
- Fixed keys, present in every result (including a failed parse), `""` when unknown: `from`, `to`, `cc`, `bcc`, `xfields`.
- `xfields`: headers whose name starts with the literal `X_` (capital X, underscore) are concatenated into `Metadata["xfields"]`, one `Name: value` line per header joined by `\n`; `""` when none. `X-` headers are not in it; they stay under their own key. A real header named `xfields` is stored as `header:xfields`.
- The text directions are not metadata keys: read `result.SubjectDirection` and `result.ContentDirection` (`ltr` or `rtl`).

#### Content

`result.MainContent` is the main body of the mail (`result.MainContentMimeType` is its media type). If both HTML and plain exist (`multipart/alternative`), HTML is used.

Text direction: HTML `dir="rtl"` / `dir="ltr"` (and `direction:` CSS) is kept in the HTML and copied to `result.ContentDirection`. Plain text uses the first strong character (Hebrew/Arabic -> `rtl`, Latin/CJK -> `ltr`). Unicode bidi marks (`U+200F` RLM, `U+200E` LRM) are not stripped.

#### Nested files (attachments)

`result.Attachments` is a list of `ExtractedAttachment`, one per part, in MIME tree order:

| Field | Meaning |
| --- | --- |
| `FileName` | File name (basename of the sender's name, or a generated fallback) |
| `SizeInBytes` | Decoded size; equals `BinaryData.Length` |
| `FullPath` | Container path (ADR-013): the file containers the part came through (a TNEF `winmail.dat`), then `FileName`, joined by `/`, for example `winmail.dat/budget.xlsx`. Never null; equals `FileName` for a top-level part. Unique within one result (ordinal, case-insensitive; a duplicate gets `~2`, `~3`, ... before the extension). An identifier, not a filesystem-safe path: sanitise before writing files |
| `BinaryData` | Decoded binary |
| `ContentId` | Content-ID without angle brackets, or `null` |
| `Disposition` | Content-Disposition value (`attachment`, `inline`), or `null` |
| `OriginalPath` | The sender's raw file name when it contained `/` or `\`, else `null`. Information only; do not use it to write files |
| `ContentLocation` | The part's Content-Location header, decoded and trimmed, or `null` |

No further extraction: no file-type detection, no parsing inside Office/PDF/images, no walking a nested `.eml`. A forwarded mail is one opaque `.eml` blob. Copy-paste images (`cid:` / `multipart/related`) are attachments with the image bytes.

---

## Errors

Malformed mail content never throws: it is recorded as a warning on `result.Warnings` and you get the partial result (ADR-023). Everything else throws.

| Case | Exception | Properties |
| --- | --- | --- |
| Null `request` or null stream | `ArgumentNullException` | `ParamName` |
| Both or neither of `FileBytes` / `InputStream` set | `ArgumentException` | `ParamName` (`request`) |
| `maxInputBytes <= 0` (constructor) | `ArgumentOutOfRangeException` | `ParamName`, `ActualValue` |
| Input longer than `maxInputBytes` (default 500 MiB; an input of exactly the limit is parsed) | `EmlInputTooLargeException` (derives from `ArgumentException`) | `InputLength` (for a stream: bytes read, limit + 1), `MaxInputBytes`, `ParamName` (`request`) |
| Unexpected exception in a stage (a library bug) | `EmlExtractionException` | `Stage` (`detect`, `parse`, `metadata`, `body` or `attachments`), `InnerException` (the original, with its stack trace) |

- Checks for the first four rows run before any parsing, except the size check on a stream, which runs while it is read (reading stops at limit + 1 bytes). Read errors from your stream become a `parse_failed` warning. Messages name the parameter and the size or limit; they never quote message content.
- `EmlExtractionException.Message` names the stage and the inner exception's type only; read the details from `InnerException`.
- `OutOfMemoryException` and exceptions thrown by your own `MeterListener` callbacks are not wrapped.
- `EmlDetector.Detect` / `IsEml` return "not supported" / `false` for input that is not a message. That is a result, not an error. They throw only `EmlExtractionException` (stage `detect`) on a library bug.
- These content warnings stay warnings (`"<code>: <detail>"`): `parse_failed`, `body_decode_failed`, `part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed`. An empty `Warnings` list on a returned result means nothing was degraded.

Batch callers catch per item, so one bad file does not lose the batch:

```csharp
var extractor = new EmlExtractor();
foreach (var (name, bytes) in files)
{
    try
    {
        var result = extractor.Extract(new EmlExtractionRequest { FileBytes = bytes, FileName = name });
        Index(name, result); // result.Warnings lists any degraded content
    }
    catch (EmlInputTooLargeException ex)
    {
        Skip(name, $"{ex.InputLength} bytes exceeds the {ex.MaxInputBytes} byte limit");
    }
    catch (EmlExtractionException ex)
    {
        ReportBug(name, ex.Stage, ex.InnerException); // library bug: keep the inner exception for the report
    }
}
```

Catch `EmlInputTooLargeException` before any `ArgumentException` handler, since it derives from it.

## Migrating from the pre-0.1 API

Every row is a public break (ADR-012, 013, 014, 016, 023). Code that uses none of the old members compiles unchanged except for the constructor row.

| Old API / behavior | New API / behavior | What callers change |
| --- | --- | --- |
| `attachment.Name` | `FileName` | Rename. |
| `attachment.Size` | `SizeInBytes` | Rename. |
| `attachment.Data` | `BinaryData` | Rename. |
| `attachment.FullPath` is `string?`: the raw sender file name when it contained `/` or `\`, else `null` | `FullPath` is a non-null `string`: the container path (a TNEF wrapper segment, then `FileName`, joined by `/`), unique per result, not filesystem-safe (ADR-013) | Stop null-checking it. Read the old meaning from `OriginalPath`. Do not write files from it without sanitising. |
| No field for the sender path or Content-Location | New `OriginalPath` (`string?`) and `ContentLocation` (`string?`) | Use them if you need that information. |
| `result.XFields`; collected `X-` and `X_` headers, case-insensitive | Property removed. `result.Metadata["xfields"]`, always present; only headers starting with the literal `X_` | Read `Metadata["xfields"]`. `X-` headers are now only under their own keys, for example `x-mailer`. |
| Metadata keys folded `-` to `_` and added punctuation-free duplicates: `reply_to` + `replyto`, `message_id` + `messageid`, `in_reply_to` + `inreplyto`, `mimeversion`, `returnpath`, `deliveredto`, `dkim_signature`; `X-Foo` and `X_Foo` merged | One lower-case key per header, `-` and `_` kept: `reply-to`, `message-id`, `in-reply-to`, `mime-version`, `return-path`, `delivered-to`, `dkim-signature`, `x-foo` and `x_foo` apart (ADR-016) | Update every key lookup. |
| `Metadata["contenttype"]` (top-level MIME type) | Key removed. `Metadata["content-type"]` holds the decoded, unfolded header value | Use `result.MainContentMimeType` for the body's type. |
| `Metadata["subject_direction"]`, `Metadata["content_direction"]` | Keys removed | Read `result.SubjectDirection` and `result.ContentDirection`. |
| Failed parse returned an empty `Metadata` map | `from`, `to`, `cc`, `bcc`, `xfields` are always present (`""`) | None, unless you checked for an empty map. |
| Positional records: `ExtractedAttachment(Name, Size, FullPath, Data, ContentId, Disposition)` and `EmlExtractionResult(Metadata, XFields, MainContent, MainContentMimeType, Attachments, Warnings)` | `ExtractedAttachment(FileName, SizeInBytes, FullPath, BinaryData, ContentId, Disposition, OriginalPath, ContentLocation)` (8 parameters); `EmlExtractionResult(Metadata, MainContent, MainContentMimeType, Attachments, Warnings)`, no `XFields` | Update every constructor call and `Deconstruct` in tests and mocks to the new arity and order. |
| `Extract(ReadOnlyMemory<byte>)` was the only extraction method; `IEmlExtractor` had only it | `Extract(EmlExtractionRequest)` is primary and is also on `IEmlExtractor`; `Extract(ReadOnlyMemory<byte>)` stays as a wrapper with identical results | Optional for callers. Implementers and mocks of `IEmlExtractor` must add the new method. |
| Public `Extract(MimeMessage)` | Internal | Pass bytes or an `EmlExtractionRequest`; MimeKit types are no longer in the public API. |
| `bool EmlDetector.IsEml(bytes)` only | `EmlDetectionResult Detect(bytes)` (`IsSupported`, `MimeType`) added; `IsEml` unchanged | Optional. |
| `new EmlExtractor()` with an implicit parameterless constructor | `EmlExtractor(long maxInputBytes = 500 MiB)`; `maxInputBytes <= 0` throws `ArgumentOutOfRangeException` | Source compatible. A binary break: recompile everything that references the library. |
| Oversized input: `input_too_large` warning and an empty result | Throws `EmlInputTooLargeException` (an `ArgumentException`; `InputLength`, `MaxInputBytes`) | Catch the exception, not the warning. |
| A stage fault: `internal_error` warning and a partial result | Throws `EmlExtractionException` (`Stage`, `InnerException`) | Catch it per item in a batch. Stop checking for `internal_error`. |
| Null request and other misuse had no defined behavior | `ArgumentNullException` for a null request; see Errors | Fix the call. |
| Targets `net10.0` only | Targets `net8.0` and `net10.0` with identical behavior | None; a .NET 8 host can now reference it. |

---

## Telemetry

The library publishes runtime metrics through the BCL `System.Diagnostics.Metrics` API and traces through `System.Diagnostics.ActivitySource` (ADR-021). It takes no OpenTelemetry, logging or EventSource dependency; nothing is emitted unless a listener subscribes. Meter name and ActivitySource name: `EmlExtraction` (version = the library's informational version).

| Instrument | Kind | Unit | Tags |
| --- | --- | --- | --- |
| `eml_extraction.extract.duration` | Histogram<double> | `s` | `eml_extraction.outcome` |
| `eml_extraction.extract.input.size` | Histogram<long> | `By` | - |
| `eml_extraction.extract.attachments` | Histogram<int> | `{attachment}` | - |
| `eml_extraction.extract.warnings` | Counter<long> | `{warning}` | `eml_extraction.warning.code` |
| `eml_extraction.detect.calls` | Counter<long> | `{call}` | `eml_extraction.detect.result` |
| `eml_extraction.extract.internal_errors` | Counter<long> | `{error}` | `eml_extraction.stage`, `error.type` |
| `eml_extraction.extract.input_too_large` | Counter<long> | `{request}` | - |

Tag values (closed sets):
- `eml_extraction.outcome`: `clean`, `warnings`, `parse_failed`, `input_too_large` (threw `EmlInputTooLargeException`), `internal_error` (threw `EmlExtractionException`)
- `eml_extraction.warning.code`: `parse_failed`, `body_decode_failed`, `part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed`
- `eml_extraction.detect.result`: `supported`, `not_supported`
- `eml_extraction.stage`: `parse`, `metadata`, `body`, `attachments`
- `error.type`: the .NET exception type's full name

Activities (all kind Internal; none for `Detect` / `IsEml`):
- `eml_extraction.extract`, one per `Extract` call. Tags `eml_extraction.input.size` (bytes), `eml_extraction.attachments.count`, `eml_extraction.warnings.count` (the two counts only on a returned result), `eml_extraction.outcome`.
- Children `eml_extraction.parse`, `eml_extraction.metadata`, `eml_extraction.body`, `eml_extraction.attachments`, created only when the parent is sampled with all data.
- When `Extract` throws `EmlExtractionException`, the failing stage activity gets status Error and `error.type` (inner exception type), and `eml_extraction.extract` gets status Error, `error.type` `EmlExtraction.Exceptions.EmlExtractionException` and outcome `internal_error`. On `EmlInputTooLargeException` the parent gets status Error, its `error.type` and outcome `input_too_large`. No status description, event or exception message is recorded. Exceptions thrown by your own listener callbacks propagate unchanged.

PII: no tag, activity name, event, baggage or status description carries message content or anything derived from it (subject, body, headers, addresses, file names, content IDs), the request `FileName`, or exception messages. A unit test enforces this.

Subscribe with OpenTelemetry:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("EmlExtraction")
    .AddOtlpExporter()
    .Build();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("EmlExtraction")
    .AddOtlpExporter()
    .Build();
```

Or watch live: `dotnet-counters monitor --counters EmlExtraction -p <pid>`.

## Setup and run

Requires the .NET 10 SDK (`global.json` pins `10.0.401`); the `net8.0` tests also need the .NET 8 runtime. If `dotnet` is missing:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
```

Clone and restore:

```bash
git clone https://github.com/yoavarad/eml-extractor.git
cd eml-extraction
dotnet restore
```

### Use from code

Reference `src/EmlExtraction/EmlExtraction.csproj` and call `EmlDetector` / `EmlExtractor` as above.

### Tests, score, throughput

```bash
dotnet test                                                    # unit tests + seed goldens (+ public fixtures if present)
dotnet run --project tools/GoldenKit -c Release -- score       # 1200-case quality score
dotnet run --project tools/GoldenKit -c Release -- probe       # harvest local public mail under corpus/internal/
dotnet run --project benches/EmlExtraction.Bench -c Release    # max files/sec on this machine
```

Regenerate the bulk golden files from the authored catalog (does **not** use the extractor to write expected values):

```bash
dotnet run --project tools/GoldenKit -c Release -- gen
```

### Layout

```
src/EmlExtraction/           detect + extract library (Abstractions/, Models/, Logics/, Exceptions/)
tests/EmlExtraction.Tests/   unit tests + seed goldens
benches/EmlExtraction.Bench  files/sec
tools/GoldenKit/             generate and score goldens
corpus/golden/seed/          small set run by `dotnet test`
corpus/golden/bulk/          1200 tagged cases
corpus/golden/catalog.jsonl  one row per bulk case
corpus/internal/             public real mail (gitignored; see Real-world tests)
docs/                        requirements map, golden plan, perf, edge cases
```

---

## Tests (what the original spec asked for)

### 1. Load — max files per second

Measured on Apple M1 Pro, 1 thread, Release, in-memory `byte[]` (no disk). Use this to size cores and RAM.

| Mix | Extract / s |
| --- | ---: |
| Tiny mail | ~48,700 |
| ~20 KB HTML | ~16,900 |
| Mail + 1 MB attachment | ~259 |

Multi-core capacity is measured, not estimated: `dotnet run -c Release --project benches/EmlExtraction.Bench -- sweep` runs the DOP sweep (ADR-015). Attachment-heavy mail is RAM-bound. Full table and sweep options: [docs/perf-results.md](docs/perf-results.md).

Corpus-level profiling (ADR-020): `scripts/profile-corpus.sh` or `scripts/profile-corpus.ps1 [--corpus <path>]` runs the sweep and large-file pass on net8.0 and net10.0 under `dotnet-trace`, `dotnet-counters` and `dotnet-gcdump` (local tools, `dotnet tool restore`) and writes the nettrace, counters CSV, gcdump and `manifest.json` (GC counts, pause time, hardware) to the gitignored `artifacts/perf/<stamp>/`. Open a nettrace with `dotnet-trace convert --format speedscope` or PerfView. A missing corpus path prints `SKIPPED` and profiles the synthetic set only.

### 2. Correctness A — emoji and exotic languages

Subject and body round-trip for Hebrew, Arabic, Chinese, Japanese, Hindi, Thai, Russian, emoji, mixed scripts, RFC 2047 encoded-words, Windows-1255 / other legacy charsets, SMTPUTF8 addresses.

Golden tags: `hebrew` (12/12), `arabic` (10/10), `rtl`, `emoji`, `unicode`, `rfc2047`, `rfc6532`.

### 3. Correctness B — attachment mix

Office-like parts (doc/docx/xls/xlsx/ppt/pptx/odt/ods/odp/rtf/pdf), nested emails (`message/rfc822`, not recursed), attached images, copy-paste CID images, plus zip/ics/vcf/media/exe stubs.

Quality bar in CI: **name + decoded SHA-256**. Payloads there are tiny typed stubs. Real Office/CID/TNEF files are covered by the public harvest below, not by the committed goldens.

---

## Real-world tests

Public mail, not in git (`corpus/internal/` is gitignored; licenses and PII vary). Two layers:

1. **Harvest** — detector + extract over every local public sample (`golden probe`). Last run: **12,853** detected messages, **0** throws, **0** parse failures, **0** warnings, ~3,500 extract/s.
2. **Targeted asserts** — `InternalCorpusTests` (17 cases). Skipped automatically if the files are not on disk; CI does not require them.

`dotnet test` includes those asserts when the files exist. Re-run the harvest with `golden probe` (see commands above). Full map: [docs/edge-cases.md](docs/edge-cases.md).

| What was proven | Source | Result |
| --- | --- | --- |
| Does not crash on mixed real mail | SpamAssassin 2003/2005, Nazario phishing mboxes, [cw-l/email-corpus](https://github.com/cw-l/email-corpus), MimeKit TestData | 12,853 / 12,853 detected; mbox `From ` handled |
| TNEF / `winmail.dat` unpacks to real files | Apache POI HMEF `quick-winmail.dat`, `bug52400-…dat`; Zimbra Outlook 11 `tnef.msg`; MimeKit `ukr.eml` | OLE `.doc` + PDF + PNG; **0** leftover `winmail.dat` (82 TNEF-header mails) |
| Real Office MIME (not stubs) | php-mime-mail-parser `issue274.eml`; ProtonMail parser `forwarding_html_attachment.eml` | OOXML `docx` (PK, 11 KB) + PDF + ODT; Proton `docx`/`xlsx`/`pdf` (PK / `%PDF-` magic; Proton zips are truncated) |
| Nested `message/rfc822` stays opaque | `issue158a`, `issue274.eml`; 154 nested `.eml` in harvest | One `.eml` blob; inner parts not promoted |
| CID paste image | php-mails `m0129`; Chatwoot Outlook 16.0 fixture | Complete PNG + `cid:` left in HTML. Chatwoot JPEG is truncated upstream |
| Outlook `image001.png@01D…` shape | Headers from [Freescout #1162](https://github.com/freescout-help-desk/freescout/issues/1162) | HTML `cid:` matches inline `Content-ID`; PNG is a stand-in (GitHub stripped the original base64) |
| Hebrew from real clients | Gmail `issue149` (`iso-8859-8-i`); 1992 Bellcore ISO-8859-8 in MimeKit `jwz.mbox` | Subject `מענה 'אני לא נמצא': Invoice …`; body/subject `תירבע` |
| Exchange-style headers / `xfields` | [Lee Holmes O365 gist](https://gist.github.com/LeeHolmes/d83dbfd7d4683de2f3e517fdd71e1923); 113 harvest mails | Max 71 headers, 68 xfield lines |
| Legacy charsets, DSN, S/MIME | Jimmy Shift_JIS; GB2312 spam; Thunderbird signed | Decode without throw; `.p7s` kept as attachment |

Not used: **Enron** (attachments already stripped; 422 MB download aborted). **TREC 2007** Waterloo tarball is 404.

Still missing vs live corp mail: Outlook/Gmail Arabic body, Outlook Hebrew body, header block >16 KB, TNEF wrapping modern `.docx`.

---

## Golden set

Expected values are written with the EML (subject, body, hashes). The parser is never used as the source of truth when generating `expected.json`.

| Set | Size | When it runs |
| --- | ---: | --- |
| Seed | ~12 cases | `dotnet test` |
| Bulk | 1000 core + 200 RFC extras = **1200** | `golden score` |

Last score: **1212/1212 cases**, **11333/11333 checks** (seed + bulk). Report: [docs/golden-score.txt](docs/golden-score.txt). Slices: [docs/golden.md](docs/golden.md).

Bulk families include detection, classic headers, `xfields`, unicode/emoji/RTL, charsets, HTML vs plain, transfer encodings, CID paste, SMTPUTF8, Office/PDF, nested mail, images, archives, folded headers, path-in-filename, and RFC 2047 / 2183 / 2387 / 2392 / 6532 extras.

Spec ↔ code: [docs/requirements.md](docs/requirements.md). Edge-case map (including the public harvest): [docs/edge-cases.md](docs/edge-cases.md).
