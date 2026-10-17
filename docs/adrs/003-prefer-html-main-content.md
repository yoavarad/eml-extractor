# ADR-003: Prefer HTML as main content in multipart/alternative

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
The original requirement asks for "the main body of the email". Many messages carry both a plain-text and an HTML rendering inside `multipart/alternative`.

## Decision
- Inside `multipart/alternative`, select HTML first, then plain text, then any other selectable text part (e.g. `text/calendar`).
- In `multipart/related`, select from the root part first, then the first selectable child.
- In other multiparts, take the first selectable part in order.
- A text part flagged as an attachment is never the main content.
- HTML is returned as markup (not converted to text); `dir` attributes and `cid:` references remain in it.
- The sibling renderings of the chosen alternative that were not selected are not reported as attachments.

## Alternatives
Not recorded in the repo.

## Consequences
Callers receive HTML for most modern mail and must strip markup themselves if they need plain text. The result carries the selected part's MIME type next to the content.

## Rationale
Per project owner, 2026-10-17: extracted content feeds a downstream indexer / LLM pipeline, and HTML keeps the document structure (headings, lists, tables, links) that the plain-text rendering loses.

## Evidence
`README.md` ("Content"), `docs/requirements.md` ("Content"), `src/EmlExtraction/BodySelector.cs`, `src/EmlExtraction/PartCollector.cs`, test `ExtractionTests.PrefersHtmlOverPlain`, golden `E03-html-alt`.
