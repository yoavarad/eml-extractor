# Extraction - metadata, main content, direction

The extractor, [ydk:contract:api/eml-extractor], turns one request into one [ydk:entity:extraction/EmlExtractionResult]. This page covers everything in that result except attachments, which have their own page ([attachments.md](attachments.md)).

## The request

Per the shared team contract (ADR-012), the caller passes a request, [ydk:entity:extraction/EmlExtractionRequest], holding the message bytes and, optionally, the caller's name for the file. The name is for the caller's own tracing; it never changes the result. A request leaves room to add other input sources later without breaking callers. Per the project owner (2026-10-18), the call that takes plain bytes stays as a convenience: it only wraps the bytes in a request and forwards, so both calls give the same result. There is no batch call; the factory runs messages in parallel itself. The parsed-message entry point that exposed parser-library types becomes internal. This is target behaviour: today the public methods take raw bytes or an already-parsed message object, and the change lands in Stage 02. It is a breaking change, made now while the library has no consumers.

## Input size

Before parsing, the extractor compares the input length with a limit the host can set when it creates the extractor ([ydk:req:extraction/max-input-size]). The default (500 MiB) was set by the project owner, not derived from measurements; the first large-file measurement (peak memory 1.3-4.2 times the input, taken at 150 MiB) puts one extraction near 2-2.5 GB at that size, and the host can always change it. A longer input is not parsed: per the project owner (2026-10-18, ADR-023) extraction throws [ydk:entity:extraction/EmlInputTooLargeException], which carries the input size and the limit. This keeps one oversized file from exhausting the host's memory, since every attachment is held in memory. The memory each message needs is measured, not guessed ([ydk:nfr:performance/memory-footprint]).

## Parsing

Parsing is delegated to the parser library, [ydk:library:deps/mimekit] (ADR-002). The parser runs in its lenient modes for addresses, header parameters, and encoded words. The repo does not record why these modes were chosen (open question in ADR-002). When the buffer starts with an mbox separator line, the parser reads it as an mbox stream and keeps only the first message ([ydk:req:extraction/mbox-from-line]). Commit 251657d records that mbox separator lines and missing code-page registration were breaking extraction on the SpamAssassin corpus and on Outlook TNEF wrappers.

## Metadata

The metadata map, [ydk:entity:extraction/Metadata], is built in three passes.

1. **Every header.** Every top-level header is copied, decoded and unfolded, under one key derived from its name. Repeated headers are kept, not dropped. This meets the original requirement to capture "as many headers as possible" ([ydk:req:metadata/all-headers]).
2. **Classic overlay.** The usual envelope headers (addresses, date, message identifiers) are rewritten in place from the parser's structured view, under the same key, so each header has one key only ([ydk:req:metadata/classic-fields]). The four address keys are always present, so callers can index them without a missing-key check. When a header is absent, its key holds an empty string (ADR-005).
3. **Custom headers.** The joined custom-header string described below is added under its own fixed key.

The keying rule is in [ydk:req:metadata/canonical-keys] (ADR-016). A key is the header name in lower case and nothing else, so two headers that differ only by a dash versus an underscore no longer merge. The text direction values are not metadata; they are reported only beside it on the result. The four address keys and the custom-header key are present in every result, including one for a message that could not be parsed ([ydk:warning:extraction/parse-failed]).

Today the code still derives keys by folding dashes into underscores, writes a second key for each classic header, puts the two direction values in the map, and returns an empty map on parse failure. Closing these gaps is Stage 02 work. The exact key names and value formats are in [ydk:entity:extraction/Metadata].

## Custom headers

The original specification asks for all fields starting with a capital X and an underscore to be joined into one long string in a metadata field. Per the project owner (ADR-014), the rule is applied literally: only header names that begin with exactly those two characters count. Headers that begin with an X and a dash, the usual internet form, are not part of the string; they stay in the general metadata under their own keys. Each matching header becomes one "name: value" line in header order, and the lines are joined into one string held in the metadata map ([ydk:req:metadata/xfields]). Authentication headers such as DKIM signatures never appear in it.

Today the code also matches the dash form, ignores case, and returns the string as a separate property instead of a metadata field. This changes in Stage 02. Whether the team's internal mail uses the underscore form is an open question in [docs/team-questions.md](../team-questions.md).

## Main content

One body part is chosen as the main content ([ydk:req:content/main-content-selection], ADR-003). HTML is preferred because the content feeds a downstream indexer and language-model pipeline, and the markup keeps the document structure that plain text loses.

- When a message offers alternative renderings, HTML wins over plain text, and plain text wins over anything else.
- In a related bundle (HTML plus its inline images), the root part is used.
- In a mixed message, the first part that qualifies is used.
- A text part marked as an attachment is never the body. A named calendar file therefore stays an attachment.

HTML is returned as markup with its inline-image references and direction attributes intact. The result also reports the chosen part's media type next to it, so callers know whether they got markup; both are empty when no body was chosen. If the chosen part cannot be decoded, the body is empty and [ydk:warning:extraction/body-decode-failed] is recorded.

## Exotic languages and emoji

The original correctness requirement covers emoji and exotic languages. Text comes back exactly as the sender wrote it: no normalisation and no stripping, whatever transfer or header encoding carried it ([ydk:req:content/unicode-fidelity]). Besides the seven scripts already tested, the requirement adds the emoji forms that break naive decoders: joined family sequences, skin-tone modifiers, national flags, keycaps, and letters with combining marks. Each is checked for exact equality. The added cases are target behaviour, tested from Stage 02.

## Text direction

Hebrew and Arabic support is part of the original correctness requirement. Commit c3b39bb adds a direction value for the subject and for the body, left-to-right or right-to-left, so the downstream indexer and language-model consumers can handle right-to-left text such as Hebrew correctly ([ydk:req:content/text-direction], ADR-006).

- For an HTML body, a direction the author declared in the markup wins over the letters.
- Otherwise, and always for the subject, the first strongly-directional character decides, following the Unicode bidi algorithm's first-strong rule.
- Empty text defaults to left-to-right.

The precedence order and the exact character ranges are in [ydk:req:content/text-direction].

Direction is reported, never applied. The text keeps its bidi marks, and the HTML keeps its attributes.

## Failure handling

Malformed but readable content never makes extraction throw ([ydk:req:extraction/no-throw-on-malformed], ADR-004 as narrowed by ADR-023). Real mail is often broken, and a partial result is what the downstream indexer and language-model pipeline needs.

A whole-message parse failure returns an empty result with [ydk:warning:extraction/parse-failed]. A failure in one part affects that part only and is recorded as a warning while the rest of the message is still extracted. A result that could not be parsed still carries the four address keys and the custom-header key, each holding an empty string, so callers never miss a key ([ydk:req:metadata/canonical-keys]).

Everything that is not a content problem throws (per the project owner, 2026-10-18, ADR-023, which supersedes the "never throws" part of ADR-004 and amends ADR-012). Programmer misuse, such as passing no request at all or an invalid size limit, throws an argument error naming the parameter and the bad value. An input over the size limit throws [ydk:entity:extraction/EmlInputTooLargeException]. Any other, unexpected failure is a library bug: it throws [ydk:entity:extraction/EmlExtractionException], which names the stage that failed (request, parse, headers, body with direction, or attachments) and keeps the original failure attached. A failure inside the host's own telemetry listener passes through unchanged. A caller processing a batch catches per message; malformed mail still returns a result, so only a bug, an oversized input, or misuse stops one call. The tests and the golden scorer fail whenever extraction throws on a golden or sample input.

The oversized-input exception is implemented. The code does not yet do the rest: today a library bug in one stage is recorded as [ydk:warning:extraction/internal-error] with that stage left empty. Switching it to an exception is Stage 02 work (docs/plans/stage-02-throw-batch.yaml). The OLE extractor already throws typed exceptions, so the two error models now point the same way ([docs/team-questions.md](../team-questions.md)). The warning and exception rules are defined once in [cross-cutting.md](cross-cutting.md).

## Evidence

[ydk:test:extraction/extraction.unit] covers metadata, custom headers, body choice, mbox input, folded headers, legacy charsets, and corrupt encodings. [ydk:test:extraction/exotic-language.unit] covers round-trips in seven scripts plus emoji, and every direction rule.
