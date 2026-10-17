# Attachments

## What the caller gets

Every part of the message that is not the chosen body comes back as one [ydk:entity:extraction/ExtractedAttachment]. Each record holds the file's name, its decoded size, the original path when the sender's filename carried one, and the decoded bytes. The original specification asks for these four; the implementation also reports the part's content identifier and disposition so callers can match inline images to the HTML ([ydk:req:attachments/attachment-record]).

The list is flat and follows document order. No record nests inside another.

## Why the library stops at the bytes

The original specification is explicit: no file-type detection and no extraction of nested content. [perf-results.md](../perf-results.md) lists type detection and nested Office extraction as downstream work, outside this library (ADR-008). The library therefore:

- never inspects attachment bytes to decide what they are, and never opens Office, PDF, image, archive, or calendar content;
- returns a forwarded or attached message as one opaque mail file and does not promote its inner parts ([ydk:req:attachments/no-recursion]). A caller that wants the inner content passes those bytes to [ydk:contract:api/eml-extractor] again, as the golden harness does.

## Outlook TNEF wrappers

Outlook sometimes wraps all attachments in a single proprietary container, `winmail.dat`. The library unpacks it into the files it holds, using MimeKit's TNEF support ([ydk:library:deps/mimekit]), and walks those files like any other part ([ydk:req:attachments/tnef-unwrap]). [requirements.md](../requirements.md) describes this as peeling the Outlook wrapper rather than parsing content, which is why it sits beside the no-recursion rule instead of against it. If unpacking fails, the container itself is returned and [ydk:warning:extraction/tnef-extract-failed] is recorded.

## Pasted images

When a user pastes an image into an HTML mail, the image travels as a related part, and the HTML points at it by content identifier. These images are attachments like any other, with their bytes, identifier, and disposition ([ydk:req:attachments/inline-cid-images]). The HTML body keeps its references untouched, so a caller can rebuild the rendered mail.

## Naming rules

- The display name is the last segment of the sender's filename. When that filename contained a path, the raw path is reported separately, so the path is neither lost nor trusted ([cross-cutting.md](cross-cutting.md), path traversal).
- A part without a filename is named from its content identifier when it has one, otherwise from its position and subtype. A nested message without a name gets a generic mail-file name.
- The non-chosen renderings of the body (the plain-text twin of an HTML body) are not attachments.

## Failures

A part that cannot be decoded is skipped and recorded as [ydk:warning:extraction/part-decode-failed]. A nested message that cannot be serialised is skipped and recorded as [ydk:warning:extraction/rfc822-extract-failed]. Either way, the other parts are still returned.

## Evidence

[ydk:test:attachments/attachment.unit] covers Office, PDF, image, and archive stubs, nested mail, pasted images, calendar files, and signature parts. Real Office documents, real TNEF containers, and real Outlook paste images are covered by [ydk:test:quality/internal-corpus.integration] when the public samples are present locally.
