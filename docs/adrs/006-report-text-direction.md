# ADR-006: Report subject and content text direction as ltr/rtl

## Status
Accepted (recorded retroactively, Stage 01 Rewrite). Amended by ADR-016: direction values are reported only on the result properties, no longer as metadata keys (target, Stage 02).

## Context
The library must support Hebrew and Arabic (Correctness A in `docs/requirements.md`). Commit `c3b39bb` adds direction so callers can rely on "subject/content direction from HTML dir or first-strong Unicode".

## Decision
- Report two values, each exactly `ltr` or `rtl`: one for the subject, one for the main content. Both appear as result properties and as the metadata keys `subject_direction` / `content_direction`.
- HTML content: use `<html dir=…>` first, then `<body dir=…>`, then the first CSS `direction:` declaration. If none of these is present, apply first-strong to the text with tags stripped.
- Otherwise (plain text, and always for the subject): first-strong per UAX #9. The explicit bidi controls RLM/RLE/RLO/RLI give `rtl` and LRM/LRE/LRO/LRI give `ltr`. A letter from the Hebrew/Arabic/Syriac/Thaana/NKo and related blocks gives `rtl`; any other letter gives `ltr`.
- Empty or missing text gives `ltr`.
- Content is never rewritten: `dir` markup and bidi marks are preserved.

## Alternatives
Not recorded in the repo.

## Consequences
Direction is a paragraph-level hint, not a per-run bidi analysis. A subject that starts with a Latin word but is mostly Hebrew reports `ltr` (test `FirstStrongLetterWinsInMixedSubject`).

## Rationale
Per project owner, 2026-10-17: extracted content feeds a downstream indexer / LLM pipeline. Reporting direction lets those consumers handle right-to-left text (e.g. Hebrew) correctly without running their own detection.

## Evidence
`src/EmlExtraction/TextDirection.cs`, `src/EmlExtraction/EmlExtractor.cs`, `README.md`, commit `c3b39bb`, `tests/EmlExtraction.Tests/ExoticLanguageTests.cs`.
