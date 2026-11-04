# ADR-026: Return a partial result instead of failing the whole extraction

## Status
Implemented (plan `docs/plans/stage-02-partial-batch.yaml`). Owner decision 2026-11-04 (the rule and the four answers below). Two further owner decisions of the same date: after a body-stage bug the text body part also comes back as an attachment (confirmed, intended: content is not lost, the caller still gets the bytes even though body text extraction failed); and a failed metadata key keeps the raw header value already mapped. Supersedes ADR-023 in part: an unexpected exception (library bug) inside `Extract` no longer throws `EmlExtractionException`; it returns a partial result with an `internal_error` warning. Restores the ADR-004 per-stage partial-result defaults and `internal_error` warning, and adds per-field metadata recovery. Amends ADR-021 (outcome and counter semantics) and ADR-012 (error model). Misuse, oversized input and detection are unchanged from ADR-023.

## Context
ADR-023 made a library bug in any extraction stage throw `EmlExtractionException`. The whole call then returns nothing, even when the parse and the other stages worked.

The metadata stage is the sharpest case. `EmlExtractor.Extract(MimeMessage, ...)` wraps all of `HeaderMapper.Map` in one stage guard, and that guard has no content catch: any exception while mapping one header, content or bug, is wrapped and thrown. One bad header loses the subject, the addresses, the body and every attachment.

The downstream indexer extracts in batches. A thrown call gives it no text at all for that message.

## Decision
Per project owner, 2026-11-04: "When failing to parse a field the extraction should always aim to extract as many fields/text/attachments as possible and never fail the whole extraction process. A partial result is much more valuable than no result."

The owner's answers:
1. An unexpected exception (library bug) in a stage returns a partial result. The failed stage or field gets its empty default and a warning. Tests and GoldenKit fail on that warning, so bugs still surface in CI. This supersedes the "bugs throw `EmlExtractionException`" part of ADR-023.
2. Metadata recovery is per field. Each header key, participant list, date, subject, message-id, references, xfields and so on is guarded separately. One bad field loses only that key, or falls back to its fixed empty default for a fixed key, with a warning. Superseded by the owner decision 2026-11-04: a failed key keeps the raw header value already mapped, only the derived value is skipped (see Details).
3. A whole-message parse failure (MimeKit cannot parse) keeps today's behaviour: an empty result with `parse_failed`. There is no raw salvage. A non-content (bug) exception in the parse stage also returns the empty result, with an `internal_error` warning, instead of throwing.
4. Misuse still throws: null or invalid arguments, and `maxInputBytes <= 0`. Oversized input still throws `EmlInputTooLargeException`. Exceptions from host telemetry listeners still propagate unchanged. `OutOfMemoryException` still propagates.

### Details
- **Content failure in one metadata field.** One of the seven content exception types (`EmlExtractor.IsContentFailure`: `FormatException`, `ParseException`, `IOException`, `InvalidOperationException`, `ArgumentException`, `NotSupportedException`, `DecoderFallbackException`) while mapping one field records `metadata_field_failed: <key>: <ExceptionType>` (`ydk:warning:extraction/metadata-field-failed`). The raw header value already mapped for that key stays (owner decision 2026-11-04); only the derived value is skipped. A key with no raw value mapped stays absent; a fixed key with none (`from`, `to`, `cc`, `bcc`, `xfields`) stays `""` (ADR-016). The eight participant lists (derived) stay empty (ADR-025). Raw headers mapped before the failure (including an `X_` header's line in `xfields`) are kept.
- **Bug in a stage or field.** Any other exception records `internal_error: <stage>[.<key>]: <ExceptionType>` (`ydk:warning:extraction/internal-error`). Stage names are those of ADR-020 / ADR-023: `parse`, `metadata`, `body`, `attachments`. A field-level bug in metadata uses `metadata.<key>`, e.g. `internal_error: metadata.date: NullReferenceException`.
- **No message content in these warnings.** They carry the exception type name only, never the exception message (MimeKit messages can quote input). The `<key>` is the result's own metadata key.
- **Defaults.** A failed stage contributes the ADR-004 stage default: metadata -> only the fixed keys, each `""`, and empty participants; body -> `MainContent` and `MainContentMimeType` null, both directions `ltr`; attachments -> no attachments; parse -> the empty `parse_failed` result shape. Stages that succeeded keep their output. Later stages still run: body runs with the metadata it got; attachments runs after a body failure. After a body failure no body part is known, so the text part that would have been the body can appear as an attachment (confirmed by the project owner 2026-11-04: content is not lost, the caller still gets the bytes even though body text extraction failed).
- **Per-field guards.** Each `Metadata` key and each participant list is built in its own guard. A bug outside any field (for example, enumerating the header list itself) is caught by the metadata stage guard, which gives the stage default and `internal_error: metadata: <ExceptionType>`.
- **Attachments, per part.** The three per-part catch sites in `PartCollector` (`part_decode_failed`, `rfc822_extract_failed`, `tnef_extract_failed`) also catch a bug for that one part: the part is skipped (TNEF keeps its raw-part fallback) and `internal_error: attachments: <ExceptionType>` is recorded. Other parts are still collected. The stage guard stays as the backstop for a bug outside a part. The catch sites already existed. The body read catch site (`body_decode_failed`) does the same for stage `body`.
- **`EmlExtractionException` stays only for detection.** `EmlDetector.Detect` and `IsEml` still wrap a bug with stage `detect`; that is unchanged. `Extract` no longer throws it. The type, its `Stage` values and its message format are unchanged, so no public API is removed.
- **Telemetry (ADR-021).** No instrument is renamed. Outcome `internal_error` now means "a result was returned with an `internal_error` warning"; it takes precedence over `warnings` and `parse_failed`. `eml_extraction.extract.internal_errors` gets +1 per `internal_error` warning, tagged `eml_extraction.stage` (the stage part only, never the key) and `error.type`. `eml_extraction.warning.code` gains `internal_error` and `metadata_field_failed`. Only a stage-level guard bug sets the stage activity to status Error with `error.type`; per-part bugs, a body-read bug and per-field metadata bugs only increment the counter and set outcome `internal_error`; the parent `eml_extraction.extract` activity is not set to Error, because the call returned a result.
- **CI gate.** Tests, the GoldenKit score and the harvest probe fail on any `internal_error` warning, as under ADR-004, and still fail on any exception. `metadata_field_failed` is a content warning and does not fail a case by itself.

## Rationale
Per project owner, 2026-11-04: a partial result is much more valuable than no result. The headers, text and attachments that were read correctly are what the indexer needs. A separate `internal_error` code keeps the bug signal that ADR-023 wanted: CI fails on it, and telemetry counts it by stage.

## Alternatives
- Keep ADR-023 (bugs throw). Rejected by the owner: one bad field loses the whole message.
- Per-stage recovery only (ADR-004 as it was). Not chosen: the owner asked for per-field metadata recovery, so one bad header loses one key, not all of them.
- Raw salvage when MimeKit cannot parse the message (scan header lines by hand). Rejected by the owner: a parse failure stays an empty `parse_failed` result.
- Also return bugs through an `Exception` property on the result. Not chosen: it adds public surface for no current consumer; the warning code and the telemetry counter already carry the signal.

## Consequences
- The code follows this ADR: `Extract` never throws `EmlExtractionException`. The body-as-attachment behaviour is confirmed by the project owner 2026-11-04: content is not lost, the caller still gets the bytes even though body text extraction failed.
- Callers again inspect `Warnings`: `internal_error` means a library bug, `metadata_field_failed` means one header could not be read. A result is always returned unless the call was misused, the input was over the limit, or memory ran out.
- `Extract` drops `<exception cref="EmlExtractionException">` from its XML docs; `EmlDetector` keeps it.
- ADR-021: `internal_errors` counts warnings, not throws. The outcome value set is unchanged.
- ADR-012: the EML error model is again "warnings for content and bugs, exceptions for misuse and limits". OLE still throws for bugs (`docs/team-questions.md` Q1).
- Breaking change for callers that catch `EmlExtractionException` from `Extract`. Acceptable: there are no consumers yet.
- Manifests: `ydk:warning:extraction/internal-error` (recreated), `ydk:warning:extraction/metadata-field-failed` (new), `ydk:req:extraction/no-throw-on-malformed`, `ydk:contract:api/eml-extractor`, `ydk:entity:extraction/EmlExtractionException`, `ydk:nfr:reliability/real-world-harvest-no-throw`, `ydk:crosscut:core/warning-format`, `ydk:req:observability/runtime-telemetry`.

## Evidence
ADR-004, ADR-012, ADR-016, ADR-020, ADR-021, ADR-023, ADR-025. `src/EmlExtraction/Logics/EmlExtractor.cs` (metadata stage guard with no content catch; `IsContentFailure`; `Wrap`), `src/EmlExtraction/Logics/HeaderMapper.cs` (`Map` builds every key in one pass), `src/EmlExtraction/Logics/PartCollector.cs` (per-part content catch sites). Project owner decision, 2026-11-04.
