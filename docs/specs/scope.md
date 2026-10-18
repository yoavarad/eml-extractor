# Scope

The original specification is translated in [docs/requirements.md](../requirements.md). This page fixes the boundary so nobody builds past it. Items marked "target" are decided but not yet in code; they are Stage 02 work.

## IN scope

- Detecting whether a byte buffer is a mail message from its content alone, ignoring the file name, as a yes/no and (target) as a typed answer: [ydk:contract:api/eml-detector], [ydk:req:detection/detect-eml], [ydk:entity:extraction/EmlDetectionResult].
- Rejecting common non-mail formats by their leading bytes. The original spec did not ask for this; it was implemented anyway: [ydk:req:detection/reject-non-email-magic].
- Accepting files that start with an mbox separator line or a UTF-8 byte-order mark: [ydk:req:extraction/mbox-from-line].
- The shared team contract for the public surface (target, ADR-012): a request record, [ydk:entity:extraction/EmlExtractionRequest], shared sub-file field names, and no parser-library types in the public API.
- Supporting both .NET 8 and .NET 10 hosts with identical behaviour, with tests run on both in CI (target, ADR-019): [ydk:req:quality/net8-multi-targeting]. The developer tools stay on .NET 10 only.
- Refusing inputs over a host-set size limit with a warning (target): [ydk:req:extraction/max-input-size].
- Extracting every top-level header under one canonical key each, the classic envelope headers in parsed form (with the four address keys always present), and the brief's custom-header field: [ydk:req:metadata/all-headers], [ydk:req:metadata/canonical-keys], [ydk:req:metadata/classic-fields], [ydk:req:metadata/xfields]. The canonical keys and the literal custom-header rule are target (ADR-014, ADR-016).
- Selecting one main body, with HTML preferred, and reporting its media type: [ydk:req:content/main-content-selection].
- Returning text exactly as written, including emoji sequences and combining marks: [ydk:req:content/unicode-fidelity].
- Reporting left-to-right or right-to-left direction for subject and body: [ydk:req:content/text-direction].
- Returning every other MIME part as a flat attachment record with decoded bytes and its position in the message: [ydk:req:attachments/attachment-record], [ydk:req:attachments/container-path] (target, ADR-013), including pasted inline images [ydk:req:attachments/inline-cid-images] and the files inside Outlook TNEF wrappers [ydk:req:attachments/tnef-unwrap].
- Never crashing the caller on malformed but readable MIME content, and reporting those problems as warnings: [ydk:req:extraction/no-throw-on-malformed]. The original spec did not ask for this; it was implemented anyway. Misuse, oversized input, and library bugs throw informative exceptions (ADR-023, target).
- The authored golden set, the public-mail harvest tool, and the load test as developer tooling: [quality.md](quality.md).
- A CI gate on the full golden score (1,200 bulk plus 12 seed cases), failing below the 1212 of 1212 baseline: [ydk:nfr:reliability/golden-pass-rate] (ADR-011). Accepted but not implemented yet; CI runs only the seed cases today.
- A committed, licensed realistic sample set run in CI, and visible skips for absent samples (target, ADR-018): [ydk:req:quality/realistic-attachment-samples].
- A load test across every degree of parallelism with measured memory, a sizing formula, and a committed baseline with a warn-only regression rule (target, ADR-015): [ydk:nfr:performance/throughput-dop-sweep], [ydk:nfr:performance/memory-footprint].
- Dev-time profiling per extraction stage with a committed hotspot report, then optimization only from that data, each change proven by its own before-and-after measurement (target, ADR-020, ADR-022): [ydk:req:quality/profiling-report], [ydk:req:quality/optimization-evidence].
- Runtime metrics and tracing through the facilities built into .NET, with no telemetry package dependency, no message content in any label, and a measured cost budget (target, ADR-021): [ydk:crosscut:core/observability], [ydk:req:observability/runtime-telemetry], [ydk:nfr:observability/metrics-overhead].
- A license file and third-party notices with a dependency license audit (ADR-017; MIT, accepted by the owner 2026-10-18): [ydk:req:quality/license-notices].
- A written capability assessment of C# versus other languages for this job, as a Stage 02 research document: [ydk:req:research/capability-assessment].
- Hand-off documents for the team: [docs/research/brief-gap-analysis.md](../research/brief-gap-analysis.md) and [docs/team-questions.md](../team-questions.md).

## OUT of scope

- Shipping a telemetry exporter or pipeline, or choosing the host's monitoring backend. The host wires that up ([ydk:crosscut:core/observability]).
- Any HTTP API, authentication, authorisation, user interface, persistence, or background service. The deliverable is an in-process class library only: [ydk:crosscut:core/library-surface].
- Wiring into the downstream processing pipeline (the "factory" or template in the original spec). The team does this; this repo only proposes the shared contract.
- Deciding the error model for the OLE extractor, or changing the OLE repo. Divergences are listed for the team.
- File-type detection of attachments, and parsing inside Office, PDF, image, archive, or calendar files: [ydk:req:attachments/no-recursion].
- Recursing into nested messages. A forwarded message is one opaque attachment, and callers re-extract it themselves if they need to (ADR-013): [ydk:req:attachments/no-recursion].
- A batch extraction call. The OLE extractor has one; per the project owner (ADR-012) the factory owns parallelism, and each call is independent.
- Converting HTML to plain text, sanitising HTML, or resolving inline-image references into the body.
- Decrypting S/MIME or PGP content, or verifying signatures. Signature and encrypted parts are kept as opaque attachments.
- Reassembling `message/partial` fragments or uuencoded payloads (edge case E24 in [edge-cases.md](../edge-cases.md)).
- Parsing more than the first message of an mbox file.
- Resolving or sanitising path-bearing attachment filenames. The raw path is reported as given, beside the full path.
- Committing the internal real-world corpus (licences and personal data).
- Golden coverage of full trace headers, warning text, recursive Office content, and latency ([ydk:req:quality/golden-independence]).

## DEFERRED

- Streaming input or output. Whole messages and decoded parts stay in memory. Moves to IN scope only if the measured memory of a maximum-size input exceeds the ceiling in [ydk:nfr:performance/memory-footprint].
- Additional request input sources (a stream or a file path, as the OLE extractor has). The request record leaves room for them (ADR-012).

These are recorded gaps in [edge-cases.md](../edge-cases.md) and [requirements.md](../requirements.md). Each needs a real sample before any work starts:

- A live Outlook or Gmail Arabic body, and a live Outlook Hebrew body (only a Gmail Hebrew subject and a 1992 Hebrew body are covered).
- A header block larger than the 16 KB detector sniff window (E27). Today's largest sample is about 12 KB.
- PGP `multipart/encrypted` payloads (E28, internal-missing).
- A single part of 50 MB or more (E30). The harvest tool skips files over 40 MB.
- Throughput on real corporate mail and on the team's production server class. The load test runs on public mail and the owner's machine; the team reruns it on its servers ([quality.md](quality.md)).
