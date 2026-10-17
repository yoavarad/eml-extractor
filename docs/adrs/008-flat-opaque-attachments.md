# ADR-008: Flat, opaque attachments — no recursion, TNEF unwrapped

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
The original requirement: for every attachment return name, size, full path if present, and the binary; "Do not extract further (no file-type detection, no nested content extraction)" (`docs/requirements.md`).

## Decision
- Every non-body leaf part becomes one attachment record holding decoded bytes.
- A nested `message/rfc822` part is serialised back to bytes as one opaque `.eml` attachment (fallback name `attached.eml`); its inner parts are not promoted.
- No file-type sniffing and no Office/PDF/image parsing.
- Outlook TNEF (`winmail.dat`) is unpacked into its contained files. This counts as peeling a transport wrapper, not as content extraction. If unpacking fails, the raw TNEF part is kept and a warning is recorded.
- Inline `cid:` images (copy-paste) are attachments; the HTML keeps its `cid:` references.
- When a filename contains `/` or `\`, the name is the basename and the full path is the raw value.
- A nameless part gets a fallback name from its Content-ID (`<cid>.<subtype>`) or its position (`part-<n>.<subtype>`).

## Alternatives
Recursing into nested messages, which the requirement explicitly excludes.

## Consequences
Callers that need a nested message's inner content must call the extractor again on its `.eml` attachment (the golden harness does this via `reextract`).

## Open questions
- The only explanation for unwrapping TNEF while not recursing into nested messages is the phrase "peels the Outlook wrapper" in `docs/requirements.md`.

## Evidence
`docs/requirements.md` ("Nested files"), `README.md`, `src/EmlExtraction/PartCollector.cs`, `tests/EmlExtraction.Tests/AttachmentTests.cs`, `InternalCorpusTests` TNEF cases.
