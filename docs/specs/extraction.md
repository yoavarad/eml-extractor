# Extraction - metadata, main content, direction

The extractor, [ydk:contract:api/eml-extractor], turns one message into one [ydk:entity:extraction/EmlExtractionResult]. This page covers everything in that result except attachments, which have their own page ([attachments.md](attachments.md)).

## Parsing

Parsing is delegated to MimeKit, [ydk:library:deps/mimekit] (ADR-002). The parser runs in its lenient modes for addresses, header parameters, and encoded words. The repo does not record why these modes were chosen (open question in ADR-002). When the buffer starts with an mbox separator line, the parser reads it as an mbox stream and keeps only the first message ([ydk:req:extraction/mbox-from-line]). Commit 251657d records that, before this was added, mbox files and Outlook wrappers broke extraction on the SpamAssassin corpus.

## Metadata

The metadata map, [ydk:entity:extraction/Metadata], is built in three passes.

1. **Every header.** Every top-level header is copied under a normalised key: lower case, with hyphens turned into underscores. Its value is already decoded and unfolded. When a header repeats, the values are joined in order. This meets the original requirement to capture "as many headers as possible" ([ydk:req:metadata/all-headers]).
2. **Classic overlay.** The usual envelope headers (addresses, subject, date, message identifiers, trace headers) are then written under fixed, punctuation-free keys from MimeKit's parsed view. These overwrite the raw copy where both exist ([ydk:req:metadata/classic-fields]). The four address keys are always present, so callers can index them without a missing-key check. When a header is absent, its key holds an empty string (ADR-005).
3. **Direction keys.** The two direction values described below are added last.

If the message cannot be parsed at all, the map is empty ([ydk:warning:extraction/parse-failed]).

## Custom headers

The original specification asks for all fields starting with "X_" to be joined into one long string. Real mail writes custom headers as "X-", so both prefixes count. Each matching header becomes one "name: value" line in header order. The lines are joined into one string on the result and are also kept in the metadata map ([ydk:req:metadata/xfields]). Authentication headers such as DKIM signatures are not custom headers and never appear in this string.

## Main content

One body part is chosen as the main content ([ydk:req:content/main-content-selection], ADR-003).

- When a message offers alternative renderings, HTML wins over plain text, and plain text wins over anything else.
- In a related bundle (HTML plus its inline images), the root part is used.
- In a mixed message, the first part that qualifies is used.
- A text part marked as an attachment is never the body. A named calendar file therefore stays an attachment.

HTML is returned as markup with its inline-image references and direction attributes intact. The result also reports the chosen part's media type, so callers know whether they got markup. If the chosen part cannot be decoded, the body is empty and [ydk:warning:extraction/body-decode-failed] is recorded.

## Text direction

Hebrew and Arabic support is part of the original correctness requirement. Commit c3b39bb adds a direction value for the subject and for the body, left-to-right or right-to-left, so callers can rely on it ([ydk:req:content/text-direction], ADR-006).

- For an HTML body, an explicit direction in the markup wins: the html element first, then the body element, then a CSS direction declaration.
- Otherwise, and always for the subject, the first strongly-directional character decides, following the Unicode bidi algorithm's first-strong rule. Explicit bidi marks count as strong.
- Empty text defaults to left-to-right.

Direction is reported, never applied. The text keeps its bidi marks, and the HTML keeps its attributes.

## Failure handling

Extraction does not throw on the malformed input it knows how to handle ([ydk:req:extraction/no-throw-on-malformed], ADR-004). A whole-message parse failure returns an empty result with [ydk:warning:extraction/parse-failed]. Any other failure affects one part only and is recorded as a warning while the rest of the message is still extracted. The warning format is defined once in [cross-cutting.md](cross-cutting.md).

## Evidence

[ydk:test:extraction/extraction.unit] covers metadata, custom headers, body choice, mbox input, folded headers, legacy charsets, and corrupt encodings. [ydk:test:extraction/exotic-language.unit] covers round-trips in eight scripts plus emoji, and every direction rule.
