# Graph Report - eml-extraction  (2026-10-28)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 2138 nodes · 4228 edges · 159 communities (121 shown, 38 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 454 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `a396e5cc`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Golden Case Catalog Builder
- Stage Fault Wrapping Tests
- Sweep Report and Baseline Compare
- Detector Legacy Equivalence Tests
- Sweep Hardware and Math Tests
- Realistic Sample Generator
- Attachment Part Collection
- Text Direction Detection
- Telemetry Behavior Tests
- Profile Copy Factor Command
- Content Catch Site Tests
- Internal Corpus Tests
- Golden Spec Schema
- Header and Body Extraction Tests
- Benchmark Corpus and Throughput Runner
- Attachment Extraction Tests
- Input Lifetime Tests
- Bench Sweep Namespace Imports
- Per-Stage Profiling Benchmarks
- Cached Encoding Provider
- Exotic Language Round-Trip Tests
- Project Files and Packages
- CSharp Capability Assessment ADR
- GoldenKit Corpus Probe Stats
- Header Metadata Mapping
- GoldenKit Namespace Imports
- Profile Report Oct 18 and Body Selection
- Profile Trace Command
- License Audit and Guard Scripts
- README and Input Size Limit
- Benchmark and Telemetry Imports
- Core Library Namespaces
- Test Project Namespace Imports
- Exception Type Tests
- ADR Data-Driven Optimization
- Body Extractor Tests
- Golden Set Test Runner
- Golden Score Evaluator
- ADR Load Test Method
- ADR Profiling Method
- ADR Runtime Metrics and Tracing
- Attachment Decoding Tests
- Profile Sample Loader
- Realistic Sample Manifest Tests
- MIME Message Parser
- ADR MimeKit Parser Choice
- Large File Sizing Math
- CSharp Best Practices Skill
- Extraction Exception Policy
- Profile Report Oct 23 Closing
- Extraction Spec
- GoldenKit Inspect Command
- Large File Load Command
- Large File Math Tests
- Async and Concurrency Guidelines
- EF Core Data Access Guidelines
- Read-Ahead and Limited Streams
- Parse and Attachments Stage Benchmarks
- Body and Attachment Extraction Core
- Realistic Samples JSON and OpenXML
- ADR Prefer HTML Main Content
- ADR Warnings Not Exceptions
- ADR Text Direction Reporting
- ADR Multi-Target Net8 Net10
- Project Rules and Testing Strategy
- Attachments Spec
- Extractor Entry and Size Limit
- Retrospective Task 12
- Retrospective Task 8
- CSharp Playbook Overview
- Extraction Telemetry Pipeline
- ADR Authored Golden Set
- ADR Pin Pkcs Package
- ADR CI Golden Score Gate
- ADR Canonical Metadata Keys
- ADR MIT License
- Golden Set Plan
- Requirements Coverage Checklist
- Project Overview Spec
- Open Team Questions
- Public API Parity Tests
- Retrospective Task 102
- Retrospective Task 11
- Retrospective Task 13
- Retrospective Task 14
- Retrospective Task 15
- Pull Request Checklist Guidelines
- ADR Custom Component Schemas
- Stream Input Tests
- Cross-Cutting Concerns Spec
- Quality Evidence Spec
- Third-Party Notices
- Stream Test Doubles
- Trace GC Support Helpers
- Testing Guidelines
- Naming and Maintainability Guidelines
- Security Baseline Guidelines
- Logging and Observability Guidelines
- Configuration and DI Guidelines
- Extraction Request Model
- Detection Spec
- README Usage Interface
- Task 146 Progress Log
- Task 32 PR Body
- Nullability and Validation Guidelines
- API Contract Guidelines
- JSON Serialization Guidelines
- Resource and Time Guidelines
- Scope Spec
- Task Issue Template
- Pull Request Template
- Extraction Result and Benchmarks
- Retrospective Task 64
- Epic Issue Template
- Task Completion Hook
- Realistic Corpus Manifest
- Glossary
- Story Issue Template
- Profile Corpus Shell Script
- Commit Message Hook
- Pre-Commit Hook
- Pre-Push Hook
- Task 106 Progress Log
- Task 32 Progress Log
- Task 68 Progress Log
- Task 71 Progress Log
- Task 72 Progress Log
- Task 75 Progress Log
- EML Format Detector
- Telemetry Overhead Benchmarks
- ADR Shared Extractor Contract
- Detector Accept Reject Tests
- Legacy Detector Reference
- Namespace Layout Tests
- ADR Stream Input
- Task 149 Progress Log
- Detector Interface
- Activity Listener Exception Test

## God Nodes (most connected - your core abstractions)
1. `EmlExtractor` - 67 edges
2. `EmlExtraction.Logics` - 43 edges
3. `EmlDetector` - 41 edges
4. `CaseDef` - 36 edges
5. `EmlExtractionResult` - 32 edges
6. `EmlInputTooLargeException` - 32 edges
7. `EmlExtractionRequest` - 31 edges
8. `EmlExtractionException` - 31 edges
9. `EmlExtraction.Tests` - 31 edges
10. `TelemetryTests` - 28 edges

## Surprising Connections (you probably didn't know these)
- `Context` --references--> `EmlExtractionRequest`  [INFERRED]
  docs/adrs/024-stream-input.md → src/EmlExtraction/Models/EmlExtractionRequest.cs
- `1. Load — max files per second on this hardware` --references--> `ThroughputRunner`  [INFERRED]
  docs/requirements.md → benches/EmlExtraction.Bench/ThroughputRunner.cs
- `Detection (is this an EML)` --references--> `EmlDetector`  [INFERRED]
  docs/research/csharp-capability-assessment.md → src/EmlExtraction/Logics/EmlDetector.cs
- `GC, pause and LOH (sweep traces, synthetic set, 2 phases of about 1.5 s each)` --references--> `EmlInputTooLargeException`  [INFERRED]
  docs/perf/profile-2026-10-18.md → src/EmlExtraction/Exceptions/EmlInputTooLargeException.cs
- `Large-file pass (net10.0 unless noted, Workstation, DOP 1, 524,288,000)` --references--> `EmlInputTooLargeException`  [INFERRED]
  docs/perf/profile-2026-10-23.md → src/EmlExtraction/Exceptions/EmlInputTooLargeException.cs

## Import Cycles
- None detected.

## Communities (159 total, 38 thin omitted)

### Community 0 - "Golden Case Catalog Builder"
Cohesion: 0.07
Nodes (49): Disposition, ADR-014: xfields joins literal `X_` headers and lives in the metadata, Alternatives, Consequences, Context, Decision, Evidence, Status (+41 more)

### Community 1 - "Stage Fault Wrapping Tests"
Cohesion: 0.20
Nodes (10): Func, OutOfMemoryException, ArgumentNullException, Exception, Fact, InlineData, MimeMessage, SuppressMessage (+2 more)

### Community 2 - "Sweep Report and Baseline Compare"
Cohesion: 0.05
Nodes (36): GeneratedRegex, IReadOnlyList, List, Regex, BaselineCompare, CompareResult, CompareCommand, Action (+28 more)

### Community 3 - "Detector Legacy Equivalence Tests"
Cohesion: 0.24
Nodes (6): Fact, IEnumerable, MemberData, Theory, TheoryData, DetectorEquivalenceTests

### Community 4 - "Sweep Hardware and Math Tests"
Cohesion: 0.06
Nodes (21): IEnumerable, CpuInfoParser, HardwareProbe, HardwareProbeException, List, ReadOnlySpan, MboxSplitter, From (+13 more)

### Community 5 - "Realistic Sample Generator"
Cohesion: 0.06
Nodes (28): Data, Dir, Eml, Multipart, Sha, Fact, MimeMessage, MimePart (+20 more)

### Community 6 - "Attachment Part Collection"
Cohesion: 0.06
Nodes (37): Additional location info, ADR-013: FullPath is the container path; nested messages stay opaque, Alternatives, Consequences, Context, Decision, Evidence, FullPath (+29 more)

### Community 7 - "Text Direction Detection"
Cohesion: 0.05
Nodes (36): Committed baseline and regression rule (ADR-015), DOP sweep (ADR-015), DOP sweep JSON schema (v1), Files / second (1 thread), Hardware, How to scale resources, Large-file pass and sizing formula (ADR-015), Nightly synthetic job (+28 more)

### Community 8 - "Telemetry Behavior Tests"
Cohesion: 0.13
Nodes (20): AssemblyInformationalVersionAttribute, IDisposable, KeyValuePair, Measurement, Activity, ActivityListener, Dictionary, Fact (+12 more)

### Community 9 - "Profile Copy Factor Command"
Cohesion: 0.06
Nodes (29): ConcurrentDictionary, CopyFactorColumn, AlwaysShow, Category, ColumnName, Id, IsNumeric, Legend (+21 more)

### Community 10 - "Content Catch Site Tests"
Cohesion: 0.15
Nodes (12): MimeContent, Exception, Fact, MimeMessage, SeekOrigin, ContentCatchSiteTests, ThrowingStream, CanRead (+4 more)

### Community 11 - "Internal Corpus Tests"
Cohesion: 0.10
Nodes (18): ADR-008: Flat, opaque attachments — no recursion, TNEF unwrapped, Alternatives, Consequences, Context, Decision, Evidence, Open questions, Status (+10 more)

### Community 12 - "Golden Spec Schema"
Cohesion: 0.06
Nodes (30): GoldenKit.Schema, JsonConverter, system_text_json_serialization, Generator, Dictionary, List, GoldenExpect, AttachmentCount (+22 more)

### Community 13 - "Header and Body Extraction Tests"
Cohesion: 0.14
Nodes (12): Evidence, As many headers as possible, Classic fields (To, From, etc.), Metadata fields, `X_` fields concatenated into `xfields`, ReadOnlyMemory, Stream, ArgumentNullException (+4 more)

### Community 14 - "Benchmark Corpus and Throughput Runner"
Cohesion: 0.24
Nodes (6): MimeMessage, Corpus, Sample, GlobalSetup, GlobalSetup, ThroughputRunner

### Community 15 - "Attachment Extraction Tests"
Cohesion: 0.15
Nodes (8): 3. Correctness B — attachment variety, BinaryWriter, Fact, InlineData, MimePart, Theory, AttachmentTests, SampleFactory

### Community 16 - "Input Lifetime Tests"
Cohesion: 0.13
Nodes (11): Input, MethodImpl, Result, Fact, MemberData, MimePart, Theory, TheoryData (+3 more)

### Community 17 - "Bench Sweep Namespace Imports"
Cohesion: 0.09
Nodes (16): benchmarkdotnet_columns, benchmarkdotnet_configs, benchmarkdotnet_diagnosers, benchmarkdotnet_environments, benchmarkdotnet_exporters_json, benchmarkdotnet_reports, benchmarkdotnet_running, EmlExtraction.Bench.Profile (+8 more)

### Community 18 - "Per-Stage Profiling Benchmarks"
Cohesion: 0.11
Nodes (15): Benchmark, Dictionary, GlobalCleanup, MemoryStream, MimeMessage, DetectStage, MetadataStage, ParseStage (+7 more)

### Community 19 - "Cached Encoding Provider"
Cohesion: 0.13
Nodes (18): DecoderExceptionFallback, DecoderReplacementFallback, EncoderExceptionFallback, EncodingInfo, EncodingProvider, ConcurrentDictionary, Encoding, IEnumerable (+10 more)

### Community 20 - "Exotic Language Round-Trip Tests"
Cohesion: 0.16
Nodes (10): Edge cases catalog, Public sources used, Fact, InlineData, MemberData, MimeMessage, Theory, TheoryData (+2 more)

### Community 21 - "Project Files and Packages"
Cohesion: 0.09
Nodes (20): MimeKit, Microsoft.NET.Sdk, BenchmarkDotNet, coverlet.collector, DocumentFormat.OpenXml, Microsoft.NET.Test.Sdk, System.Reflection.MetadataLoadContext, xunit (+12 more)

### Community 22 - "CSharp Capability Assessment ADR"
Cohesion: 0.08
Nodes (22): ADR-001: C# / .NET 10 class library stack, Alternatives, Consequences, Context, Decision, Evidence, Status, C# capability assessment - MimeKit on .NET vs Python, Java, Go (+14 more)

### Community 23 - "GoldenKit Corpus Probe Stats"
Cohesion: 0.17
Nodes (11): HashSet, IEnumerable, MimeMessage, Probe, ConcurrentDictionary, JsonSerializerOptions, List, TimeSpan (+3 more)

### Community 24 - "Header Metadata Mapping"
Cohesion: 0.11
Nodes (14): Subject, BodyStage, Content, ADR-005: Always emit from, to, cc, and bcc metadata keys, Alternatives, Consequences, Context, Decision (+6 more)

### Community 25 - "GoldenKit Namespace Imports"
Cohesion: 0.17
Nodes (4): GoldenKit, system, system_collections_concurrent, system_text

### Community 26 - "Profile Report Oct 18 and Body Selection"
Cohesion: 0.12
Nodes (17): Attachment copy factor, Comparison with `docs/perf-baseline.json`, Data caveats (read first), GC, pause and LOH (sweep traces, synthetic set, 2 phases of about 1.5 s each), Hardware and runs, Inputs, Per-stage CPU share (share of extraction CPU, `Extract(Request)` = 100%), Profile report 2026-10-18 (+9 more)

### Community 27 - "Profile Trace Command"
Cohesion: 0.21
Nodes (11): Dictionary, List, TimeSpan, GcStats, Manifest, ProfileTraceCommand, RunRecord, HardwareBlock (+3 more)

### Community 28 - "License Audit and Guard Scripts"
Cohesion: 0.11
Nodes (18): YDK unified guard hook — all guard checks in one script., _read_stdin(), glob, os, pathlib, re, subprocess, sys (+10 more)

### Community 29 - "README and Input Size Limit"
Cohesion: 0.12
Nodes (17): ArgumentException, 1. Load — max files per second, 2. Correctness A — emoji and exotic languages, 3. Correctness B — attachment mix, EML extraction, Errors, Golden set, Layout (+9 more)

### Community 30 - "Benchmark and Telemetry Imports"
Cohesion: 0.17
Nodes (8): benchmarkdotnet_attributes, benchmarkdotnet_jobs, EmlExtraction.Bench, system_diagnostics, system_diagnostics_metrics, system_reflection, system_runtime_interopservices, TelemetryCollectionDefinition

### Community 31 - "Core Library Namespaces"
Cohesion: 0.30
Nodes (4): EmlExtraction.Exceptions, EmlExtraction.Logics, EmlExtraction.Models, system_diagnostics_codeanalysis

### Community 32 - "Test Project Namespace Imports"
Cohesion: 0.14
Nodes (7): EmlExtraction.Tests, EmlExtraction.Abstractions, mimekit, mimekit_tnef, mimekit_utils, system_runtime_compilerservices, system_security_cryptography

### Community 33 - "Exception Type Tests"
Cohesion: 0.22
Nodes (7): ArgumentException, ArgumentNullException, ArgumentOutOfRangeException, Fact, InlineData, Theory, ExceptionTypeTests

### Community 34 - "ADR Data-Driven Optimization"
Cohesion: 0.13
Nodes (14): ADR-022: Data-driven optimization process, Alternatives, Candidate areas (hypotheses only, not commitments), Closing, Consequences, Context, Decision, Evidence (+6 more)

### Community 35 - "Body Extractor Tests"
Cohesion: 0.27
Nodes (7): BodyExtractor, Exception, Fact, InvalidCastException, Span, BodyExtractorTests, ThrowingStream

### Community 36 - "Golden Set Test Runner"
Cohesion: 0.14
Nodes (11): Fact, GoldenAttachment, GoldenExpect, GoldenField, GoldenSpecFile, IEnumerable, JsonSerializerOptions, MemberData (+3 more)

### Community 37 - "Golden Score Evaluator"
Cohesion: 0.16
Nodes (14): CaseScore, GoldenExpect, GoldenField, GoldenSpecFile, IEnumerable, JsonSerializerOptions, List, CaseScore (+6 more)

### Community 38 - "ADR Load Test Method"
Cohesion: 0.14
Nodes (13): ADR-015: Load-test method - DOP sweep on real mail, measured memory, committed baseline, Alternatives, Baseline and regression rule, Consequences, Context, Decision, Evidence, Garbage collector (+5 more)

### Community 39 - "ADR Profiling Method"
Cohesion: 0.14
Nodes (13): ADR-020: Profiling method - per-stage CPU, allocation, and GC hotspots on the load-test corpus, Alternatives, Consequences, Context, Decision, Evidence, Inputs, Output (+5 more)

### Community 40 - "ADR Runtime Metrics and Tracing"
Cohesion: 0.14
Nodes (13): Activities (tracing), ADR-021: Runtime metrics and tracing with BCL Meter and ActivitySource, Alternatives, Consequences, Context, Decision, Evidence, Instruments (+5 more)

### Community 42 - "Attachment Decoding Tests"
Cohesion: 0.33
Nodes (5): Fact, InlineData, IReadOnlyList, Theory, AttachmentDecodeTests

### Community 43 - "Profile Sample Loader"
Cohesion: 0.23
Nodes (7): IEnumerable, IReadOnlyList, List, Name, TnefPart, ProfileSamples, Bytes

### Community 44 - "Realistic Sample Manifest Tests"
Cohesion: 0.21
Nodes (6): JsonElement, Fact, IEnumerable, MemberData, Theory, RealisticSampleExtractionTests

### Community 45 - "MIME Message Parser"
Cohesion: 0.22
Nodes (7): MemoryStream, ParserOptions, ReadOnlyMemory, MimeMessageParser, Fact, ParserOptions, MimeMessageParserTests

### Community 46 - "ADR MimeKit Parser Choice"
Cohesion: 0.20
Nodes (9): ADR-002: MimeKit as the MIME parser, configured leniently, Alternatives, Consequences, Context, Decision, Evidence, Open questions, Status (+1 more)

### Community 47 - "Large File Sizing Math"
Cohesion: 0.28
Nodes (3): IReadOnlyList, LargeFileMath, MinMessageBytes

### Community 48 - "CSharp Best Practices Skill"
Cohesion: 0.18
Nodes (10): Authorship and license, C# Development Best Practices, C#/.NET guardrails, Definition of done, Required operating procedure, Source of truth, Workflow: fix a bug, Workflow: implement a feature (+2 more)

### Community 49 - "Extraction Exception Policy"
Cohesion: 0.13
Nodes (15): ADR-023: Throw for misuse, limits, and library bugs; warn for content, Alternatives, Consequences, Context, Decision, Detection, Evidence, New exception types (+7 more)

### Community 50 - "Profile Report Oct 23 Closing"
Cohesion: 0.18
Nodes (10): ADR-022 targets, Comparison with `docs/perf-baseline.json`, Delta table per optimization, GC pause (profile-trace, synthetic set, pause ms over the whole run; run 1 / run 2), Hardware and runs, Large-file pass (net10.0 unless noted, Workstation, DOP 1, 524,288,000), Leftovers for the owner, Limits re-check (#51) (+2 more)

### Community 51 - "Extraction Spec"
Cohesion: 0.18
Nodes (11): Custom headers, Evidence, Exotic languages and emoji, Extraction - metadata, main content, direction, Failure handling, Input size, Main content, Metadata (+3 more)

### Community 53 - "Large File Load Command"
Cohesion: 0.36
Nodes (4): LargeFileCommand, Pass, M, Pass

### Community 54 - "Large File Math Tests"
Cohesion: 0.27
Nodes (6): ArgumentException, ArgumentOutOfRangeException, Fact, InlineData, Theory, LargeFileMathTests

### Community 55 - "Async and Concurrency Guidelines"
Cohesion: 0.20
Nodes (10): 4.1 Keep asynchronous code asynchronous, 4.2 Pass cancellation tokens end to end, 4.3 Set explicit timeouts for outbound calls, 4.4 Do not use `async void` except for event handlers, 4.5 Do not fire-and-forget background work, 4.6 Bound parallel work, 4.7 Protect shared mutable state, 4.8 Use `IAsyncEnumerable<T>` for genuine streams (+2 more)

### Community 56 - "EF Core Data Access Guidelines"
Cohesion: 0.20
Nodes (10): 9.1 Prevent N+1 queries, 9.2 Paginate collection endpoints and queries, 9.3 Parameterize SQL, 9.4 Keep transactions short and database-focused, 9.5 Use `AsNoTracking()` for read-only EF Core queries, 9.6 Save once per unit of work, not once per row, 9.7 Understand deferred execution and enumeration, 9.8 Keep `IQueryable` inside the data-access boundary (+2 more)

### Community 57 - "Read-Ahead and Limited Streams"
Cohesion: 0.05
Nodes (28): SeekOrigin, Span, SuppressMessage, LimitedReadStream, BytesRead, CanRead, CanSeek, CanWrite (+20 more)

### Community 58 - "Parse and Attachments Stage Benchmarks"
Cohesion: 0.22
Nodes (6): GlobalSetup, TextPart, AttachmentsStage, MimeMessage, ReadOnlySpan, Stream

### Community 59 - "Body and Attachment Extraction Core"
Cohesion: 0.24
Nodes (8): IReadOnlyList, BodyContent, IReadOnlyList, List, MimeMessage, SuppressMessage, TextPart, BodyContent

### Community 60 - "Realistic Samples JSON and OpenXML"
Cohesion: 0.14
Nodes (10): RealisticSamples, documentformat_openxml, documentformat_openxml_packaging, documentformat_openxml_presentation, documentformat_openxml_spreadsheet, documentformat_openxml_wordprocessing, system_io_compression, system_text_json (+2 more)

### Community 61 - "ADR Prefer HTML Main Content"
Cohesion: 0.22
Nodes (8): ADR-003: Prefer HTML as main content in multipart/alternative, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 62 - "ADR Warnings Not Exceptions"
Cohesion: 0.22
Nodes (8): ADR-004: Record warnings instead of throwing on malformed MIME, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 63 - "ADR Text Direction Reporting"
Cohesion: 0.22
Nodes (8): ADR-006: Report subject and content text direction as ltr/rtl, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 64 - "ADR Multi-Target Net8 Net10"
Cohesion: 0.22
Nodes (8): ADR-019: Multi-target net8.0 and net10.0, Alternatives, Consequences, Context, Decision, Evidence, Risks the Stage 02 task must verify, Status

### Community 65 - "Project Rules and Testing Strategy"
Cohesion: 0.22
Nodes (7): Known Gotchas, Product Context, Project Rules, Layers, Rules, Testing strategy, What is real and what is stubbed

### Community 66 - "Attachments Spec"
Cohesion: 0.22
Nodes (9): Attachments, Evidence, Failures, Full path: where the file sat in the message, Naming rules, Outlook TNEF wrappers, Pasted images, What the caller gets (+1 more)

### Community 67 - "Extractor Entry and Size Limit"
Cohesion: 0.17
Nodes (13): AsyncLocal, Decision, Conventions, IEmlExtractor, Action, EmlExtractor, StageFault, CheckedExtractor (+5 more)

### Community 68 - "Retrospective Task 12"
Cohesion: 0.22
Nodes (8): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 12, Rule suggestions, Shipped, Summary, Template suggestions

### Community 69 - "Retrospective Task 8"
Cohesion: 0.22
Nodes (8): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 8, Rule suggestions, Shipped, Summary, Template suggestions

### Community 70 - "CSharp Playbook Overview"
Cohesion: 0.20
Nodes (9): 14. Performance principles, 15. Tooling and automated enforcement, 17. Definition of done, 18. Keeping the playbook useful, 19. Relationship to the agent skill, 1.1 Defaults for repositories generated by `dotnet-webapi-clean`, 1. How to apply this playbook, C# / .NET Engineering Best Practices (+1 more)

### Community 71 - "Extraction Telemetry Pipeline"
Cohesion: 0.14
Nodes (14): ActivitySource, Counter, Histogram, Meter, Activity, Exception, EmlExtractionTelemetry, ExtractScope (+6 more)

### Community 72 - "ADR Authored Golden Set"
Cohesion: 0.25
Nodes (7): ADR-007: Authored golden set as the quality bar, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 73 - "ADR Pin Pkcs Package"
Cohesion: 0.25
Nodes (7): ADR-009: Pin System.Security.Cryptography.Pkcs 10.0.13, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 74 - "ADR CI Golden Score Gate"
Cohesion: 0.25
Nodes (7): ADR-011: Gate CI on the full golden score, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 75 - "ADR Canonical Metadata Keys"
Cohesion: 0.25
Nodes (7): ADR-016: One canonical metadata key per header, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 76 - "ADR MIT License"
Cohesion: 0.25
Nodes (7): ADR-017: MIT license with a third-party notices file, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 77 - "Golden Set Plan"
Cohesion: 0.25
Nodes (8): 1,000 core (original plan), +200 optional RFC extras, File types that must appear (attachments), Golden set plan — 1,000 core + 200 RFC extras, Tag vocabulary (scorer slices), Tooling, What we will not gold, Why 1,200

### Community 78 - "Requirements Coverage Checklist"
Cohesion: 0.33
Nodes (6): 1. Load — max files per second on this hardware, 2. Correctness A — emoji and exotic languages, Content, Out of original spec (implemented anyway), Requirements (English) — coverage checklist, Tests

### Community 79 - "Project Overview Spec"
Cohesion: 0.25
Nodes (8): Dependencies and licensing, EML Extraction - Overview, End-to-end flow, Language choice, Problem statement, Spec map, Success criteria, Who uses it and how

### Community 80 - "Open Team Questions"
Cohesion: 0.25
Nodes (8): 1. Shared extractor contract (ADR-012), 2. Template and factory integration, 3. Target framework, 4. License, 5. `X_` headers in internal mail, 6. Telemetry naming conventions (ADR-021), Error model divergence, Open questions for the team

### Community 81 - "Public API Parity Tests"
Cohesion: 0.39
Nodes (4): MemberInfo, Fact, List, PublicApiTests

### Community 82 - "Retrospective Task 102"
Cohesion: 0.25
Nodes (7): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 102, Rule suggestions, Shipped, Summary

### Community 83 - "Retrospective Task 11"
Cohesion: 0.25
Nodes (7): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 11, Rule suggestions, Shipped, Summary

### Community 84 - "Retrospective Task 13"
Cohesion: 0.25
Nodes (7): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 13, Rule suggestions, Shipped, Summary

### Community 85 - "Retrospective Task 14"
Cohesion: 0.25
Nodes (7): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 14, Rule suggestions, Shipped, Summary

### Community 86 - "Retrospective Task 15"
Cohesion: 0.25
Nodes (7): AI Analysis, Analysis (Claude Code session, no API provider), Patterns, Retrospective: 15, Rule suggestions, Shipped, Summary

### Community 87 - "Pull Request Checklist Guidelines"
Cohesion: 0.29
Nodes (7): 16. Pull request checklist, Async and concurrency, Configuration and resources, Contracts and data, Correctness and errors, Logging and diagnostics, Tests and maintainability

### Community 88 - "ADR Custom Component Schemas"
Cohesion: 0.29
Nodes (6): ADR-010: Custom `library` and `warning` component schemas, Alternatives, Consequences, Context, Decision, Status

### Community 89 - "Stream Input Tests"
Cohesion: 0.13
Nodes (11): ArgumentNullException, Fact, NonSeekableStream, BytesRead, CanRead, CanSeek, CanWrite, Disposed (+3 more)

### Community 90 - "Cross-Cutting Concerns Spec"
Cohesion: 0.29
Nodes (7): Cross-cutting concerns, Diagnostics, Exceptions, Failure and abuse cases, Observability, Runtime surface, Timestamps and text

### Community 91 - "Quality Evidence Spec"
Cohesion: 0.29
Nodes (7): Authored golden set, Correctness A - emoji and exotic languages, Correctness B - realistic attachment samples, Load test, Profiling and optimization, Quality evidence - golden set, realistic samples, harvest, load test, Real-world harvest

### Community 92 - "Third-Party Notices"
Cohesion: 0.29
Nodes (6): Apache POI NOTICE, Apache POI test data (Apache-2.0), Committed samples (ADR-018), Runtime packages (shipped with the library), Test, bench and tooling packages (not shipped), Third-party notices

### Community 93 - "Stream Test Doubles"
Cohesion: 0.11
Nodes (11): MemoryStream, SeekOrigin, FailingStream, CanRead, CanSeek, CanWrite, Length, Position (+3 more)

### Community 95 - "Testing Guidelines"
Cohesion: 0.33
Nodes (6): 11.1 Test observable behavior, not implementation details, 11.2 Keep expected values independent, 11.3 One behavior per test; use descriptive names, 11.4 Every bug fix should include a regression test, 11.5 Test the boundaries and failure paths, 11. Testing

### Community 96 - "Naming and Maintainability Guidelines"
Cohesion: 0.33
Nodes (6): 12.1 Name things for the reader, 12.2 Delete dead code, 12.3 Comments explain why, not what, 12.4 Keep methods focused and at one level of abstraction, 12.5 Keep layers and responsibilities clear, 12. Naming, structure, and maintainability

### Community 97 - "Security Baseline Guidelines"
Cohesion: 0.33
Nodes (6): 13.1 Treat all external input as untrusted, 13.2 Check authorization and resource ownership, 13.3 Do not construct executable content from raw input, 13.4 Handle passwords with a purpose-built password hasher, 13.5 Keep dependencies maintained, 13. Security baseline

### Community 98 - "Logging and Observability Guidelines"
Cohesion: 0.33
Nodes (6): 3.1 Use structured logging, 3.2 Choose log levels deliberately, 3.3 Never log secrets or sensitive data, 3.4 Make requests traceable, 3.5 Avoid logging inside hot loops, 3. Logging and observability

### Community 99 - "Configuration and DI Guidelines"
Cohesion: 0.33
Nodes (6): 7.1 Keep environment-specific values out of code, 7.2 Never commit secrets, 7.3 Validate configuration at startup, 7.4 Use dependency injection correctly, 7.5 Reuse HTTP clients safely, 7. Configuration, dependency injection, and secrets

### Community 100 - "Extraction Request Model"
Cohesion: 0.12
Nodes (12): ReadOnlyMemory, Stream, EmlExtractionRequest, FileBytes, FileName, HasFileBytes, InputStream, IReadOnlyList (+4 more)

### Community 101 - "Detection Spec"
Cohesion: 0.33
Nodes (6): Detection, Evidence, How it decides, What it does not do, What it returns, Why it exists

### Community 102 - "README Usage Interface"
Cohesion: 0.33
Nodes (6): 1. Detect, 2. Extract, Content, Interface, Metadata, Nested files (attachments)

### Community 103 - "Task 146 Progress Log"
Cohesion: 0.33
Nodes (5): Build (done), Follow-up: instance classes (done), Plan (done), Review (done, 1 round), Task 146 orchestrator progress

### Community 104 - "Task 32 PR Body"
Cohesion: 0.33
Nodes (5): Reviewer leftovers (not fixed), Summary, Test Plan, Verification Plugins, Verification Results

### Community 105 - "Nullability and Validation Guidelines"
Cohesion: 0.40
Nodes (5): 5.1 Enable nullable reference types, 5.2 Validate untrusted data at boundaries, 5.3 Make invalid states hard to represent, 5.4 Use framework capabilities before inventing replacements, 5. Nullability, validation, and domain modeling

### Community 106 - "API Contract Guidelines"
Cohesion: 0.40
Nodes (5): 6.1 Never expose persistence entities directly from an API, 6.2 Treat published API contracts as compatibility commitments, 6.3 Use decimal types for money, 6.4 Use consistent error responses and status codes, 6. API contracts and data models

### Community 107 - "JSON Serialization Guidelines"
Cohesion: 0.40
Nodes (5): 8.1 Configure serialization once and reuse it, 8.2 Be explicit about contract-sensitive behavior, 8.3 Prefer one serialization system per boundary, 8.4 Consider cycles and untrusted polymorphic data, 8. JSON serialization

### Community 108 - "Resource and Time Guidelines"
Cohesion: 0.50
Nodes (4): 10.1 Dispose resources deterministically, 10.2 Store and transmit UTC instants, 10.3 Inject time-dependent behavior, 10. Resource management and time

### Community 109 - "Scope Spec"
Cohesion: 0.50
Nodes (4): DEFERRED, IN scope, OUT of scope, Scope

### Community 110 - "Task Issue Template"
Cohesion: 0.50
Nodes (3): Acceptance Criteria, Description, Test Strategy

### Community 111 - "Pull Request Template"
Cohesion: 0.50
Nodes (3): Summary, Test Plan, Verification Results

### Community 112 - "Extraction Result and Benchmarks"
Cohesion: 0.16
Nodes (10): Benchmark, EmlBenchmarks, 2. Extract function, IReadOnlyDictionary, ReadOnlyMemory, Stream, IReadOnlyList, EmlExtractionResult (+2 more)

### Community 113 - "Retrospective Task 64"
Cohesion: 0.50
Nodes (3): AI Analysis, Retrospective: 64, Shipped

### Community 142 - "EML Format Detector"
Cohesion: 0.32
Nodes (3): ReadOnlySpan, SuppressMessage, EmlDetector

### Community 143 - "Telemetry Overhead Benchmarks"
Cohesion: 0.16
Nodes (10): ActivityListener, Benchmark, GlobalCleanup, MeterListener, ListenerMode, Activity, Meter, None (+2 more)

### Community 144 - "ADR Shared Extractor Contract"
Cohesion: 0.15
Nodes (13): ADR-012: Shared team extractor contract (request, detection result, subfile names), Alternatives, Amendment: source layout and namespaces (2026-10-23, task #149), Consequences, Context, Decision, Detection, Error model (+5 more)

### Community 145 - "Detector Accept Reject Tests"
Cohesion: 0.42
Nodes (3): Fact, ReadOnlySpan, DetectorTests

### Community 146 - "Legacy Detector Reference"
Cohesion: 0.52
Nodes (3): HashSet, ReadOnlySpan, Legacy

### Community 147 - "Namespace Layout Tests"
Cohesion: 0.43
Nodes (3): Dictionary, Fact, NamespaceLayoutTests

### Community 148 - "ADR Stream Input"
Cohesion: 0.33
Nodes (5): ADR-024: Stream input, parsed straight from the caller's stream, Consequences, Context, Status, What streaming saves and what it does not

### Community 149 - "Task 149 Progress Log"
Cohesion: 0.33
Nodes (5): Build (done), Leftover fixes (done), Plan (done), Review round 1 (done), Task 149 orchestrator progress

### Community 150 - "Detector Interface"
Cohesion: 0.40
Nodes (4): 1. Detect function, Interface, ReadOnlySpan, IEmlDetector

### Community 151 - "Activity Listener Exception Test"
Cohesion: 0.50
Nodes (3): InlineData, InvalidCastException, Theory

## Knowledge Gaps
- **552 isolated node(s):** `NonParallelCollectionDefinition`, `TelemetryCollectionDefinition`, `Eml`, `Expect`, `Id` (+547 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 917 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **38 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EmlExtractor` connect `Extractor Entry and Size Limit` to `Stage Fault Wrapping Tests`, `Sweep Report and Baseline Compare`, `Attachment Part Collection`, `Text Direction Detection`, `Telemetry Behavior Tests`, `Content Catch Site Tests`, `Benchmark Corpus and Throughput Runner`, `Telemetry Overhead Benchmarks`, `ADR Shared Extractor Contract`, `Per-Stage Profiling Benchmarks`, `GoldenKit Corpus Probe Stats`, `README and Input Size Limit`, `Core Library Namespaces`, `Exception Type Tests`, `Body Extractor Tests`, `Golden Score Evaluator`, `ADR Runtime Metrics and Tracing`, `MIME Message Parser`, `GoldenKit Inspect Command`, `Large File Load Command`, `Large File Math Tests`, `ADR Warnings Not Exceptions`, `Extraction Telemetry Pipeline`, `ADR Authored Golden Set`, `Golden Set Plan`, `Stream Input Tests`, `Extraction Request Model`, `Extraction Result and Benchmarks`?**
  _High betweenness centrality (0.168) - this node is a cross-community bridge._
- **Why does `2. Error handling and exceptions` connect `Sweep Report and Baseline Compare` to `CSharp Playbook Overview`?**
  _High betweenness centrality (0.071) - this node is a cross-community bridge._
- **Are the 35 inferred relationships involving `EmlExtractor` (e.g. with `Consequences` and `Decision`) actually correct?**
  _`EmlExtractor` has 35 INFERRED edges - model-reasoned connections that need verification._
- **Are the 13 inferred relationships involving `EmlDetector` (e.g. with `Amendment: source layout and namespaces (2026-10-23, task #149)` and `Consequences`) actually correct?**
  _`EmlDetector` has 13 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `EmlExtractionResult` (e.g. with `Amendment: source layout and namespaces (2026-10-23, task #149)` and `2. Extract function`) actually correct?**
  _`EmlExtractionResult` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `NonParallelCollectionDefinition`, `TelemetryCollectionDefinition`, `Eml` to the rest of the system?**
  _552 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Golden Case Catalog Builder` be split into smaller, more focused modules?**
  _Cohesion score 0.06763717805151176 - nodes in this community are weakly interconnected._