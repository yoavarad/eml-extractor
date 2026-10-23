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

## Review (done, 1 round)
- cavecrew-reviewer: 0 red, 2 yellow (mbox test did not prove mbox branch; no body_decode_failed/HTML-direction test) -> fixed with MboxParseStopsAtNextFromLine, BodyDecodeContentFailureIsWarning, BodyDecodeBugPropagates, HtmlBodyContentDirectionUsesHtmlDir.
- Leftover (accepted): BodyStage bench now rethrows non-content exceptions (old copy swallowed all) and allocates a [] warnings list per call; BodyExtractor<->EmlExtractor.IsContentFailure reference cycle (harmless).
- Tests net8 380p/18s, net10 381p/17s; format OK; profiling bench dry run (net10) exit 0, 90 benchmarks (ExtractOverLimit throws by design, pre-existing).

## Follow-up: instance classes (done)
- Merged origin/main; MimeMessageParser/BodyExtractor now internal sealed instance classes, one instance each per EmlExtractor; benches use shared static instances. IsContentFailure move skipped (also used by PartCollector).
- build -warnaserror OK, format OK, tests net8 380p/18s, net10 381p/17s, GoldenKit 0 failures. Review 1 round (2 bench alloc findings fixed).
