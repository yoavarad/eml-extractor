# Testing strategy

## Layers

| Layer | What it proves | Runs in CI |
| --- | --- | --- |
| Unit | Each rule of detection, metadata, body choice, direction, unicode fidelity, and attachment collection, on messages built in code | Yes |
| Seed golden | Authored end-to-end expectations on 12 committed messages | Yes |
| Realistic samples | Correctness B on committed, licensed Office, Outlook item, mail, TNEF, and pasted-image samples | Target: yes, always (ADR-018, Stage 02) |
| Internal corpus | Behaviour on specific real public samples | Only when the samples are present locally; target is a visible skip otherwise |
| Bulk golden score | 1,200 authored cases across 48 tags | Not yet. Target is a CI gate at the 1212 of 1212 baseline (ADR-011); today it is run by hand |
| Harvest probe | No crash across every local public sample | No, run by hand |
| Load test | Throughput, latency, and memory across every degree of parallelism | By hand on the owner's machine; target is a nightly warn-only synthetic run (ADR-015) |
| Telemetry | Every runtime measurement is emitted with its contracted name and labels, and no message content leaks into it | Target: yes, on both .NET versions (ADR-021, Stage 02) |
| Profiling | Where time and memory go per stage, and the cost of telemetry | No, run by hand on the owner's machine (ADR-020, Stage 02) |

The unit layer is split by concern. [ydk:test:detection/detector.unit] covers detection, [ydk:test:extraction/extraction.unit] covers metadata and body, [ydk:test:extraction/exotic-language.unit] covers scripts, emoji, and direction, and [ydk:test:attachments/attachment.unit] covers attachments. The seed golden layer is [ydk:test:quality/seed-golden.integration], the realistic-sample layer is [ydk:test:quality/realistic-samples.integration], the internal corpus layer is [ydk:test:quality/internal-corpus.integration], and the telemetry layer is [ydk:test:observability/telemetry.unit], which checks [ydk:req:observability/runtime-telemetry] with the real .NET listeners. The bulk score, the harvest probe, and the load test feed [ydk:nfr:reliability/golden-pass-rate], [ydk:nfr:reliability/real-world-harvest-no-throw], [ydk:nfr:performance/throughput-dop-sweep], and [ydk:nfr:performance/memory-footprint]. Profiling feeds [ydk:req:quality/profiling-report], and the telemetry cost benchmark feeds [ydk:nfr:observability/metrics-overhead].

Per the project owner (ADR-019), the unit, seed-golden, and realistic-sample layers run on both .NET 8 and .NET 10 in CI, and a failure on either fails the build ([ydk:req:quality/net8-multi-targeting]). The bulk golden gate runs on .NET 10; the developer tools stay on .NET 10 only. This is target behaviour for Stage 02.

## What is real and what is stubbed

- **Parser.** MimeKit is always real ([ydk:library:deps/mimekit]). Fixtures are written either by MimeKit's own message builder or as raw message text.
- **Attachment payloads.** Unit and golden tests use small stub files with the correct leading bytes. Real documents appear in the committed realistic sample set (target) and in the internal corpus.
- **Storage and network.** The library uses neither, so there is nothing to stub. Golden fixtures and realistic samples are read from the test output folder. Internal samples are read from the gitignored corpus folder when present.

## Rules

- Never mock the detector, the extractor, or MimeKit. Tests run the real code on real bytes.
- Never generate expected values from extractor output, and never copy them from prose. Golden values come from the authored catalog ([ydk:req:quality/golden-independence]); realistic-sample values come from the generator's inputs or the source's published contents ([ydk:req:quality/realistic-attachment-samples]).
- Every committed sample records its source and license.
- Emoji and script assertions compare exactly, with no normalisation ([ydk:req:content/unicode-fidelity]).
- Raw-text message fixtures must be converted to CRLF line endings in a way that is safe when the checkout already uses CRLF. See Known Gotchas in [project-rules.md](../project-rules.md).
- Assert on warning code prefixes, never on the free-text part ([ydk:crosscut:core/warning-format]).
- A test whose sample cannot be committed must show as skipped, naming the missing file, when the sample is absent. It must never pass without running (ADR-018; today such tests still pass vacuously, Stage 02).
- Re-run the bulk score, the harvest, and the load test after any MimeKit upgrade or any change to extraction rules, and commit the new reports.
- Telemetry tests share one process-wide measurement source, so they run one at a time and listen only to this library's name.
- An optimization carries its own before-and-after measurement and is never merged on the strength of a test pass alone ([ydk:req:quality/optimization-evidence]).
