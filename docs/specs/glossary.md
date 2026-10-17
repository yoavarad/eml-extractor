# Glossary

One name per concept. Use the canonical term in specs, code comments, and reviews.

| Canonical term | Meaning | Do not use |
| --- | --- | --- |
| Message | One RFC 5322 internet mail message with its MIME body | email file, mail object |
| EML | The on-disk or in-memory byte form of a message | .eml file (when talking about content rather than a file name) |
| Detector | The sniffing entry point, [ydk:contract:api/eml-detector] | validator, classifier |
| Extractor | The parsing entry point, [ydk:contract:api/eml-extractor] | parser (that word means MimeKit), reader |
| Extraction result | The record returned by the extractor, [ydk:entity:extraction/EmlExtractionResult] | output, response |
| Metadata | The header map on the extraction result, [ydk:entity:extraction/Metadata] | headers dict, properties |
| Classic fields | The envelope headers exposed under fixed keys ([ydk:req:metadata/classic-fields]) | standard headers, core headers |
| Address keys | The four classic fields for sender and recipients that are always present | participants |
| xfields | The single string that joins every custom (X- or X_) header line ([ydk:req:metadata/xfields]) | x-headers blob, custom metadata |
| Main content | The one body part chosen as the message body ([ydk:req:content/main-content-selection]) | body text, message text |
| Direction | Left-to-right or right-to-left paragraph direction of the subject or main content ([ydk:req:content/text-direction]) | RTL flag, bidi mode |
| Attachment | Any collected non-body part, [ydk:entity:extraction/ExtractedAttachment], including inline images and nested messages | file, nested file, part (when it has been collected) |
| Nested message | An attached message, returned as one opaque attachment ([ydk:req:attachments/no-recursion]) | inner email, sub-mail |
| Inline image | An image part referenced from HTML by content identifier ([ydk:req:attachments/inline-cid-images]) | embedded image, CID attachment |
| TNEF wrapper | Outlook's `winmail.dat` container, unpacked into its files ([ydk:req:attachments/tnef-unwrap]) | winmail attachment |
| mbox separator | The "From " line that begins each message in an mbox archive ([ydk:req:extraction/mbox-from-line]) | envelope line, From_ header |
| Warning | One diagnostic string on the extraction result ([ydk:crosscut:core/warning-format]) | error, exception (unless one is actually thrown) |
| Golden case | One authored message with its expected result ([ydk:req:quality/golden-independence]) | fixture (for golden data), snapshot |
| Seed set | The 12 golden cases run by the normal test command | smoke set |
| Bulk set | The 1,200 golden cases scored by GoldenKit | full set, big set |
| Harvest | A probe run over local public real-world mail ([ydk:nfr:reliability/real-world-harvest-no-throw]) | crawl, scan |
| Internal corpus | The gitignored folder of public real-world samples | private corpus, test data |
| Typed stub | A tiny attachment payload with the right leading bytes but no real content | fake file, dummy |
