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
- A TNEF wrapper without a usable filename uses the segment `winmail.dat`, the name Outlook gives these wrappers. The wrapper is not itself reported, so this name appears only as a path segment; if two nameless wrappers yield files with equal paths, the collision rule below separates them.
- Because nested messages stay opaque (below), the only container one `Extract` call passes through is a TNEF wrapper. Example: `winmail.dat/budget.xlsx`.
- A caller that re-extracts a nested message composes the deeper path itself: inner path = outer `FullPath` + `/` + inner `FullPath`, for example `fwd.eml` + `/` + `report.docx` = `fwd.eml/report.docx`. The golden harness `reextract` check does the same.

### Segment names
- Each segment is a `FileName` as produced by the naming rules: the last segment of the sender's filename after splitting on `/` and `\`, trimmed of whitespace and quotes.
- A filename that is empty, `.` or `..` after that step counts as nameless.
- Nameless parts use the existing fallbacks: `<content-id>.<subtype>` when a Content-ID exists, otherwise `part-<index>.<subtype>` (index = number of attachment records emitted before this part in the processing order below, starting at 0; subtype `bin` when empty); a nameless nested message is `attached.eml`.
- In a fallback name (Content-ID and subtype text), every character in the set `/ \ : * ? " < > |` and every control character (U+0000-U+001F, U+007F) is replaced by `_`. A segment therefore never contains the separator. Sender-supplied filenames are not rewritten beyond taking the last segment.

### Not filesystem-safe
- `FileName` and `FullPath` are identifiers inside the result, not safe file-system paths. They may still contain reserved device names (`CON`, `NUL`, `COM1`, ...), `:` (an NTFS alternate-data-stream marker), trailing dots or spaces, characters invalid on some file systems, or very long names. A consumer that writes files must sanitise them first. The library promises only the uniqueness rule below.

### Processing order
- Records are produced in one deterministic order: depth-first document order of the MIME tree; when a TNEF wrapper is met, its unpacked files are processed at the wrapper's position, in the order the container lists them, before the walk continues.
- `part-<index>` and the collision rule both use this order.

### Name collisions
- `FullPath` is unique within one result under ordinal, case-insensitive comparison (invariant upper-casing). This is a key-uniqueness rule, not a file-system guarantee.
- Two passes. Pass 1 computes every record's literal path (segments + `FileName`) and reserves the set of all literal paths. Pass 2 walks the records in processing order: the first record with a given literal path keeps it; each later duplicate gets `~<n>` inserted before the extension of its last segment, with n = 2, 3, ... the smallest number whose resulting path is neither in the reserved set nor already assigned. A literal path such as `image001~2.png` is therefore never displaced.
- The extension is the text from the last `.` of the last segment that is not its first character; a segment without one gets `~<n>` appended.
- Example (testable): parts named `image001.png`, `image001.png`, `image001~2.png`, in that order, get `image001.png`, `image001~3.png`, `image001~2.png`.
- `FileName` is not changed by de-duplication.

### Additional location info
- `OriginalPath` (`string?`): the raw, trimmed sender filename when it contains `/` or `\`, else null. This is today's `FullPath` meaning, kept so path information is "neither lost nor trusted".
- `ContentLocation` (`string?`): the part's `Content-Location` header value, decoded and trimmed, else null.
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
