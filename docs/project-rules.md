# Project Rules

Conventions, preferences, and domain knowledge.

## Product Context

We are building an in-process C# / .NET 10 class library that decides whether a byte buffer is an EML message (RFC 822/2822/5322 + RFC 2045-2049 MIME). For a message, it extracts the header metadata (every header, the classic fields under fixed keys, and custom X-/X_ headers joined into `xfields`), one main body (HTML preferred) with its ltr/rtl direction, and every attachment as name, decoded size, optional raw path, and decoded bytes. It does no file-type detection and no recursion into nested content. The consumer is a downstream file-processing pipeline (the "factory" of the original Hebrew spec, out of scope). Specs: `docs/specs/overview.md`. Original spec map: `docs/requirements.md`.

## Conventions

- Stack is fixed (ADR-001): .NET SDK pinned by `global.json`; build with `TreatWarningsAsErrors=true`; `dotnet format --verify-no-changes` must pass; tests use xUnit.
- Public surface is only `EmlDetector.IsEml`, `EmlExtractor.Extract` (byte and `MimeMessage` overloads), and the result records in `Models.cs`. Keep helpers `internal`.
- Extraction never throws for handled failures. Record a warning `"<code>: <detail>"` instead (ADR-004, `ydk:crosscut:core/warning-format`). New warning kinds need a `ydk:warning:*` manifest.
- Metadata keys: header name lower-cased with `-` replaced by `_`. Classic overlay keys have no punctuation (`replyto`, `messageid`). `from`/`to`/`cc`/`bcc` are always present (ADR-005).
- Attachments are flat and opaque. Never recurse into `message/rfc822` and never sniff file types. TNEF is the only wrapper that gets unpacked (ADR-008).
- Golden `expected.json` values are authored from the catalog. `tools/GoldenKit gen` must never call `EmlExtractor` (ADR-007).
- Real-world samples go under gitignored `corpus/internal/`. Never commit them (licences and PII vary). Tests that use them must pass vacuously when the file is absent.
- After any change to extraction behaviour or a MimeKit upgrade: run `GoldenKit score` and `GoldenKit probe`, and commit the refreshed `docs/golden-score.txt`.
- Keep the `System.Security.Cryptography.Pkcs` pin until MimeKit's transitive version is no longer vulnerable (ADR-009).
- YDK: in-process deps use the custom `library` schema, and diagnostics use the custom `warning` schema (ADR-010). Do not use `ext`/`error`, which are HTTP-shaped.

## Known Gotchas

- `core.autocrlf=true` on Windows checkouts turns C# raw-string EML fixtures into CRLF already. Converting with `Replace("\n", "\r\n")` then produces `\r\r\n` and breaks header parsing. Use `ReplaceLineEndings("\r\n")` (commit 679156b).
- MimeKit TNEF and legacy charsets (GB2312, Shift_JIS, Windows-125x) need `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`. The extractor's static constructor does this; test code that decodes on its own (e.g. `GoldenTests.Cases`) must register it as well.
- mbox-prefixed files (`From ` first line) must be parsed with `MimeParser(..., MimeFormat.Mbox)`. Before this, mbox `From_` lines "were breaking SpamAssassin" (commit 251657d).
- The detector sniffs only the first 16 KB. Header blocks larger than that have no real-world sample yet (edge case E27).
- The top-level `Extract` catch filters by exception type. An exception type outside that list propagates despite the "does not throw" wording in the README.
- `ydk` (Python) on Windows reads manifests and narratives with the cp1252 codepage. Non-Latin characters (Hebrew, CJK) in `.ydk/components/**` or `docs/specs/**` crash `ydk component validate` with `UnicodeDecodeError`. Keep those files ASCII, or set `PYTHONUTF8=1`.
- The YDK cross-ref linker treats a `.` directly after a `ydk:type:ns/name` ID in a manifest as part of the ID. Put a space before sentence-ending punctuation.
- `tools/GoldenKit` locates the repo root via `EmlExtraction.slnx`; its other marker, `docs/golden-1200-plan.md`, no longer exists (renamed to `docs/golden.md`).
