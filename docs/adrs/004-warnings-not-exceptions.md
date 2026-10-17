# ADR-004: Record warnings instead of throwing on malformed MIME

## Status
Accepted (recorded retroactively, Stage 01 Rewrite). Throw contract decided by the project owner on 2026-10-17. Implementation of that contract is pending, tracked as Stage 02 work.

## Context
`docs/requirements.md` lists "Parse does not crash the process" under "Out of original spec (implemented anyway)". The public-mail harvest of 12,853 messages is the evidence offered for 0 throws.

`README.md` says extract "does not throw on malformed MIME". The code does not fully match that. The byte overload's top-level catch filters by exception type, so an unlisted type propagates, and the `MimeMessage` overload has no top-level try/catch.

## Decision
Per project owner, 2026-10-17:
- Content-driven failures never throw. Both `Extract` overloads (`ReadOnlyMemory<byte>` and `MimeMessage`) behave the same.
- Known content failures map to their existing warnings: `parse_failed` (whole message, empty result), and `body_decode_failed`, `part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed` (one part; extraction continues).
- Any other, unexpected exception is caught and recorded as a new `internal_error` warning that names the stage and the exception type (`ydk:warning:extraction/internal-error`).
- After parsing, each extraction stage is guarded separately: header/metadata mapping, body selection plus direction, and attachment collection. A stage that throws unexpectedly contributes its empty default and adds one `internal_error` warning; stages that succeeded keep their output.
- A parse failure (MimeKit cannot parse the message at all) still returns an empty result with `parse_failed`.
- Programmer misuse still throws: a null argument raises `ArgumentNullException`.
- Tests and GoldenKit fail on any `internal_error` warning, so library bugs surface in CI instead of hiding.
- Warnings are plain strings of the form `<code>: <detail>`.

## Rationale
Per project owner, 2026-10-17: the downstream indexer / LLM pipeline extracts messages in batches. One bad message must not abort a batch. A separate `internal_error` code keeps the bug signal that a catch-everything approach would lose. A partial result beats nothing for that pipeline: for example, the headers survive a crash in body selection.

## Alternatives
- Plain catch-everything with no bug signal. Rejected: it hides library defects behind ordinary warnings.
- Catch only the 7 known exception types and rethrow the rest (current code). Rejected: one bad message aborts a batch in the downstream indexer / LLM pipeline.
- Return an empty result on `internal_error`, like `parse_failed`. Rejected: it throws away the output of stages that succeeded, and a partial result is more useful downstream.

## Consequences
- Callers must inspect the warnings list to tell a clean extraction from a degraded one.
- An `internal_error` warning means a library bug, not bad input.
- `docs/golden.md` ("What we will not gold") excludes warning text from golden tests. Tests assert on code prefixes only.
- Current code gap (Stage 02 work): the byte overload catches only `FormatException`, `ParseException`, `IOException`, `InvalidOperationException`, `ArgumentException`, `NotSupportedException` and `DecoderFallbackException`, and any other type propagates. The `MimeMessage` overload has no top-level try/catch. No `internal_error` warning exists in code, and tests and GoldenKit do not check for it yet.

## Evidence
`src/EmlExtraction/EmlExtractor.cs`, `src/EmlExtraction/PartCollector.cs`, `README.md`, `docs/requirements.md`, `docs/edge-cases.md`. Project owner decision, 2026-10-17.
