# EML Extraction - Overview

## Problem statement

We are building an in-process .NET library, for .NET 10 and (target, ADR-019) .NET 8, that receives the raw bytes of a file, decides whether those bytes are an internet mail message (EML, RFC 822/2822/5322 with RFC 2045-2049 MIME), and, for messages, returns the header metadata, the main body, and every attached file as raw decoded bytes. The library stops there: it does not detect file types and does not open attachments.

The consumer is a downstream file-processing pipeline. The original specification (written in Hebrew, translated in [docs/requirements.md](../requirements.md)) calls it "the factory" and puts its wiring out of scope: the component goes into the team's extraction template, and the team does the factory integration. That pipeline needs one call to classify a file and one call to break a message into parts it can route further. The original specification also asked for evidence that the parts come out right for exotic languages and mixed attachments, and for a measured files-per-second figure to size hardware with.

A sibling extractor for OLE / Office files was built against the same brief. The owner decided (ADR-012) to propose one shared contract to the team, so both extractors look the same to the template, and to align this library with it now. How each brief item is met, and what is still open, is in [docs/research/brief-gap-analysis.md](../research/brief-gap-analysis.md); questions for the team are in [docs/team-questions.md](../team-questions.md).

## Who uses it and how

A caller holds a file in memory. It asks the detector, [ydk:contract:api/eml-detector], whether the bytes are a message, either as a plain yes/no or as a typed answer, [ydk:entity:extraction/EmlDetectionResult]. If they are, it wraps the same bytes in a request, [ydk:entity:extraction/EmlExtractionRequest], passes it to the extractor, [ydk:contract:api/eml-extractor] (or passes the bytes directly to the convenience call, which wraps them the same way), and receives one [ydk:entity:extraction/EmlExtractionResult]. From that result the caller reads the header map [ydk:entity:extraction/Metadata] (which includes the concatenated custom-header field), the main body and its media type, the subject and body text direction, a flat list of [ydk:entity:extraction/ExtractedAttachment] records, and a list of warnings.

The two calls are independent. The extractor does not re-run detection, and the detector never parses MIME. The request, the typed detection answer, and the move of parser-library types out of the public surface are target behaviour (ADR-012); today the extractor takes raw bytes or an already-parsed message object directly. The change lands in Stage 02.

## End-to-end flow

1. The caller hands raw bytes to the detector. The detector strips a byte-order mark, rejects known non-mail formats by their leading bytes, skips an mbox separator line, and checks that the first lines form a plausible header block ([ydk:req:detection/detect-eml], [ydk:req:detection/reject-non-email-magic]). Details are in [detection.md](detection.md).
2. For a positive answer, the caller hands the same bytes to the extractor in a request. Inputs over the configured size limit are refused before parsing; target (ADR-023) by throwing an exception that carries the size and the limit ([ydk:req:extraction/max-input-size]). The parser ([ydk:library:deps/mimekit]) parses the message, in mbox mode when the separator line is present ([ydk:req:extraction/mbox-from-line]).
3. Every header is copied into the metadata map under one key each, the classic envelope headers are rewritten in parsed form, and headers whose names start with the brief's custom prefix are joined into one metadata field ([ydk:req:metadata/all-headers], [ydk:req:metadata/canonical-keys], [ydk:req:metadata/classic-fields], [ydk:req:metadata/xfields]).
4. The main body is chosen, preferring HTML over plain text ([ydk:req:content/main-content-selection]), and the subject and body directions are computed ([ydk:req:content/text-direction]). Text is returned exactly as written ([ydk:req:content/unicode-fidelity]). Details are in [extraction.md](extraction.md).
5. Every remaining MIME leaf becomes an attachment record with its position in the message. Nested messages stay opaque, Outlook TNEF wrappers are unpacked, and pasted inline images are kept as files ([ydk:req:attachments/attachment-record], [ydk:req:attachments/container-path], [ydk:req:attachments/no-recursion], [ydk:req:attachments/tnef-unwrap], [ydk:req:attachments/inline-cid-images]). Details are in [attachments.md](attachments.md).
6. Content problems along the way, such as a bad charset or an undecodable part, become warnings on the result with partial data ([ydk:req:extraction/no-throw-on-malformed], format in [ydk:crosscut:core/warning-format]). Misuse, oversized input, and library bugs throw informative exceptions instead (ADR-023, target).

## Success criteria

- Every authored golden case passes: [ydk:nfr:reliability/golden-pass-rate]. Last recorded result: 1212 of 1212 cases.
- Real-world public-mail content produces no exceptions: [ydk:nfr:reliability/real-world-harvest-no-throw]. Last recorded run: 12,853 messages.
- Committed, licensed realistic attachment samples pass in CI, and tests whose samples are absent show as skipped, never as passed: [ydk:req:quality/realistic-attachment-samples] (target, ADR-018).
- Throughput is measured across every degree of parallelism on real and synthetic mail, with memory, and published with a sizing formula the team can rerun: [ydk:nfr:performance/throughput-dop-sweep], [ydk:nfr:performance/memory-footprint] (target, ADR-015). The existing single-thread baselines stay as history: [ydk:nfr:performance/extract-tiny-throughput], [ydk:nfr:performance/extract-html-20kb-throughput], [ydk:nfr:performance/extract-1mb-attachment-throughput], [ydk:nfr:performance/detect-tiny-throughput].
- The library and its tests build and pass on both .NET 8 and .NET 10 ([ydk:req:quality/net8-multi-targeting], target, ADR-019).
- The CI pipeline builds with warnings as errors, verifies formatting, runs the unit and seed-golden tests, and fails on High or Critical vulnerable packages (ADR-001).

How these are measured is in [quality.md](quality.md) and [testing.md](testing.md).

## Dependencies and licensing

The only runtime library the code calls is the mail parser, [ydk:library:deps/mimekit]. Its parser options and the code-page encoding registration are fixed once for the whole process. The parser brings a cryptography package with it, [ydk:library:deps/bouncycastle-cryptography], which the library ships but never calls. A third package, [ydk:library:deps/system-security-cryptography-pkcs], is referenced only to pin a patched transitive version (ADR-009). None is a network service.

The brief requires everything to be open source. The repo is MIT-licensed (owner decision 2026-10-18, `LICENSE`), with a notices file that lists every dependency and every committed sample with its license ([ydk:req:quality/license-notices], ADR-017). All runtime dependencies are MIT-licensed.

## Language choice

The brief asks for C# on .NET 10. Per the project owner (ADR-019), the library also targets .NET 8 so the team's template can load it while it still runs on .NET 8; that target is dropped once the template moves to .NET 10. The brief also asks for an assessment of improving C# libraries versus using another language if a large capability gap shows up. No such gap has been found so far, but the comparison was never written down. It will be produced as a Stage 02 research document comparing the current parser with the main mail libraries of other languages, capability by capability ([ydk:req:research/capability-assessment]).

## Spec map

| Narrative | Covers |
| --- | --- |
| [scope.md](scope.md) | What is in, out, and deferred |
| [cross-cutting.md](cross-cutting.md) | Runtime surface, warning format, timestamps, failure and abuse cases |
| [detection.md](detection.md) | Deciding whether bytes are a message |
| [extraction.md](extraction.md) | Request, size limit, metadata, custom headers, main body, direction, warnings |
| [attachments.md](attachments.md) | Attachment records, full path, nested mail, TNEF, inline images |
| [quality.md](quality.md) | Golden set, realistic samples, real-world harvest, load test |
| [testing.md](testing.md) | Test layers and rules |
| [glossary.md](glossary.md) | Canonical terms |

Decisions are recorded in [docs/adrs/](../adrs/). The original documents stay in place as source material: [README.md](../../README.md), [requirements.md](../requirements.md), [edge-cases.md](../edge-cases.md), [golden.md](../golden.md), [perf-results.md](../perf-results.md), [golden-score.txt](../golden-score.txt). README describes today's API until the Stage 02 changes land.
