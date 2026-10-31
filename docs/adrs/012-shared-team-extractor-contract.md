# ADR-012: Shared team extractor contract (request, detection result, subfile names)

## Status
Accepted for EML; proposed to team (per project owner, 2026-10-17; bytes overload and batch API settled by the owner on 2026-10-18). Implemented. Amended by ADR-023 (2026-10-18): the error model is now exceptions for misuse, limits and bugs, warnings for content. Extended by ADR-024 (2026-10-23): the request gains `InputStream`, and `Extract(Stream)` is added. Amended by the namespace layout (2026-10-23, task #149): see "Amendment: source layout and namespaces". Extended by ADR-025 (2026-10-31): the result gains the init-only `Participants` property and the `EmailParticipant` record; the result constructor is unchanged.

## Context
The team brief gives two functions per extractor: a detection function (bytes in, "is this the format?") and an extraction function (metadata, main content, sub-files with name, size, full path if it exists, and the binary). The extracted component goes into the team's extraction template, and the team wires it into the factory.

Two extractors were built against the same brief: this repo (EML) and `yoavarad/ole-extractor` (OLE / Office). They grew different public shapes:

- EML takes raw bytes (`Extract(ReadOnlyMemory<byte>)`) and also exposes `Extract(MimeMessage)`, which leaks MimeKit types into the public API.
- OLE takes request classes (`ExtractionRequest` with `FileBytes` / `InputStream` / `FilePath` + `FileName`; `MimeDetectionRequest` with `FileBytes` + `FileName`) "so fields can be added later".
- EML detection returns `bool`; OLE returns `MimeDetectionResult { DetectedFormat, MimeType, IsSupported }`.
- Subfile records differ: EML `ExtractedAttachment { Name, Size, FullPath, Data, ContentId, Disposition }`; OLE `EmbeddedFileItem { BinaryData, FileName, PackagePath, SizeInBytes }`.
- Error model differs: EML never throws on content and records warnings (ADR-004); OLE throws typed exceptions.

A template that hosts both extractors needs one shape. There are no consumers of the EML API yet, so a breaking change costs nothing now and more later.

## Decision
Per project owner, 2026-10-17. EML adopts the following now (implemented in Stage 02) and proposes it to the team as the shared contract.

### Requests
- Extraction input is a request record, `EmlExtractionRequest`:
  - `FileBytes` - `ReadOnlyMemory<byte>`, required, the whole message. A `byte[]` converts implicitly, so callers holding an array pass it unchanged.
  - `FileName` - `string?`, optional, for traceability only. It never changes the result (detection and extraction stay content-only).
  - Room for later fields: a stream or file-path source (as OLE has) may be added as new optional members without breaking callers. None is added now. (ADR-024 adds `InputStream`; `FileBytes` is then no longer required, and exactly one of the two must be set. A file-path source is still not added.)
- `EmlExtractor.Extract(EmlExtractionRequest)` is the primary public extraction method. A null request throws `ArgumentNullException` (programmer misuse, ADR-004).
- Per project owner (2026-10-18), `Extract(ReadOnlyMemory<byte>)` stays public as a thin convenience wrapper: it builds a request with those bytes and no `FileName` and calls the primary method, so both give identical results, warnings, and size-limit behaviour. It holds no logic of its own.
- `Extract(MimeMessage)` becomes `internal`. Tests and tools that need it use `InternalsVisibleTo`.
- `EmlExtractor` takes one optional constructor argument, the input-size limit (`ydk:req:extraction/max-input-size`).

### Detection
- `EmlDetector.Detect(ReadOnlySpan<byte>)` returns `EmlDetectionResult`:
  - `IsSupported` - `bool`, true when the bytes are a message (same rule as today's `IsEml`).
  - `MimeType` - `string`, `message/rfc822` when `IsSupported`, otherwise the empty string.
- `EmlDetector.IsEml(ReadOnlySpan<byte>)` stays as the `bool` convenience the brief describes; it equals `Detect(data).IsSupported`.
- Detection takes bytes, not a request: the brief says "bytes in", and `FileName` must not influence detection. OLE's `MimeDetectionRequest` is listed as a divergence for the team (`docs/team-questions.md`).
- OLE's `DetectedFormat` enum is OLE-specific (one extractor, several formats). EML handles one format and does not add it.

### Subfile field names
The shared names are the brief's four items. EML keeps two extra fields it already has and adds two (ADR-013).

| Brief item | EML current | OLE current | Proposed (shared) | Why |
| --- | --- | --- | --- | --- |
| name | `Name` | `FileName` | `FileName` | Unambiguous ("name" of what?); already OLE's name, so OLE does not change. |
| size | `Size` | `SizeInBytes` | `SizeInBytes` | States the unit; already OLE's name. |
| full path if it exists | `FullPath` (raw filename only when it contains `/` or `\`) | `PackagePath` | `FullPath` | The brief's own term. `PackagePath` names an OOXML/OPC concept that does not fit mail. Meaning: position inside the container (ADR-013). |
| binary | `Data` | `BinaryData` | `BinaryData` | Already OLE's name; says what the bytes are. |
| (EML only) | `ContentId` | - | `ContentId` | Matches `cid:` references in HTML. Unchanged. |
| (EML only) | `Disposition` | - | `Disposition` | `inline` / `attachment`. Unchanged. |
| (EML only) | - | - | `OriginalPath` | The sender's raw path-bearing filename, today's `FullPath` meaning (ADR-013). |
| (EML only) | - | - | `ContentLocation` | The part's Content-Location header (ADR-013). |

Net effect: OLE renames one field (`PackagePath` to `FullPath`); EML renames three (`Name`, `Size`, `Data`) and redefines `FullPath`.

The record and list names are not part of this decision. EML keeps `ExtractedAttachment` and `Attachments`; OLE keeps `EmbeddedFileItem` and `EmbeddedFiles`. Aligning them is a team question.

### Error model
- EML keeps no-throw + warnings (ADR-004). The batch pipeline must not lose a batch to one bad message.
- OLE throws typed exceptions. This is a divergence the team must decide on (`docs/team-questions.md`). Until then the template adapter for each extractor maps its error model to whatever the template expects.

## Alternatives
- Keep the EML API as is and let the template adapt. Rejected: every consumer then learns two shapes, and `Extract(MimeMessage)` keeps MimeKit in the public contract.
- Adopt OLE's names wholesale, including `PackagePath`. Rejected for the path field: the brief says "full path", and "package" means nothing for mail.
- Remove the bytes overload and keep only the request method. Rejected by the project owner (2026-10-18): the brief describes bytes in, and a thin wrapper costs nothing as long as it only forwards to the request method.
- A batch method, as OLE has (`ExtractManyAsync(IEnumerable<ExtractionRequest>, maxDOP)` returning per-item results with an `Error` slot). Rejected by the project owner (2026-10-18): the factory owns parallelism, each `Extract` call is independent and stateless, and a batch API would duplicate the factory's scheduling (YAGNI). OLE needs per-item error slots because it throws; EML already returns warnings per result. Revisit only if the team's template requires a batch entry point on every extractor (`docs/team-questions.md`).
- Make detection take a request too (OLE's `MimeDetectionRequest`). Not adopted for EML: the brief's detection is bytes in, and the only extra field (`FileName`) must not affect the answer. Left to the team.
- Adopt OLE's typed exceptions. Rejected for EML (ADR-004).

## Consequences
- No batch API: callers, in practice the factory, run their own parallel loop over `Extract`.
- Breaking change to every public type: `EmlDetector`, `EmlExtractor`, `ExtractedAttachment`, and new `EmlExtractionRequest` / `EmlDetectionResult`. Acceptable now because there are no consumers.
- Golden `expected.json` files, GoldenKit, the bench, and the unit tests are updated in Stage 02 to the new names.
- `README.md` describes the Stage 02 API and carries a "Migrating from the pre-0.1 API" table.
- Manifests: `ydk:contract:api/eml-detector`, `ydk:contract:api/eml-extractor`, `ydk:entity:extraction/EmlExtractionRequest`, `ydk:entity:extraction/EmlDetectionResult`, `ydk:entity:extraction/ExtractedAttachment`, `ydk:crosscut:core/library-surface`.

## Evidence
Team brief (`docs/requirements.md`), `yoavarad/ole-extractor` DTOs (`ExtractorOLE/DTOs/ExtractionRequest.cs`, `MimeDetectionRequest.cs`, `MimeDetectionResult.cs`, `DocumentExtractionResult.cs`), `src/EmlExtraction/Models.cs`, `docs/research/brief-gap-analysis.md`. Project owner decision, 2026-10-17.

## Amendment: source layout and namespaces (2026-10-23, task #149)
Owner decision 2026-10-23 (reviewer issue #141): `src/EmlExtraction` is split into four folders, each with a matching sub-namespace. No `.cs` file stays at the project root.

| Folder / namespace | Types |
| --- | --- |
| `Abstractions` / `EmlExtraction.Abstractions` | `IEmlExtractor`, `IEmlDetector` |
| `Models` / `EmlExtraction.Models` | `EmlExtractionRequest`, `EmlExtractionResult`, `EmlDetectionResult`, `ExtractedAttachment`, `EmailParticipant` (ADR-025) |
| `Exceptions` / `EmlExtraction.Exceptions` | `EmlExtractionException`, `EmlInputTooLargeException` |
| `Logics` / `EmlExtraction.Logics` | `EmlExtractor`, `EmlDetector` and every internal type (parser, body/part/header stages, text direction, encoding cache, telemetry, stream wrappers) |

Consequences:
- Breaking change: callers replace `using EmlExtraction;` with the sub-namespaces they use. Accepted because there are no external consumers yet.
- Unchanged: assembly name, package id, `InternalsVisibleTo`, Meter and ActivitySource name `EmlExtraction` (ADR-021), instrument names, behaviour.
- The `error.type` telemetry tag carries the exception type's full name, so for the library's own exceptions it now reads `EmlExtraction.Exceptions.EmlExtractionException` / `EmlExtraction.Exceptions.EmlInputTooLargeException`.
- `NamespaceLayoutTests` pins the public layout and fails if any type lands outside the four namespaces.
