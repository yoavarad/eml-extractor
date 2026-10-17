# EML Extraction - Overview

## Problem statement

We are building an in-process .NET 10 library that receives the raw bytes of a file, decides whether those bytes are an internet mail message (EML, RFC 822/2822/5322 with RFC 2045-2049 MIME), and, for messages, returns the header metadata, the main body, and every attached file as raw decoded bytes. The library stops there: it does not detect file types and does not open attachments.

The consumer is a downstream file-processing pipeline. The original specification (written in Hebrew, translated in [docs/requirements.md](../requirements.md)) calls it "the factory" and puts its wiring out of scope. That pipeline needs one call to classify a file and one call to break a message into parts it can route further. The original specification also asked for evidence that the parts come out right for exotic languages and mixed attachments, and for a measured files-per-second figure to size hardware with.

## Who uses it and how

A caller holds a file in memory. It asks the detector, [ydk:contract:api/eml-detector], whether the bytes are a message. If they are, it passes the same bytes to the extractor, [ydk:contract:api/eml-extractor], and receives one [ydk:entity:extraction/EmlExtractionResult]. From that result the caller reads the header map [ydk:entity:extraction/Metadata], the concatenated custom-header string, the main body and its media type, the subject and body text direction, a flat list of [ydk:entity:extraction/ExtractedAttachment] records, and a list of warnings.

The two calls are independent. The extractor does not re-run detection, and the detector never parses MIME. A caller that already holds a MimeKit message object can skip the byte overload and pass the parsed message directly (see [ydk:contract:api/eml-extractor]).

## End-to-end flow

1. The caller hands raw bytes to the detector. The detector strips a byte-order mark, rejects known non-mail formats by their leading bytes, skips an mbox separator line, and checks that the first lines form a plausible header block ([ydk:req:detection/detect-eml], [ydk:req:detection/reject-non-email-magic]). Details are in [detection.md](detection.md).
2. For a positive answer, the caller hands the same bytes to the extractor. MimeKit ([ydk:library:deps/mimekit]) parses the message, in mbox mode when the separator line is present ([ydk:req:extraction/mbox-from-line]).
3. Every header is copied into the metadata map, the classic envelope headers are overlaid under fixed keys, and custom headers are concatenated ([ydk:req:metadata/all-headers], [ydk:req:metadata/classic-fields], [ydk:req:metadata/xfields]).
4. The main body is chosen, preferring HTML over plain text ([ydk:req:content/main-content-selection]), and the subject and body directions are computed ([ydk:req:content/text-direction]). Details are in [extraction.md](extraction.md).
5. Every remaining MIME leaf becomes an attachment record. Nested messages stay opaque, Outlook TNEF wrappers are unpacked, and pasted inline images are kept as files ([ydk:req:attachments/attachment-record], [ydk:req:attachments/no-recursion], [ydk:req:attachments/tnef-unwrap], [ydk:req:attachments/inline-cid-images]). Details are in [attachments.md](attachments.md).
6. Problems along the way become warnings on the result instead of exceptions ([ydk:req:extraction/no-throw-on-malformed], format in [ydk:crosscut:core/warning-format]).

## Success criteria

- Every authored golden case passes: [ydk:nfr:reliability/golden-pass-rate]. Last recorded result: 1212 of 1212 cases.
- The real-world public-mail harvest produces no uncaught exceptions: [ydk:nfr:reliability/real-world-harvest-no-throw]. Last recorded run: 12,853 messages.
- Single-thread throughput is measured and published for sizing: [ydk:nfr:performance/extract-tiny-throughput], [ydk:nfr:performance/extract-html-20kb-throughput], [ydk:nfr:performance/extract-1mb-attachment-throughput], [ydk:nfr:performance/detect-tiny-throughput].
- The CI pipeline builds with warnings as errors, verifies formatting, runs the unit and seed-golden tests, and fails on High or Critical vulnerable packages (ADR-001).

How these are measured is in [quality.md](quality.md) and [testing.md](testing.md).

## Dependencies

The only runtime library the code calls is MimeKit, [ydk:library:deps/mimekit]. Its parser options and the code-page encoding registration are fixed once for the whole process. A second package, [ydk:library:deps/system-security-cryptography-pkcs], is referenced only to pin a patched transitive version (ADR-009). Neither is a network service.

## Spec map

| Narrative | Covers |
| --- | --- |
| [scope.md](scope.md) | What is in, out, and deferred |
| [cross-cutting.md](cross-cutting.md) | Runtime surface, warning format, timestamps, failure and abuse cases |
| [detection.md](detection.md) | Deciding whether bytes are a message |
| [extraction.md](extraction.md) | Metadata, custom headers, main body, direction, warnings |
| [attachments.md](attachments.md) | Attachment records, nested mail, TNEF, inline images |
| [quality.md](quality.md) | Golden set, real-world harvest, throughput |
| [testing.md](testing.md) | Test layers and rules |
| [glossary.md](glossary.md) | Canonical terms |

Decisions are recorded in [docs/adrs/](../adrs/). The original documents stay in place as source material: [README.md](../../README.md), [requirements.md](../requirements.md), [edge-cases.md](../edge-cases.md), [golden.md](../golden.md), [perf-results.md](../perf-results.md), [golden-score.txt](../golden-score.txt).
