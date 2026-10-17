# ADR-004: Record warnings instead of throwing on malformed MIME

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
`docs/requirements.md` lists "Parse does not crash the process" under "Out of original spec (implemented anyway)". The public-mail harvest of 12,853 messages is the evidence offered for 0 throws.

## Decision
- `Extract` catches parser and decoder exceptions of the types `FormatException`, `ParseException`, `IOException`, `InvalidOperationException`, `ArgumentException`, `NotSupportedException` and `DecoderFallbackException`, then returns an empty result carrying a `parse_failed` warning.
- Each per-part failure (body decode, part decode, nested message serialisation, TNEF unpack) is caught on its own and recorded as a warning, and extraction continues with the remaining parts.
- Warnings are plain strings of the form `<code>: <detail>`.

## Alternatives
Not recorded in the repo.

## Consequences
- Callers must inspect the warnings list to tell a clean extraction from a degraded one.
- An exception type outside the caught list still propagates from the top-level parse (see Open questions).
- `docs/golden.md` ("What we will not gold") explicitly excludes warning strings from golden tests.

## Open questions
- `README.md` says extract "does not throw on malformed MIME", but the top-level catch filters by exception type, so an unlisted type would propagate. The repo does not say whether that is intended.

## Evidence
`src/EmlExtraction/EmlExtractor.cs`, `src/EmlExtraction/PartCollector.cs`, `README.md`, `docs/requirements.md`, `docs/edge-cases.md`.
