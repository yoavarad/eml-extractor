# Open questions for the team

Date: 2026-10-17. Items this repo cannot decide alone. Background: `docs/research/brief-gap-analysis.md`. Each item names the default this repo uses until the team answers.

## 1. Shared extractor contract (ADR-012)

EML has adopted the proposal below and asks the team to make it the contract for every extractor in the template, starting with `yoavarad/ole-extractor`.

| Concept | EML (target) | OLE (today) | Question |
| --- | --- | --- | --- |
| Extraction input | `EmlExtractionRequest { FileBytes, FileName }` | `ExtractionRequest { FileBytes, InputStream, FilePath, FileName, DetectedMimeType }` | Agree on `FileBytes` + `FileName` as the common core? Should stream and file-path sources be in the shared contract? |
| Detection input | bytes | `MimeDetectionRequest { FileBytes, FileName }` | Request object or bytes? EML uses bytes because the brief says "bytes in" and the file name must not affect detection |
| Detection output | `EmlDetectionResult { IsSupported, MimeType }` + `IsEml` bool | `MimeDetectionResult { DetectedFormat, MimeType, IsSupported }` | Agree on `IsSupported` + `MimeType` as the common core? |
| Sub-file fields | `FileName`, `SizeInBytes`, `FullPath`, `BinaryData` | `FileName`, `SizeInBytes`, `PackagePath`, `BinaryData` | Rename OLE `PackagePath` to `FullPath` (the brief's term)? |
| Sub-file path meaning | Container path, `/`-separated, unique per result (ADR-013) | Path inside the package | Same separator, uniqueness, and "relative to the input file" rule for both? |
| Sub-file record / list names | `ExtractedAttachment` / `Attachments` | `EmbeddedFileItem` / `EmbeddedFiles` | Pick one pair, or keep per-format names? |
| Main content | `MainContent` + `MainContentMimeType` | `ExtractedText` | Align names? |
| Batch entry point | none: the factory owns parallelism (owner, 2026-10-18) | `ExtractManyAsync(maxDOP)` | EML will not add one; does the template require it of every extractor? |

Default until answered: EML ships its target names; the template adapter maps OLE.

### Error model divergence

- EML: never throws on content; every problem is a warning string `"<code>: <detail>"` on the result, and a stage that fails still returns the other stages' output (ADR-004). Only a null argument throws.
- OLE: throws typed exceptions.

Question: which model does the template expect? If exceptions, should EML add a strict mode that throws on `parse_failed` / `internal_error`, or should the template adapter convert warnings? Default: EML keeps no-throw.

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

This repo has no license yet. Proposal: MIT, with `THIRD-PARTY-NOTICES.md` (ADR-017). All runtime dependencies (MimeKit, BouncyCastle.Cryptography, System.Security.Cryptography.Pkcs) are MIT; test dependencies are MIT or Apache-2.0.

- Project owner: confirm MIT, and the copyright holder line.
- Team: is there a required license for template components (for example Apache-2.0 for its patent grant)?

Default: no `LICENSE` file is added until the owner confirms.

## 5. `X_` headers in internal mail

The brief says headers starting with `X_` (capital X, underscore) go into `xfields`. Public mail almost always uses `X-`. Per the owner, EML now applies the rule literally (ADR-014): only `X_` counts, case-sensitive, and `X-` headers stay in the general metadata.

- Does the team's internal mail actually carry `X_` headers (for example added by an internal gateway)?
- If not, was `X_` meant as `X-` (or as the result of a `-` to `_` key normalisation in an earlier system)?
- Should lower-case `x_` count?

Default: literal, case-sensitive `X_`.
