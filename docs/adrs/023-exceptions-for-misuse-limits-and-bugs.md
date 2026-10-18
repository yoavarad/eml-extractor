# ADR-023: Throw for misuse, limits, and library bugs; warn for content

## Status
Accepted by owner 2026-10-18. Implemented (#104, #105, #106; plan `docs/plans/stage-02-throw-batch.yaml`). Supersedes ADR-004 in part (the "never throws" contract, the `internal_error` warning, and the per-stage partial-result defaults for bugs). Amends ADR-012 (error model), ADR-021 (outcome and counter semantics), ADR-020 (stage names are no longer "guarded" stages), and ADR-005 / ADR-014 / ADR-016 (no `input_too_large` result exists any more).

## Context
ADR-004 made extraction "never throw": content failures became warnings, and any other exception became an `internal_error` warning with a partial result (#37). ADR-012 kept that as the EML error model. The size guard (#38) returned an empty result with an `input_too_large` warning.

That contract hides problems the caller should see:
- A library bug turns into a warning string. A caller that does not parse warning codes treats a broken result as a degraded but valid one, and the stack trace is lost.
- An oversized input returns a valid-looking empty result. The caller must check a warning code to learn that nothing was extracted.
- It diverges from `yoavarad/ole-extractor`, which throws typed exceptions (ADR-012, `docs/team-questions.md` Q1).

Bad email content is a different matter. Real mail is often malformed (bad charset, undecodable part, broken MIME structure that still yields partial data), and a partial result is the useful outcome for the downstream indexer.

## Decision
Per project owner, 2026-10-18: throw relevant, informative errors when the call cannot do its job; keep warnings for content.

### What throws
| Case | Exception | Wrapped? |
| --- | --- | --- |
| Null argument (`request`, `message`) | `ArgumentNullException` | no |
| Invalid argument value | `ArgumentException` | no |
| Invalid option, e.g. `maxInputBytes <= 0` | `ArgumentOutOfRangeException` | no |
| Input longer than `maxInputBytes` | `EmlInputTooLargeException` (new) | no |
| Unexpected exception inside extraction or detection (library bug) | `EmlExtractionException` (new), original as `InnerException` | yes |
| Exception thrown by a host callback (e.g. a `MeterListener` or `ActivityListener` callback) | the host's exception | no, propagates unchanged |

- Every message names the parameter and the bad value or limit, e.g. `maxInputBytes must be greater than 0. Actual value: 0. (Parameter 'maxInputBytes')`.
- Messages never quote message content (subject, addresses, body, file names). Sizes, limits, parameter names, stage names and exception type names are allowed.
- Misuse and limit checks run before any parsing.

### New exception types
Owner-approved 2026-10-18: names, base types, `sealed`, and properties as below.
- `EmlInputTooLargeException : ArgumentException` (sealed). Properties `long InputLength` and `long MaxInputBytes`. `ParamName` is `request` (the bytes overload forwards to the request overload). Message: `Input is <InputLength> bytes, which exceeds maxInputBytes <MaxInputBytes>.` A length equal to the limit is parsed. Deriving from `ArgumentException` lets callers that already catch argument errors handle it, and it is the caller's input that is wrong. Manifest: `ydk:entity:extraction/EmlInputTooLargeException`.
- `EmlExtractionException : Exception` (sealed). Property `string Stage`, one of `request`, `detect`, `parse`, `metadata`, `body`, `attachments` (the ADR-020 stage names; ADR-021 keeps the last four for its `eml_extraction.stage` tag). `InnerException` is the original exception. Message: `EML extraction failed in stage '<Stage>': <InnerTypeFullName>.` The inner exception's message is not copied into the outer message (MimeKit messages can quote input); it stays on `InnerException`. Manifest: `ydk:entity:extraction/EmlExtractionException`.

### Wrap or propagate unexpected exceptions
Owner-approved 2026-10-18: wrap, in `EmlExtractionException`, and never wrap the cases in the table marked "no".
- The stage name is the most useful fact for a bug report, and only the library knows it.
- One type tells the caller "library bug" apart from misuse (`ArgumentException` family) without a list of BCL and MimeKit types.
- Nothing is lost: the original exception, type and stack trace are on `InnerException`.
- `OutOfMemoryException` is not wrapped; it is an environment condition, not a bug in a stage.

### What stays a warning
- Malformed-but-readable content keeps its warning and partial result: `parse_failed`, `body_decode_failed`, `part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed` (`ydk:crosscut:core/warning-format`).
- Each content catch site handles only content exception types: `FormatException`, `ParseException`, `IOException`, `InvalidOperationException`, `ArgumentException`, `NotSupportedException`, `DecoderFallbackException` (the list `parse_failed` already uses). Any other type is a bug and is wrapped as above.
- `internal_error` and `input_too_large` warnings are removed. No result carries them.

### Detection
`EmlDetector.Detect` and `IsEml` still return `IsSupported = false` / `false` for input that is not a message: that is a result, not an error. The span overloads have no argument that can be null, so in practice they throw only on a library bug, wrapped with stage `detect`.

## Consequences
- Callers that process batches catch per item. Malformed mail still returns a result, so a batch is lost only to a bug, an oversized input, or misuse, each of which the caller should see.
- `Warnings` means only "content was degraded". A clean warnings list on a returned result means nothing went wrong.
- Tests and GoldenKit fail when extraction throws on a golden or realistic input (they no longer look for an `internal_error` warning). The harvest NFR counts exceptions on real-world content.
- ADR-021: `eml_extraction.extract.internal_errors` counts calls that threw `EmlExtractionException` (tags `eml_extraction.stage`, `error.type` = the inner exception's type); `eml_extraction.extract.input_too_large` counts calls that threw `EmlInputTooLargeException`. `eml_extraction.outcome` values: `clean`, `warnings`, `parse_failed`, `input_too_large`, `internal_error` (the last two now mean the call threw). `eml_extraction.warning.code` no longer contains `internal_error` or `input_too_large`. On a wrapped exception the stage activity gets status Error and `error.type`. No instrument is renamed, so #71 and #72 change only where they record.
- ADR-012's error model now matches OLE's direction (typed exceptions). `docs/team-questions.md` Q1 is narrowed to exception naming and base types.
- ADR-005 / ADR-014 / ADR-016: the fixed metadata keys are present on every returned result, including `parse_failed`. There is no `input_too_large` result.
- Breaking change. Acceptable: there are no consumers yet.
- Manifests: `ydk:req:extraction/no-throw-on-malformed` (narrowed to content), `ydk:req:extraction/max-input-size`, `ydk:nfr:reliability/real-world-harvest-no-throw`, `ydk:warning:extraction/internal-error` and `ydk:warning:extraction/input-too-large` (deleted, #105 and #106), `ydk:contract:api/eml-extractor`, `ydk:contract:api/eml-detector`, `ydk:entity:extraction/EmlExtractionException`, `ydk:entity:extraction/EmlInputTooLargeException`.

## Alternatives
- Keep ADR-004 (never throw, `internal_error` warning). Rejected by the owner: it hides bugs and oversized inputs behind warning strings.
- Let unexpected exceptions propagate unwrapped. Simpler and keeps the original type at the top. Rejected (owner, 2026-10-18): the caller loses the stage name and needs its own list of "bug" types to tell bugs from misuse.
- Return a failure result with an `Error` slot (OLE's batch shape). Rejected: ADR-012 has no batch API, and a result that may be an error is the same problem as a warning code.
- `EmlInputTooLargeException : InvalidOperationException` or `: EmlExtractionException`. Not chosen: the input is the caller's argument, so `ArgumentException` is the closest BCL meaning, and mixing it with the bug type would blur the two.
- Strict mode flag (throw only when asked). Rejected: two behaviours to test and document for no current consumer.

## Evidence
ADR-004, ADR-012, ADR-020, ADR-021, `src/EmlExtraction/EmlExtractor.cs` (per-stage guards from #37, size guard from #38), `src/EmlExtraction/PartCollector.cs` (content catch sites), `docs/team-questions.md` Q1. Project owner decision, 2026-10-18.
