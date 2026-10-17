# Extraction - metadata, main content, direction

The extractor, [ydk:contract:api/eml-extractor], turns one message into one [ydk:entity:extraction/EmlExtractionResult]. This page covers everything in that result except attachments, which have their own page ([attachments.md](attachments.md)).

## Parsing

Parsing is delegated to MimeKit, [ydk:library:deps/mimekit] (ADR-002). The parser runs in its lenient modes for addresses, header parameters, and encoded words. The repo does not record why these modes were chosen (open question in ADR-002). When the buffer starts with an mbox separator line, the parser reads it as an mbox stream and keeps only the first message ([ydk:req:extraction/mbox-from-line]). Commit 251657d records that mbox separator lines and missing code-page registration were breaking extraction on the SpamAssassin corpus and on Outlook TNEF wrappers.

## Metadata

The metadata map, [ydk:entity:extraction/Metadata], is built in three passes.

1. **Every header.** Every top-level header is copied, decoded and unfolded, under a predictable key derived from its name. Repeated headers are kept, not dropped. This meets the original requirement to capture "as many headers as possible" ([ydk:req:metadata/all-headers]).
2. **Classic overlay.** The usual envelope headers (addresses, subject, date, message identifiers, trace headers) are then written under fixed keys from MimeKit's parsed view, replacing the raw copy where both exist ([ydk:req:metadata/classic-fields]). The four address keys are always present, so callers can index them without a missing-key check. When a header is absent, its key holds an empty string (ADR-005).
3. **Direction keys.** The two direction values described below are added last.

The exact key names and value formats are in [ydk:entity:extraction/Metadata].

If the message cannot be parsed at all, the map is empty ([ydk:warning:extraction/parse-failed]).

## Custom headers

The original specification asks for all fields starting with "X_" to be joined into one long string. Real mail writes custom headers as "X-", so both prefixes count. Each matching header becomes one "name: value" line in header order. The lines are joined into one string on the result and are also kept in the metadata map ([ydk:req:metadata/xfields]). Authentication headers such as DKIM signatures are not custom headers and never appear in this string.

## Main content

One body part is chosen as the main content ([ydk:req:content/main-content-selection], ADR-003). HTML is preferred because the content feeds a downstream indexer and language-model pipeline, and the markup keeps the document structure that plain text loses.

- When a message offers alternative renderings, HTML wins over plain text, and plain text wins over anything else.
- In a related bundle (HTML plus its inline images), the root part is used.
- In a mixed message, the first part that qualifies is used.
- A text part marked as an attachment is never the body. A named calendar file therefore stays an attachment.

HTML is returned as markup with its inline-image references and direction attributes intact. The result also reports the chosen part's media type next to it, so callers know whether they got markup; both are empty when no body was chosen. If the chosen part cannot be decoded, the body is empty and [ydk:warning:extraction/body-decode-failed] is recorded.

## Text direction

Hebrew and Arabic support is part of the original correctness requirement. Commit c3b39bb adds a direction value for the subject and for the body, left-to-right or right-to-left, so the downstream indexer and language-model consumers can handle right-to-left text such as Hebrew correctly ([ydk:req:content/text-direction], ADR-006).

- For an HTML body, a direction the author declared in the markup wins over the letters.
- Otherwise, and always for the subject, the first strongly-directional character decides, following the Unicode bidi algorithm's first-strong rule.
- Empty text defaults to left-to-right.

The precedence order and the exact character ranges are in [ydk:req:content/text-direction].

Direction is reported, never applied. The text keeps its bidi marks, and the HTML keeps its attributes.

## Failure handling

Content never makes extraction throw ([ydk:req:extraction/no-throw-on-malformed], ADR-004). Per the project owner (2026-10-17), the downstream indexer and language-model pipeline extracts messages in batches, and one bad message must not abort the batch. Both ways of calling the extractor behave the same. A whole-message parse failure returns an empty result with [ydk:warning:extraction/parse-failed]. A failure in one part affects that part only and is recorded as a warning while the rest of the message is still extracted. Any other, unexpected failure is caught too and recorded as [ydk:warning:extraction/internal-error], naming the kind of failure. That warning means a library bug, so the tests and the golden scorer fail whenever it appears, and bugs surface in CI instead of hiding. Only programmer misuse, such as passing no message at all, still throws.

The code does not do this yet: today it catches a fixed list of failure kinds when parsing bytes, lets any other kind escape, and catches nothing at the top level when given an already-parsed message. Closing that gap is Stage 02 work. The warning format is defined once in [cross-cutting.md](cross-cutting.md).

## Evidence

[ydk:test:extraction/extraction.unit] covers metadata, custom headers, body choice, mbox input, folded headers, legacy charsets, and corrupt encodings. [ydk:test:extraction/exotic-language.unit] covers round-trips in seven scripts plus emoji, and every direction rule.
