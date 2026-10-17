# Project Rules

Conventions, preferences, and domain knowledge.

## Product Context

We are building an in-process C# / .NET 10 class library that decides whether a byte buffer is an EML message (RFC 822/2822/5322 + RFC 2045-2049 MIME). For a message, it extracts the header metadata (every header under one canonical key, the classic fields in parsed form, and headers starting with literal `X_` joined into the metadata field `xfields`), one main body (HTML preferred) with its ltr/rtl direction, and every attachment as file name, decoded size, container path, and decoded bytes. It does no file-type detection and no recursion into nested content. The consumer is a downstream file-processing pipeline (the "factory" of the original Hebrew spec, out of scope); the component goes into the team's extraction template. A sibling OLE extractor follows the same brief; ADR-012 proposes one shared contract for both. Brief coverage: `docs/research/brief-gap-analysis.md`; open team items: `docs/team-questions.md`. Specs: `docs/specs/overview.md`. Original spec map: `docs/requirements.md`.

## Conventions

- Stack is fixed (ADR-001): .NET SDK pinned by `global.json`; build with `TreatWarningsAsErrors=true`; `dotnet format --verify-no-changes` must pass; tests use xUnit.
- Public surface today: `EmlDetector.IsEml`, `EmlExtractor.Extract` (byte and `MimeMessage` overloads), and the result records in `Models.cs`. Target (ADR-012, Stage 02): `EmlDetector.Detect` + `IsEml`, `EmlExtractor(maxInputBytes)` + `Extract(EmlExtractionRequest)` only; `Extract(MimeMessage)` internal; no MimeKit type in the public API. Keep helpers `internal`.
- Shared sub-file field names (ADR-012): `FileName`, `SizeInBytes`, `FullPath`, `BinaryData`; EML-only `ContentId`, `Disposition`, `OriginalPath`, `ContentLocation`. Spec names and DTO names must match exactly.
- Extraction never throws on content, from either `Extract` overload. Known content failures record their warning `"<code>: <detail>"`; any other exception records `internal_error` with the exception type. Only a null argument throws `ArgumentNullException` (ADR-004, `ydk:crosscut:core/warning-format`). Tests and GoldenKit must fail on any `internal_error` warning. New warning kinds need a `ydk:warning:*` manifest.
- Metadata keys (target, ADR-016): header name trimmed and lower-cased, nothing else changed (`message-id`, `x-mailer`, `x_custom`); one key per header; the classic overlay rewrites its own key; no direction keys in the map; `from`/`to`/`cc`/`bcc`/`xfields` always present, also on `parse_failed`. Today keys fold `-` to `_` and the overlay adds punctuation-free keys (`replyto`, `messageid`).
- `xfields` (target, ADR-014): only header names starting with exactly `X_` (ordinal), `Name: value` lines joined by `
`, stored as `Metadata["xfields"]`.
- Attachments are flat and opaque. Never recurse into `message/rfc822` and never sniff file types. TNEF is the only wrapper that gets unpacked (ADR-008, ADR-013). `FullPath` is the container path (`winmail.dat/budget.xlsx`), unique per result (ADR-013).
- Golden `expected.json` values are authored from the catalog. `tools/GoldenKit gen` must never call `EmlExtractor` (ADR-007).
- Real-world samples go under gitignored `corpus/internal/`. Never commit them (licences and PII vary). Tests that use them must report Skipped (with the missing path) when the file is absent, never Passed (ADR-018; today they still pass vacuously, Stage 02).
- Committed samples (`corpus/realistic/`, ADR-018) need a provenance + license entry; expected values come from generator inputs or published contents, never from extractor output or prose.
- Load-test reports must name the CPU model; never write "Unknown processor". Refresh the perf baseline in the same commit as a runtime, MimeKit, or machine change (ADR-015).
- After any change to extraction behaviour or a MimeKit upgrade: run `GoldenKit score` and `GoldenKit probe`, and commit the refreshed `docs/golden-score.txt`.
- Keep the `System.Security.Cryptography.Pkcs` pin until MimeKit's transitive version is no longer vulnerable (ADR-009).
- YDK: in-process deps use the custom `library` schema, and diagnostics use the custom `warning` schema (ADR-010). Do not use `ext`/`error`, which are HTTP-shaped.

## Known Gotchas

- `core.autocrlf=true` on Windows checkouts turns C# raw-string EML fixtures into CRLF already. Converting with `Replace("\n", "\r\n")` then produces `\r\r\n` and breaks header parsing. Use `ReplaceLineEndings("\r\n")` (commit 679156b).
- MimeKit TNEF and legacy charsets (GB2312, Shift_JIS, Windows-125x) need `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`. The extractor's static constructor does this; test code that decodes on its own (e.g. `GoldenTests.Cases`) must register it as well.
- mbox-prefixed files (`From ` first line) must be parsed with `MimeParser(..., MimeFormat.Mbox)`. Before this, mbox `From_` lines "were breaking SpamAssassin" (commit 251657d).
- The detector sniffs only the first 16 KB. Header blocks larger than that have no real-world sample yet (edge case E27).
- Not yet aligned with ADR-004: the byte `Extract` catch filters by seven exception types, so any other type propagates, and the `MimeMessage` overload has no try/catch. Fixing this is Stage 02 work.
- `ydk` (Python) on Windows reads manifests and narratives with the cp1252 codepage. Non-Latin characters (Hebrew, CJK) in `.ydk/components/**` or `docs/specs/**` crash `ydk component validate` with `UnicodeDecodeError`. Keep those files ASCII, or set `PYTHONUTF8=1`. (Observed with ydk 1.6.0 on Windows.)
- The YDK cross-ref linker treats a `.` directly after a `ydk:type:ns/name` ID in a manifest as part of the ID. Put a space before sentence-ending punctuation. (Observed with ydk 1.6.0 on Windows.)
- `tools/GoldenKit` locates the repo root via `EmlExtraction.slnx`; its other marker, `docs/golden-1200-plan.md`, no longer exists (renamed to `docs/golden.md`).
