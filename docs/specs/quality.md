# Quality evidence - golden set, harvest, throughput

The original specification asks for three kinds of proof: correctness for emoji and exotic languages, correctness for a mix of attachment kinds, and the maximum files per second on the available hardware. This page explains how each is produced. Raw reports are in [golden-score.txt](../golden-score.txt), [golden.md](../golden.md), [edge-cases.md](../edge-cases.md), and [perf-results.md](../perf-results.md).

## Authored golden set

A golden set copied from the parser's own output would only prove that the parser agrees with itself. Instead, every expected value is written from the same authored description used to generate the test message: the subject, body, and attachment hashes come from the catalog, never from the extractor ([ydk:req:quality/golden-independence], ADR-007).

There are two tiers:

- **Seed** - 12 cases that run with every normal test run, including CI ([ydk:test:quality/seed-golden.integration]).
- **Bulk** - 1,000 core cases plus 200 RFC-edge cases, grouped into families: detection, classic headers, custom headers, unicode and right-to-left text, legacy charsets, body choice, transfer encodings, multipart shapes, dispositions and filenames, inline images, international addresses, Office and PDF files, nested mail, images, other file types, edge cases, and combinations. The bulk set is scored with the GoldenKit tool, and the report is committed.

The bar is that every case passes: [ydk:nfr:reliability/golden-pass-rate]. The last committed report shows 1212 of 1212 cases and 11,333 of 11,333 checks, across 48 tags.

The target is that CI runs the full score, all 1,200 bulk cases plus the 12 seed cases, on every change and fails when the result drops below the 1212 of 1212 baseline (ADR-011). This is not wired up yet: today CI runs only the seed cases, as part of the normal test run, and the bulk score is run by hand. Wiring it is Stage 02 work.

Attachment payloads in the golden set are tiny typed stubs with the correct leading bytes, checked by name, size, and hash. They prove decoding and naming, not real-world document variety. The harvest covers that.

## Real-world harvest

Real public mail cannot be committed: licences vary and the samples contain personal data. So it lives in a gitignored folder and is exercised two ways:

- The GoldenKit probe command runs detection and extraction over every local sample and counts throws, parse failures, and warnings. The target is zero uncaught exceptions ([ydk:nfr:reliability/real-world-harvest-no-throw]). The last run detected 12,853 messages with 0 throws, 0 parse failures, and 0 warnings.
- [ydk:test:quality/internal-corpus.integration] makes 17 targeted assertions about specific real samples: Outlook TNEF, large Exchange header blocks, Outlook paste images, legacy Asian charsets, delivery reports, S/MIME, real Office files, and Hebrew from real clients. Each test passes vacuously when its sample is absent, so CI does not depend on them.

The gaps the harvest has not yet closed are listed under DEFERRED in [scope.md](scope.md).

## Throughput

The pipeline team needs a per-core figure to size servers. The bench measures single-thread throughput on in-memory buffers with no disk access, so the result reflects parsing cost alone. Recorded baselines come from an Apple M1 Pro:

- tiny message: [ydk:nfr:performance/extract-tiny-throughput]
- 20 KB HTML message: [ydk:nfr:performance/extract-html-20kb-throughput]
- message with a 1 MB attachment: [ydk:nfr:performance/extract-1mb-attachment-throughput]
- detection alone: [ydk:nfr:performance/detect-tiny-throughput]

Two of these manifests also carry the sizing formulas from [perf-results.md](../perf-results.md). The tiny-message manifest carries the capacity formula: single-thread rate times cores times a contention factor of 0.7. The 1 MB attachment manifest carries the memory formula that bounds concurrency for attachment-heavy mail. The other two record only the measured rate. These numbers are baselines, not commitments. The repo sets no throughput target, and the figures must be re-measured on production hardware.
