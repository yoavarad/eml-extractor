# Task 149 orchestrator progress

## Plan (done)
- Abstractions: IEmlExtractor, IEmlDetector
- Models: EmlExtractionRequest, EmlExtractionResult, EmlDetectionResult, ExtractedAttachment
- Exceptions: EmlExtractionException, EmlInputTooLargeException
- Logics: EmlExtractor, EmlDetector, MimeMessageParser, BodyExtractor, BodySelector, PartCollector, HeaderMapper, TextDirection, CachingEncodingProvider, EmlExtractionTelemetry, LimitedReadStream, ReadAheadStream
- All 20 files fit; none unmapped. git mv + namespace line + explicit usings. Update tests/benches/tools usings, README, docs/specs, ADR-012 amendment, namespace-layout test.

## Build (done)
- 20 files git mv'd into Abstractions/Models/Exceptions/Logics, namespaces updated, explicit usings added (src/tests/benches/tools), GoldenKit Program.cs qualified ref fixed.
- New tests/EmlExtraction.Tests/NamespaceLayoutTests.cs.
- README usings block + error.type value + layout; ADR-012 amendment section.
- Build -warnaserror OK; format verify OK; tests 0 fail net8/net10 (TextDirectionTests.HtmlWithoutMarkupDirectionAllocatesNothing flaky alloc test, passes on rerun); GoldenKit 1212/1212.
- Note: error.type tag value for own exceptions now EmlExtraction.Exceptions.* (FullName); documented.
