# Open questions for the team

Date: 2026-10-17. Items this repo cannot decide alone. Background: `docs/research/brief-gap-analysis.md`. Each item names the default this repo uses until the team answers.

## 1. Shared extractor contract (ADR-012)

EML has adopted the proposal below and asks the team to make it the contract for every extractor in the template, starting with `yoavarad/ole-extractor`.

| Concept | EML (target) | OLE (today) | Question |
| --- | --- | --- | --- |
| Extraction input | `EmlExtractionRequest { FileBytes, InputStream, FileName }` (exactly one of the two sources, ADR-024) | `ExtractionRequest { FileBytes, InputStream, FilePath, FileName, DetectedMimeType }` | Agree on `FileBytes` + `FileName` as the common core? Should stream and file-path sources be in the shared contract? |
| Detection input | bytes | `MimeDetectionRequest { FileBytes, FileName }` | Request object or bytes? EML uses bytes because the brief says "bytes in" and the file name must not affect detection |
| Detection output | `EmlDetectionResult { IsSupported, MimeType }` + `IsEml` bool | `MimeDetectionResult { DetectedFormat, MimeType, IsSupported }` | Agree on `IsSupported` + `MimeType` as the common core? |
| Sub-file fields | `FileName`, `SizeInBytes`, `FullPath`, `BinaryData` | `FileName`, `SizeInBytes`, `PackagePath`, `BinaryData` | Rename OLE `PackagePath` to `FullPath` (the brief's term)? |
| Sub-file path meaning | Container path, `/`-separated, unique per result (ADR-013) | Path inside the package | Same separator, uniqueness, and "relative to the input file" rule for both? |
| Sub-file record / list names | `ExtractedAttachment` / `Attachments` | `EmbeddedFileItem` / `EmbeddedFiles` | Pick one pair, or keep per-format names? |
| Main content | `MainContent` + `MainContentMimeType` | `ExtractedText` | Align names? |
| Batch entry point | none: the factory owns parallelism (owner, 2026-10-18) | `ExtractManyAsync(maxDOP)` | EML will not add one; does the template require it of every extractor? |

Default until answered: EML ships its target names; the template adapter maps OLE.

### Error model divergence

- EML (ADR-023, owner 2026-10-18, target): throws typed exceptions for misuse (`ArgumentException` family), oversized input (`EmlInputTooLargeException : ArgumentException`), and library bugs (`EmlExtractionException`, naming the stage, original as `InnerException`). Malformed-but-readable content still returns a partial result with warning strings `"<code>: <detail>"`.
- OLE: throws typed exceptions.

The two models now point the same way. Question: should the exception names and base types be shared across extractors (for example one `ExtractionException` base the template catches)? Default: EML ships `EmlExtractionException` and `EmlInputTooLargeException`; the template adapter maps them.

## 2. Template and factory integration

Neither repo specifies how a component is plugged into the team's extraction template or the factory.

- What interface or registration does the template require (detector + extractor pair, MIME-type routing, DI registration)?
- How does the factory recurse into sub-files: does it call detection on every `BinaryData` and dispatch by `MimeType`? EML leaves nested `.eml` attachments opaque for exactly this (ADR-013).
- How should paths compose across recursion? EML proposes outer `FullPath` + `/` + inner `FullPath` (for example `fwd.eml/report.docx`). Should the request carry a parent path instead?
- What does the factory do with warnings: log, store with the record, or fail the file?
- Which packaging (project reference, NuGet) does the template use? (Target framework: section 3.)
- Which GC mode does the factory host use (Workstation or Server)? The load test reports both (ADR-015).

Default: none; this repo stays a standalone class library until the template contract is known.

## 3. Target framework

Per the owner (ADR-019), the library and its tests target both net8.0 and net10.0, because the template may still run on .NET 8 LTS. .NET 8 support ends on 2026-11-10.

- When does the template move to .NET 10? net8.0 is dropped from this library at that point.
- Does the template need any other target (for example .NET Framework or netstandard2.0)?

Default: net8.0 + net10.0 until the template moves.

## 4. License

**Closed.** Owner decision 2026-10-18: MIT, "Copyright (c) 2026 Yoav Arad" (ADR-017, `LICENSE`, `THIRD-PARTY-NOTICES.md`). All runtime dependencies (MimeKit, BouncyCastle.Cryptography, System.Security.Cryptography.Pkcs) are MIT; test dependencies are MIT, Apache-2.0 or MS-PL.

## 5. `X_` headers in internal mail

The brief says headers starting with `X_` (capital X, underscore) go into `xfields`. Public mail almost always uses `X-`. Per the owner, EML now applies the rule literally (ADR-014): only `X_` counts, case-sensitive, and `X-` headers stay in the general metadata.

- Does the team's internal mail actually carry `X_` headers (for example added by an internal gateway)?
- If not, was `X_` meant as `X-` (or as the result of a `-` to `_` key normalisation in an earlier system)?
- Should lower-case `x_` count?

Default: literal, case-sensitive `X_`.

## 6. Telemetry naming conventions (ADR-021)

Per the owner (2026-10-18), the library will emit runtime metrics and traces through the .NET built-in `Meter` and `ActivitySource`, both named `EmlExtraction`, with instruments such as `eml_extraction.extract.duration` and `eml_extraction.extract.warnings` (full list: `ydk:req:observability/runtime-telemetry`). The host wires OpenTelemetry; the library has no telemetry package dependency.

- Does the factory or the extraction template already have telemetry naming conventions (meter names, instrument prefix, tag keys)?
- Is there an existing shared meter or activity source the extractors should report under, or a required set of instruments per extractor?
- Should the OLE extractor emit the same instrument set under its own name, so dashboards can compare extractors?

Default: the names in ADR-021. They may be renamed once, before the first release that ships them, if the team answers; after that a rename is a breaking change.
