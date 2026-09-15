# Requirements (English) — coverage checklist

Original spec translated from Hebrew. Factory/template wiring is out of scope.

Standards: RFC 822 / 2822 / 5322 (EML) · RFC 2045–2049 (MIME). Implementation: C# / .NET 10, MimeKit 4.18.0 (MIT).

Legend: **Done** · **Partial** (category covered, not production samples) · **Gap**

---

## Interface

### 1. Detect function

**Requirement:** Input is a bytes object. Output is whether the file is EML.

- **Done**
- Contract: `IEmlDetector.IsEml` in `src/EmlExtraction/Models.cs`
- Implementation: `EmlDetector.IsEml` in `src/EmlExtraction/EmlDetector.cs`
- Tests: `tests/EmlExtraction.Tests/DetectorTests.cs` (accept EML / BOM / mbox; reject OLE, PDF, ZIP, PNG, JPEG, JSON)
- Golden: `corpus/golden/seed/E19-ole/` (`is_eml: false`)

### 2. Extract function

**Requirement:** Input is a file already identified as EML. Extract metadata fields, body content, and nested files (attachments).

- **Done**
- Contract: `IEmlExtractor.Extract` → `EmlExtractionResult` in `src/EmlExtraction/Models.cs`
- Implementation: `EmlExtractor.Extract` in `src/EmlExtraction/EmlExtractor.cs` (`HeaderMapper`, `BodySelector`, `PartCollector`)
- Tests: `ExtractionTests.cs`, `AttachmentTests.cs`, `GoldenTests.cs`

---

## Metadata fields

### Classic fields (To, From, etc.)

**Requirement:** Classic envelope/header fields.

- **Done**
- `HeaderMapper.Map` overlays: `from`, `to`, `cc`, `bcc`, `replyto`, `sender`, `subject`, `date`, `messageid`, `inreplyto`, `references`, `mimeversion`, `contenttype`, `returnpath`, `deliveredto`, `received`
- File: `src/EmlExtraction/HeaderMapper.cs`
- Tests: `ExtractionTests.ExtractsClassicHeadersAndUnicodeSubject`

### As many headers as possible

**Requirement:** Capture as many headers as possible, not only the classic set.

- **Done**
- Every MIME header is copied into `Metadata` with a normalized key (`lowercase`, `-` → `_`)
- File: `src/EmlExtraction/HeaderMapper.cs`
- Tests: `ExtractionTests.ExtractsClassicHeadersAndUnicodeSubject` (`dkim_signature`), golden `E10-xfields`

### `X_` fields concatenated into `xfields`

**Requirement:** All metadata fields that start with `X_` (uppercase X then underscore) are joined into one long string in field `xfields`.

- **Done** (also `X-`, the usual RFC custom-header form)
- `IsXHeader` matches `X-` or `X_`; lines joined with `\n` into `EmlExtractionResult.XFields`
- File: `src/EmlExtraction/HeaderMapper.cs`
- Tests: `ExtractionTests.ExtractsClassicHeadersAndUnicodeSubject`, golden `corpus/golden/seed/E10-xfields/`

---

## Content

**Requirement:** The main body of the email.

- **Done**
- `BodySelector.Select` prefers HTML over plain in `multipart/alternative`; result is `MainContent`
- File: `src/EmlExtraction/BodySelector.cs`
- Tests: `ExtractionTests.PrefersHtmlOverPlain`, golden `E03-html-alt`, `E01-emoji`, `E02-hebrew`, `E13-empty`

---

## Nested files (attachments)

**Requirement:** For every attachment: name, size, full path if present, and the binary itself. Do not extract further (no file-type detection, no nested content extraction).

- **Done**
- `ExtractedAttachment`: `Name`, `Size`, `FullPath`, `Data` in `src/EmlExtraction/Models.cs`
- Decode + collect: `PartCollector` in `src/EmlExtraction/PartCollector.cs`
- Nested `message/rfc822` is one opaque `.eml` blob (`AttachmentTests.NestedEmlIsOpaqueBinaryAndNotRecursed`, golden `E06-nested`)
- Path in filename: `ExtractionTests.PathInFileNameIsPreservedSeparately`
- No Office/PDF inner parse in this component

Note: Outlook `winmail.dat` (TNEF) is unpacked to flat attachments (`TnefPart` in `PartCollector.Walk`). That peels the Outlook wrapper; it does not parse Office content. There is **no TNEF sample** in the golden set yet.

---

## Tests

### 1. Load — max files per second on this hardware

**Requirement:** Understand max files/sec on available hardware so resource counts can be inferred.

- **Partial** — measured, but only on generated EMLs (not Enron/TREC/corp mail)
- Bench: `benches/EmlExtraction.Bench/Program.cs` (`ThroughputRunner`)
- Results: `docs/perf-results.md` (Apple M1 Pro, .NET 10, Release)
- Tiny mail ~48,663 extract/s · ~20 KB HTML ~16,881/s · 1 MB attachment ~259/s (1 thread)

### 2. Correctness A — emoji and exotic languages

**Requirement:** Support emoji and exotic languages.

- **Done** on generated mail
- `tests/EmlExtraction.Tests/ExoticLanguageTests.cs` (Hebrew, Arabic, Chinese, Japanese, Hindi, Thai, Russian, emoji)
- Also: `ExtractionTests.Windows1255BodyDecodes`, goldens `E01-emoji`, `E02-hebrew`, `E04-win1255`
- **Gap:** no real Gmail/Outlook messages in the set

### 3. Correctness B — attachment variety

**Requirement:** Mix of Office files, nested emails, attached images, copy-paste (CID) images, etc.

- **Partial** — MIME paths covered; Office payloads are dummy bytes

| Kind | Status | Where |
| --- | --- | --- |
| Office-like (docx/xlsx/pptx/pdf/zip) | Done as named MIME parts + SHA-256 of fake payload | `AttachmentTests.OfficeAndBinaryAttachmentsRoundTrip`, golden `E05-docx` |
| Nested email | Done, no recurse | `AttachmentTests.NestedEmlIsOpaqueBinaryAndNotRecursed`, golden `E06-nested` |
| Attached image | Done | `AttachmentTests.AttachedImageIsNotTreatedAsBody`, golden `E07-png-attach` |
| Copy-paste / CID image | Done on synthetic `multipart/related` | `AttachmentTests.CopyPasteCidImageIsExtracted`, `SampleFactory.RelatedHtmlWithCid`, golden `E08-cid` |
| Real Outlook paste / real OOXML / TNEF | Gap | `docs/edge-cases.md` E17, E18 |

---

## Out of original spec (implemented anyway)

- Detector rejects non-email magics (OLE `.msg`, PDF, …) — `EmlDetector`
- Parse does not crash the process — `EmlExtractor.Extract` catch → `Warnings`
- Tagged golden harness — seed cases in `corpus/golden/seed/` (`GoldenTests`); 1200 bulk cases via `tools/GoldenKit`
