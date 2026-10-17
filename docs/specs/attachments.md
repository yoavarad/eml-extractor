# Attachments

## What the caller gets

Every part of the message that is not the chosen body comes back as one [ydk:entity:extraction/ExtractedAttachment]. The one exception is the other renderings of the chosen body (see Naming rules below), which are dropped rather than returned. The original specification asks for four things per sub-file: its name, its size, its full path if it exists, and its bytes. Each record holds those four, plus the part's content identifier and disposition so callers can match inline images to the HTML ([ydk:req:attachments/attachment-record]).

The four brief fields carry the names of the shared team contract proposed in ADR-012, so the OLE extractor and this one describe a sub-file the same way. The renaming is target behaviour; the code still uses its older names until Stage 02.

The list is flat and follows document order. No record nests inside another.

## Full path: where the file sat in the message

Per the project owner (ADR-013), the full path tells the caller where in the message a file came from. It lists the file containers the part was found in, then the file's own name ([ydk:req:attachments/container-path]). A file attached directly to the message has a path equal to its name. A file unpacked from an Outlook wrapper has the wrapper's name in front, so the factory can see that a spreadsheet arrived inside `winmail.dat`. Multipart structure is not a file container and adds nothing.

The path never includes the name the caller gave the whole message. Files are visited in one fixed order, and when two files would end up with the same path, the later one gets a numbered suffix that never takes a name another file already has, so every path in one result is unique and usable as a key. The displayed name stays as the sender wrote it. Neither the name nor the path is safe to use directly as a file name on disk: reserved device names and characters some file systems reject can survive, so a consumer that writes files must clean them first.

Two other location hints are kept beside the path, never mixed into it: the sender's original filename when it contained directory parts, and the part's declared content location. Both are sender-controlled, so they are reported and not trusted ([cross-cutting.md](cross-cutting.md), path traversal).

Today the code fills the full path only when the sender's filename contains a directory part, and ignores the content location. Closing that gap is Stage 02 work.

## Why the library stops at the bytes

The original specification is explicit: no file-type detection and no extraction of nested content. [perf-results.md](../perf-results.md) lists type detection and nested Office extraction as downstream work, outside this library (ADR-008). The library therefore:

- never inspects attachment bytes to decide what they are, and never opens Office, PDF, image, archive, or calendar content;
- returns a forwarded or attached message as one opaque mail file and does not promote its inner parts ([ydk:req:attachments/no-recursion]).

ADR-013 confirms that nested messages stay opaque. A nested message is a complete mail file, and opening mail files is the factory's job, done by calling [ydk:contract:api/eml-extractor] again on those bytes, as the golden harness does. A caller that does this builds the deeper path by putting the nested message's path in front of each inner path, for example a report inside a forwarded message.

## Outlook TNEF wrappers

Outlook sometimes wraps all attachments in a single proprietary container, `winmail.dat`. The library unpacks it into the files it holds, using the parser library's TNEF support ([ydk:library:deps/mimekit]), and walks those files like any other part ([ydk:req:attachments/tnef-unwrap]). The wrapper is a transport envelope around the outer message's own attachments, not a file the sender chose to attach, which is why it is unpacked while nested messages are not (ADR-013). Each unpacked file's path starts with the wrapper's name. If unpacking fails, the container itself is returned and [ydk:warning:extraction/tnef-extract-failed] is recorded.

## Pasted images

When a user pastes an image into an HTML mail, the image travels as a related part, and the HTML points at it by content identifier. These images are attachments like any other, with their bytes, identifier, and disposition ([ydk:req:attachments/inline-cid-images]). The HTML body keeps its references untouched, so a caller can rebuild the rendered mail.

## Naming rules

- The display name is the last segment of the sender's filename. A name that is empty or only dots after that step counts as missing.
- A part without a usable filename is named from its content identifier when it has one, otherwise from its position and subtype. A nested message without a name gets a generic mail-file name. Separator characters inside a generated name are replaced, so a name never splits the path.
- The non-chosen renderings of the body (the plain-text twin of an HTML body) are not attachments.

The exact rules are on [ydk:entity:extraction/ExtractedAttachment] and [ydk:req:attachments/container-path].

## Failures

A part that cannot be decoded is skipped and recorded as [ydk:warning:extraction/part-decode-failed]. A nested message that cannot be serialised is skipped and recorded as [ydk:warning:extraction/rfc822-extract-failed]. Either way, the other parts are still returned.

## Evidence

[ydk:test:attachments/attachment.unit] covers Office, PDF, image, and archive stubs, nested mail, pasted images, calendar files, and signature parts. Real Office documents, real TNEF containers, attached Outlook items, and real paste images move into a committed, licensed sample set that CI always runs ([ydk:test:quality/realistic-samples.integration], ADR-018). Until that set exists they are covered only by [ydk:test:quality/internal-corpus.integration] when the public samples are present locally.
