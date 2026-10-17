# Testing strategy

## Layers

| Layer | What it proves | Runs in CI |
| --- | --- | --- |
| Unit | Each rule of detection, metadata, body choice, direction, and attachment collection, on messages built in code | Yes |
| Seed golden | Authored end-to-end expectations on 12 committed messages | Yes |
| Internal corpus | Behaviour on specific real public samples | Only when the samples are present locally; CI skips them |
| Bulk golden score | 1,200 authored cases across 48 tags | Not yet. Target is a CI gate at the 1212 of 1212 baseline (ADR-011); today it is run by hand |
| Harvest probe | No crash across every local public sample | No, run by hand |
| Throughput bench | Files per second on the current machine | No, run by hand |

The unit layer is split by concern. [ydk:test:detection/detector.unit] covers detection, [ydk:test:extraction/extraction.unit] covers metadata and body, [ydk:test:extraction/exotic-language.unit] covers scripts and direction, and [ydk:test:attachments/attachment.unit] covers attachments. The seed golden layer is [ydk:test:quality/seed-golden.integration] and the internal corpus layer is [ydk:test:quality/internal-corpus.integration]. The bulk score, the harvest probe, and the bench feed [ydk:nfr:reliability/golden-pass-rate], [ydk:nfr:reliability/real-world-harvest-no-throw], and the throughput baselines in [quality.md](quality.md).

## What is real and what is stubbed

- **Parser.** MimeKit is always real ([ydk:library:deps/mimekit]). Fixtures are written either by MimeKit's own message builder or as raw message text.
- **Attachment payloads.** Unit and golden tests use small stub files with the correct leading bytes. Real documents appear only in the internal corpus.
- **Storage and network.** The library uses neither, so there is nothing to stub. Golden fixtures are read from the test output folder. Internal samples are read from the gitignored corpus folder when present.

## Rules

- Never mock the detector, the extractor, or MimeKit. Tests run the real code on real bytes.
- Never generate golden expected values from extractor output ([ydk:req:quality/golden-independence]).
- Raw-text message fixtures must be converted to CRLF line endings in a way that is safe when the checkout already uses CRLF. See Known Gotchas in [project-rules.md](../project-rules.md).
- Assert on warning code prefixes, never on the free-text part ([ydk:crosscut:core/warning-format]).
- Internal-corpus tests must pass vacuously when their sample is missing, so CI never depends on files that cannot be committed.
- Re-run the bulk score and the harvest after any MimeKit upgrade or any change to extraction rules, and commit the new score report.
