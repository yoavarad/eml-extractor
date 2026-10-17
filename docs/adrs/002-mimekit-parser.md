# ADR-002: MimeKit as the MIME parser, configured leniently

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
Extraction must cover RFC 822/2822/5322 messages and RFC 2045–2049 MIME, including RFC 2047 encoded-words, legacy charsets, TNEF (`winmail.dat`) and mbox-prefixed files (see `docs/requirements.md`, `docs/edge-cases.md`).

## Decision
- Parse with MimeKit 4.18.0 (MIT licence, per `README.md`).
- Use a cloned default parser options object with `AllowUnquotedCommasInAddresses = true`, `ParameterComplianceMode = Loose`, `Rfc2047ComplianceMode = Loose`.
- When the buffer starts with an mbox `From ` line (after an optional UTF-8 BOM), parse with `MimeParser` in `MimeFormat.Mbox`; otherwise use `MimeMessage.Load`.
- Register `CodePagesEncodingProvider` once (static constructor of the extractor) so code-page charsets decode; `docs/requirements.md` notes MimeKit's TNEF support requires it.
- Unpack TNEF with MimeKit's `TnefPart.ExtractAttachments`.

## Alternatives
Not recorded in the repo.

## Consequences
- Parser behaviour (address parsing, charset fallback, TNEF) is MimeKit's; any MimeKit upgrade must be re-scored against the golden set (ADR-007).
- Commit `251657d` records that mbox `From ` lines and missing code-page registration "were breaking SpamAssassin and TNEF" before the mbox path and provider registration were added.

## Open questions
- The repo gives no reason for choosing MimeKit beyond its licence.
- The repo gives no reason for the three lenient parser options. They may be tied to the public-mail hardening in `251657d`, but nothing says so.

## Evidence
`src/EmlExtraction/EmlExtractor.cs`, `src/EmlExtraction/PartCollector.cs`, `src/EmlExtraction/EmlExtraction.csproj`, `README.md`, commit `251657d`.
