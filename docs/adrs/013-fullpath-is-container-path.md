# ADR-013: FullPath is the container path; nested messages stay opaque

## Status
Accepted (per project owner, 2026-10-17). Implementation pending, tracked as Stage 02 work. Amends ADR-008.

## Context
The brief asks, for every sub-file, for its "full path if it exists". Today `FullPath` is set only when the sender's MIME filename contains `/` or `\`, and it then holds that raw string. That reading has three problems:

- It is almost always null, so the brief's field carries no information for real mail.
- The `Content-Location` header, which some clients use for the part's location, is ignored.
- It says nothing about where in the message the part came from. A file unpacked from `winmail.dat` looks the same as a top-level attachment.

OLE's `PackagePath` is the location of the embedded file inside its container. Read the same way, "full path" for mail is the part's position in the message.

ADR-008 also leaves an open question: TNEF wrappers are unpacked, but nested `message/rfc822` parts stay opaque.

## Decision
Per project owner, 2026-10-17.

### FullPath
- `FullPath` is the container path of the subfile: the names of the file containers the part was reached through, then the part's own `FileName`, joined by `/`.
- It is relative to the message given to `Extract`. It never includes the request's `FileName`.
- It is always set (never null). For a part not inside any file container, `FullPath` equals `FileName`.
- Containers that add a segment: a TNEF wrapper (segment = the wrapper's `FileName`, usually `winmail.dat`) and a nested message (segment = its `FileName`, for example `fwd.eml`). MIME multipart nodes are structure, not files, and add no segment.
- Because nested messages stay opaque (below), the only container one `Extract` call passes through is a TNEF wrapper. Example: `winmail.dat/budget.xlsx`.
- A caller that re-extracts a nested message composes the deeper path itself: inner path = outer `FullPath` + `/` + inner `FullPath`, for example `fwd.eml` + `/` + `report.docx` = `fwd.eml/report.docx`. The golden harness `reextract` check does the same.

### Segment names
- Each segment is a `FileName` as produced by the naming rules: the last segment of the sender's filename after splitting on `/` and `\`, trimmed of whitespace and quotes.
- A filename that is empty, `.` or `..` after that step counts as nameless.
- Nameless parts use the existing fallbacks: `<content-id>.<subtype>` when a Content-ID exists, otherwise `part-<index>.<subtype>` (index = attachments collected so far; subtype `bin` when empty); a nameless nested message is `attached.eml`.
- In a fallback name, `/` and `\` (possible inside a Content-ID) are replaced by `_`, so a segment never contains the separator.

### Name collisions
- `FullPath` is unique within one result. Paths are compared ordinal, case-insensitive, so they are safe on case-insensitive file systems.
- When a path is already taken, the last segment gets `~<n>` inserted before its extension, with n = 2, 3, ... the smallest free number: `image001.png`, `image001~2.png`. The extension is the text from the last `.` that is not the first character; a segment without one gets `~<n>` appended.
- `FileName` is not changed by de-duplication.

### Additional location info
- `OriginalPath` (`string?`): the raw, trimmed sender filename when it contains `/` or `\`, else null. This is today's `FullPath` meaning, kept so path information is "neither lost nor trusted".
- `ContentLocation` (`string?`): the part's `Content-Location` header value, decoded and trimmed, else null. For TNEF-unpacked files it is null unless MimeKit exposes one on the unpacked part.
- Neither is used to build `FullPath`. Callers must not use either to write files.

### Nested messages stay opaque
- A `message/rfc822` part stays one opaque attachment holding the re-serialised message. Its inner parts are not promoted, and no `fwd.eml/...` path appears in one `Extract` call.
- Justification against the brief: "no need to extract further". A nested message is a complete mail file; extracting it is the same job the factory does for any `.eml` it finds, and the factory decides how deep to go. TNEF is different: `winmail.dat` is an Outlook transport wrapper around the outer message's own attachments, not a file the sender attached, so unpacking it is part of reading the outer message (ADR-008).
- This closes ADR-008's open question.

## Alternatives
- Keep `FullPath` as the raw path-bearing filename (status quo). Rejected: null for nearly all real mail and silent about TNEF origin.
- Use `Content-Location` as `FullPath`. Rejected: rarely present, sender-controlled, often a URL, and not a position in the message.
- Include multipart nodes in the path (for example `mixed[1]/related[0]/image001.png`). Rejected: the brief asks for a file path, and multipart indices are noise to the factory.
- Recurse into nested messages and emit `fwd.eml/report.docx` directly. Rejected: contradicts "no need to extract further", duplicates the factory's job, and makes one call's output size unbounded.
- Leave `FullPath` null for top-level parts. Rejected: callers would need two code paths; a single always-set path is simpler.

## Consequences
- `ExtractedAttachment` gains `OriginalPath` and `ContentLocation`; `FullPath` becomes non-null (`ydk:entity:extraction/ExtractedAttachment`, `ydk:req:attachments/container-path`).
- Golden `full_path` expectations change meaning; the path-in-filename slice moves to `original_path`. GoldenKit and the seed goldens are updated in Stage 02.
- Breaking change, covered by ADR-012.

## Evidence
`src/EmlExtraction/PartCollector.cs`, `docs/requirements.md` ("Nested files"), ADR-008, `yoavarad/ole-extractor` `EmbeddedFileItem.PackagePath`. Project owner decision, 2026-10-17.
