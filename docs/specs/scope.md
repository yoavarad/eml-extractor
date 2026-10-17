# Scope

The original specification is translated in [docs/requirements.md](../requirements.md). This page fixes the boundary so nobody builds past it.

## IN scope

- Detecting whether a byte buffer is a mail message from its content alone, ignoring the file name: [ydk:contract:api/eml-detector], [ydk:req:detection/detect-eml].
- Rejecting common non-mail formats by their leading bytes. The original spec did not ask for this; it was implemented anyway: [ydk:req:detection/reject-non-email-magic].
- Accepting files that start with an mbox separator line or a UTF-8 byte-order mark: [ydk:req:extraction/mbox-from-line].
- Extracting every top-level header, the classic envelope headers under fixed keys (with the four address keys always present), and the concatenated custom-header string: [ydk:req:metadata/all-headers], [ydk:req:metadata/classic-fields], [ydk:req:metadata/xfields].
- Selecting one main body, with HTML preferred, and reporting its media type: [ydk:req:content/main-content-selection].
- Reporting left-to-right or right-to-left direction for subject and body: [ydk:req:content/text-direction].
- Returning every other MIME part as a flat attachment record with decoded bytes: [ydk:req:attachments/attachment-record], including pasted inline images [ydk:req:attachments/inline-cid-images] and the files inside Outlook TNEF wrappers [ydk:req:attachments/tnef-unwrap].
- Never crashing the caller on malformed MIME of the handled kinds, and reporting problems as warnings: [ydk:req:extraction/no-throw-on-malformed]. The original spec did not ask for this; it was implemented anyway.
- The authored golden set, the public-mail harvest tool, and the throughput bench as developer tooling: [quality.md](quality.md).

## OUT of scope

- Any HTTP API, authentication, authorisation, user interface, persistence, or background service. The deliverable is an in-process class library only: [ydk:crosscut:core/library-surface].
- Wiring into the downstream processing pipeline (the "factory" or template in the original spec).
- File-type detection of attachments, and parsing inside Office, PDF, image, archive, or calendar files: [ydk:req:attachments/no-recursion].
- Recursing into nested messages. A forwarded message is one opaque attachment, and callers re-extract it themselves if they need to: [ydk:req:attachments/no-recursion].
- Converting HTML to plain text, sanitising HTML, or resolving inline-image references into the body.
- Decrypting S/MIME or PGP content, or verifying signatures. Signature and encrypted parts are kept as opaque attachments.
- Reassembling `message/partial` fragments or uuencoded payloads (edge case E24 in [edge-cases.md](../edge-cases.md)).
- Parsing more than the first message of an mbox file.
- Streaming input or output. Whole messages and decoded parts are held in memory.
- Resolving or sanitising path-bearing attachment filenames. The raw path is reported as given.
- Golden coverage of full trace headers, warning text, recursive Office content, and latency ([ydk:req:quality/golden-independence]).

## DEFERRED

These are recorded gaps in [edge-cases.md](../edge-cases.md) and [requirements.md](../requirements.md). Each needs a real sample before any work starts:

- A live Outlook or Gmail Arabic body, and a live Outlook Hebrew body (only a Gmail Hebrew subject and a 1992 Hebrew body are covered).
- A header block larger than the 16 KB detector sniff window (E27). Today's largest sample is about 12 KB.
- A TNEF wrapper containing a modern OOXML document (E17). Today's samples hold OLE documents, PDF, and PNG.
- PGP `multipart/encrypted` payloads (E28, internal-missing).
- A single part of 50 MB or more (E30). The harvest tool skips files over 40 MB.
- An original, unstripped Outlook paste image (E08 uses a stand-in PNG).
- Throughput measured on generated mail only; real corporate mail and production server hardware are not measured ([quality.md](quality.md)).
- Wiring the 1200-case bulk score into CI. Today it is run by hand (ADR-007).
