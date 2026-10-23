# Task 146 orchestrator progress

## Plan (done)
- MimeMessageParser (internal static, matches HeaderMapper/BodySelector/PartCollector style): Options, Open, Parse, StartsWithMboxFrom; static ctor registers CachingEncodingProvider (dedup'd by runtime) so direct harness calls keep encoding semantics.
- BodyExtractor (internal static): ExtractBody(message, subject, warnings) -> BodyContent record struct; ExtractAttachments(message, body, warnings).
- EmlExtractor: orchestration only. Body-stage StageFault moves before BodySelector.Select (Select is pure; unobservable).
- Call sites: benches StageBenchmarks/ProfileSamples, InputLifetimeTests. New tests MimeMessageParserTests, BodyExtractorTests.

## Build (done)
- New: src/EmlExtraction/MimeMessageParser.cs, BodyExtractor.cs; tests MimeMessageParserTests, BodyExtractorTests.
- EmlExtractor: orchestration only. Benches + InputLifetimeTests call sites updated; BodyStage bench now calls BodyExtractor.ExtractBody.
- build -warnaserror OK, format OK, tests net8 376p/18s, net10 377p/17s, GoldenKit 1212/1212 (13142 checks).
