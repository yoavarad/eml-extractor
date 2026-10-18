# ADR-014: xfields joins literal `X_` headers and lives in the metadata

## Status
Accepted (per project owner, 2026-10-17). Implemented. Supersedes the X-/X_ matching rule in `ydk:req:metadata/xfields` and `docs/requirements.md`. Amended by ADR-023 (2026-10-18): oversized input throws, so there is no `input_too_large` result.

## Context
The brief says: all metadata fields that start with `X_` (capital X, then underscore) are concatenated into one long string in a field named `xfields`.

The code instead:
- matches `X-` or `X_`, case-insensitive, because `X-` is the usual RFC custom-header form;
- exposes the string as a separate result property `XFields`, not as a metadata field.

The brief is literal and the team's internal mail may use `X_` headers on purpose. Widening the rule changes what lands in the field the factory reads.

## Decision
Per project owner, 2026-10-17:
- A header is an xfields header when its field name starts with exactly `X_`: capital `X` (U+0058) followed by `_` (U+005F), compared ordinally. `x_foo`, `X-Foo`, and `x-foo` do not match.
- `xfields` is a metadata field: `Metadata["xfields"]`. It is always present; the empty string when no header matches, including on `parse_failed` and `input_too_large`.
- Format (unchanged from today): one line per matching header, `<original field name>: <decoded, unfolded value>`, in header order, joined by `\n` (U+000A), no trailing newline. A repeated header gives one line per occurrence.
- The separate `XFields` result property is removed, so the value has one home.
- `X-` headers are not in `xfields`. Like every header, they stay in the general metadata under their own key.
- The X-Foo / X_Foo key collision is fixed by the canonical keying rule of ADR-016: `-` and `_` are no longer folded together.

## Alternatives
- Keep matching `X-` and `X_` (status quo). Rejected: diverges from the brief, and whether the team's mail uses `X_` deliberately is unknown (`docs/team-questions.md`).
- Case-insensitive `X_` (`x_` too). Rejected: the brief says capital X.
- Keep `XFields` as a property and also add the metadata key. Rejected: two copies of one value.
- Change the separator (for example `; ` or CRLF). Rejected: header values may contain `; `, while unfolded values never contain `\n`, so `\n` splits back losslessly.

## Consequences
- Real-world mail with only `X-` headers yields `xfields == ""`. The Exchange header test that asserts `X-MS-...` lines in xfields changes to assert them in general metadata.
- Golden x-headers cases and seed E10 are regenerated from the catalog in Stage 02 (ADR-007 still applies).
- Breaking change, covered by ADR-012.

## Evidence
Team brief (`docs/requirements.md`, "X_ fields concatenated into xfields"), `src/EmlExtraction/HeaderMapper.cs` (`IsXHeader`). Project owner decision, 2026-10-17.
