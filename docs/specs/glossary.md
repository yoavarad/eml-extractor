# Glossary

One name per concept. Use the canonical term in specs, code comments, and reviews.

| Canonical term | Meaning | Do not use |
| --- | --- | --- |
| Message | One RFC 5322 internet mail message with its MIME body | email file, mail object |
| EML | The on-disk or in-memory byte form of a message | .eml file (when talking about content rather than a file name) |
| Detector | The sniffing entry point, [ydk:contract:api/eml-detector] | validator, classifier |
| Extractor | The parsing entry point, [ydk:contract:api/eml-extractor] | parser (that word means MimeKit), reader |
| Extraction request | The input record holding the message bytes and an optional caller file name, [ydk:entity:extraction/EmlExtractionRequest] (target, ADR-012) | input, call args |
| Detection result | The typed answer of the detector: yes/no plus media type, [ydk:entity:extraction/EmlDetectionResult] (target, ADR-012) | detection response, sniff result |
| Extraction result | The record returned by the extractor, [ydk:entity:extraction/EmlExtractionResult] | output, response |
| Metadata | The header map on the extraction result, [ydk:entity:extraction/Metadata] | headers dict, properties |
| Canonical key | The one metadata key of a header: its name in lower case, nothing else changed ([ydk:req:metadata/canonical-keys]) | normalised name, snake key |
| Classic fields | The envelope headers exposed under fixed keys ([ydk:req:metadata/classic-fields]) | standard headers, core headers |
| Address keys | The four classic fields for sender and recipients that are always present | participants |
| xfields | The metadata field that joins, one line each, every header whose name starts with exactly capital X and underscore ([ydk:req:metadata/xfields], ADR-014) | x-headers blob, custom metadata, X-headers (for the dash form, which is not in xfields) |
| Main content | The one body part chosen as the message body ([ydk:req:content/main-content-selection]) | body text, message text |
| Direction | Left-to-right or right-to-left paragraph direction of the subject or main content ([ydk:req:content/text-direction]) | RTL flag, bidi mode |
| Sub-file | The brief's word for an attachment; used only when quoting the brief | embedded file (OLE term) |
| Attachment | Any collected non-body part, [ydk:entity:extraction/ExtractedAttachment], including inline images and nested messages | file, nested file, part (when it has been collected) |
| Full path | The container path of an attachment: the names of the file containers it was found in, then its own name, joined by a slash ([ydk:req:attachments/container-path], ADR-013) | package path (OLE term), location, raw path |
| Container path | Synonym used only to explain the full path | - |
| Original path | The sender's raw filename when it contains directory parts; kept beside the full path, never trusted | full path (old meaning) |
| Shared team contract | The request, detection result, and sub-file names proposed to the team for both extractors (ADR-012) | common API, unified DTOs |
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
| Realistic sample set | The committed, licensed set of real or tool-generated samples run in CI ([ydk:req:quality/realistic-attachment-samples]) | fixtures (for these), real corpus |
| Load test | The sweep over every degree of parallelism that measures throughput and memory ([ydk:nfr:performance/throughput-dop-sweep]) | bench (for the sweep), stress test |
| Hotspot | A stage or code path that the profiling report ranks among the largest shares of processor time or allocated memory ([ydk:req:quality/profiling-report], ADR-020) | bottleneck (unless measured), slow part |
| Profiling report | The committed per-stage report with the ranked hotspot table ([ydk:req:quality/profiling-report]) | perf notes, profile dump |
| Meter | The library's one named source of runtime measurements, which the host subscribes to ([ydk:crosscut:core/observability], ADR-021) | metrics logger, stats collector |
| Telemetry contract | The fixed list of measurement and trace names, units, and labels the library emits ([ydk:req:observability/runtime-telemetry]) | metrics schema, counters list |
| Scaling efficiency | Measured throughput at n threads divided by n times the one-thread throughput (ADR-015) | contention factor, fudge factor |
| Typed stub | A tiny attachment payload with the right leading bytes but no real content | fake file, dummy |
