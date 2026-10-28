# Graph Report - eml-extraction  (2026-10-28)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 1908 nodes · 4296 edges · 161 communities (104 shown, 57 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 496 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e559f909`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Extraction Request and Result Models
- Stage Fault Wrapping Tests
- Baseline Comparison Tests
- Detector Legacy Equivalence Tests
- Sweep Math and CPU Parsing
- Realistic Sample Generator
- Attachment Part Collection
- Text Direction Detection
- Telemetry Behavior Tests
- Caching Encoding Provider
- Content Catch Site Tests
- Internal Corpus Tests
- Golden Case Catalog Builder
- Header and Body Extraction Tests
- Test Project File Imports
- Attachment Naming Tests
- Input Lifetime Tests
- Bench Sweep Namespace Imports
- Per-Stage Profiling Benchmarks
- Theory Data-Driven Tests
- Exotic Language Round-Trip Tests
- Project Files and Packages
- CSharp Capability Assessment ADR
- GoldenKit Corpus Probe Stats
- Header Metadata Mapping
- GoldenKit Tool Entry Points
- README Overview
- Input Size Limiting Stream
- License Audit and Guard Scripts
- Non-Seekable Stream Test Double
- Benchmark and Telemetry Imports
- Core Library Namespaces
- Test Project Namespace Imports
- Throwing Stream Test Double
- ADR Data-Driven Optimization
- Body Extractor Tests
- Golden Requirements Checks
- Golden Score Evaluator
- ADR Load Test Method
- ADR Profiling Method
- ADR Runtime Metrics and Tracing
- Spec Documents Index
- Attachment Decoding Tests
- Mbox Splitting and Sample Loading
- Realistic Sample Manifest Tests
- MIME Message Parser
- ADR MimeKit Parser Choice
- Large File Sizing Math
- CSharp Best Practices Skill
- Extraction Exception Types
- Profile Report Oct 23 Closing
- Extraction Spec
- ADR Warnings Not Exceptions
- Large File Load Command
- Test Input Discovery
- Async and Concurrency Guidelines
- EF Core Data Access Guidelines
- Read-Ahead Peek Stream
- ADR Stream Input
- ADR Prefer HTML Main Content
- Tracking Memory Stream Double
- ADR Text Direction Reporting
- ADR Multi-Target Net8 Net10
- Project Rules and Testing Strategy
- Attachments Spec
- Baseline Compare Command
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
- Task 32 PR Body
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
- Extractor Entry and Stream Input
- Cross-Cutting Concerns Spec
- Quality Evidence Spec
- Third-Party Notices
- Failing Stream Test Double
- Testing Guidelines
- Naming and Maintainability Guidelines
- Security Baseline Guidelines
- Logging and Observability Guidelines
- Configuration and DI Guidelines
- Detection Spec
- Task 146 Progress Log
- Nullability and Validation Guidelines
- API Contract Guidelines
- JSON Serialization Guidelines
- Resource and Time Guidelines
- Scope Spec
- Task Issue Template
- Pull Request Template
- Retrospective Task 64
- Epic Issue Template
- Task Completion Hook
- Realistic Corpus Manifest
- Story Issue Template
- Profile Corpus Shell Script
- Task 106 Progress Log
- Task 32 Progress Log
- Task 68 Progress Log
- Task 71 Progress Log
- Task 72 Progress Log
- Task 75 Progress Log
- EML Format Detector
- Throughput and Overhead Benchmarks
- ADR Shared Extractor Contract
- Task 149 Progress Log
- Detector Interface
- DOP Sweep Command
- Hardware Probe and Profiling
- Sweep Report and Runner
- ADR FullPath Container Path
- Profile Sample Selection Math
- Throughput Results Doc
- Detector and Layout Facts
- Error Handling Guidelines
- ADR Xfields Metadata
- Brief Gap Analysis
- Baseline Regression Checks
- Throughput Runner

## God Nodes (most connected - your core abstractions)
1. `EmlExtractor` - 80 edges
2. `EmlDetector` - 45 edges
3. `EmlExtraction.Logics` - 43 edges
4. `EmlExtractionResult` - 40 edges
5. `CaseDef` - 36 edges
6. `EmlExtractionRequest` - 33 edges
7. `EmlInputTooLargeException` - 32 edges
8. `EmlExtractionException` - 31 edges
9. `EmlExtraction.Tests` - 31 edges
10. `InternalCorpusTests` - 28 edges

## Surprising Connections (you probably didn't know these)
- `Context` --references--> `EmlExtractionRequest`  [INFERRED]
  docs/adrs/024-stream-input.md → src/EmlExtraction/Models/EmlExtractionRequest.cs
- `Real-world tests` --references--> `InternalCorpusTests`  [INFERRED]
  README.md → tests/EmlExtraction.Tests/InternalCorpusTests.cs
- `Detection (is this an EML)` --references--> `EmlDetector`  [INFERRED]
  docs/research/csharp-capability-assessment.md → src/EmlExtraction/Logics/EmlDetector.cs
- `1. Load — max files per second on this hardware` --references--> `ThroughputRunner`  [INFERRED]
  docs/requirements.md → benches/EmlExtraction.Bench/ThroughputRunner.cs
- `Status` --references--> `EmlExtractionException`  [INFERRED]
  docs/adrs/020-profiling-method.md → src/EmlExtraction/Exceptions/EmlExtractionException.cs

## Import Cycles
- None detected.

## Communities (161 total, 57 thin omitted)

### Community 0 - "Extraction Request and Result Models"
Cohesion: 0.15
Nodes (13): Amendment: source layout and namespaces (2026-10-23, task #149), IEmlExtractor, EmlExtractionRequest, FileBytes, FileName, HasFileBytes, InputStream, EmlExtractionResult (+5 more)

### Community 4 - "Sweep Math and CPU Parsing"
Cohesion: 0.13
Nodes (3): CpuInfoParser, SweepMath, SweepPureTests

### Community 5 - "Realistic Sample Generator"
Cohesion: 0.09
Nodes (4): RealisticSamplesTests, Committed, Binaries, SampleGenerator

### Community 6 - "Attachment Part Collection"
Cohesion: 0.06
Nodes (32): AttachmentsStage, Attachment copy factor, Comparison with `docs/perf-baseline.json`, Data caveats (read first), GC, pause and LOH (sweep traces, synthetic set, 2 phases of about 1.5 s each), Hardware and runs, Inputs, Per-stage allocation share (BDN MemoryDiagnoser, exact, TFM-independent) (+24 more)

### Community 7 - "Text Direction Detection"
Cohesion: 0.11
Nodes (10): Confidence and caveats, Hotspots not taken, Optimization triage 2026-10-18, Owner decisions requested, Provisional targets (traced to report rows), Ranked tasks, Rationale per task, Rules applied (owner decision 2026-10-18, on #74) (+2 more)

### Community 8 - "Telemetry Behavior Tests"
Cohesion: 0.13
Nodes (7): Measurement, Recorder, Instruments, Measurements, SpanRecorder, All, TelemetryTests

### Community 9 - "Caching Encoding Provider"
Cohesion: 0.08
Nodes (15): CopyFactorColumn, AlwaysShow, Category, ColumnName, Id, IsNumeric, Legend, PriorityInCategory (+7 more)

### Community 10 - "Content Catch Site Tests"
Cohesion: 0.12
Nodes (8): Corpus, ContentCatchSiteTests, ThrowingStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 11 - "Internal Corpus Tests"
Cohesion: 0.09
Nodes (20): ADR-008: Flat, opaque attachments — no recursion, TNEF unwrapped, Alternatives, Consequences, Context, Decision, Evidence, Open questions, Status (+12 more)

### Community 12 - "Golden Case Catalog Builder"
Cohesion: 0.08
Nodes (36): GoldenKit.Schema, CaseDef, Eml, Expect, Id, IsEml, Tags, Catalog (+28 more)

### Community 17 - "Bench Sweep Namespace Imports"
Cohesion: 0.12
Nodes (3): EmlExtraction.Bench.Profile, EmlExtraction.Bench.Sweep, NonParallelCollectionDefinition

### Community 18 - "Per-Stage Profiling Benchmarks"
Cohesion: 0.17
Nodes (8): BodyStage, MetadataStage, ParseStage, RequestStage, StageBenchmark, Bytes, Message, Sample

### Community 20 - "Exotic Language Round-Trip Tests"
Cohesion: 0.22
Nodes (4): Edge cases catalog, Public sources used, ExoticLanguageTests, Sequences

### Community 21 - "Project Files and Packages"
Cohesion: 0.09
Nodes (20): MimeKit, Microsoft.NET.Sdk, BenchmarkDotNet, coverlet.collector, DocumentFormat.OpenXml, Microsoft.NET.Test.Sdk, System.Reflection.MetadataLoadContext, xunit (+12 more)

### Community 22 - "CSharp Capability Assessment ADR"
Cohesion: 0.08
Nodes (22): ADR-001: C# / .NET 10 class library stack, Alternatives, Consequences, Context, Decision, Evidence, Status, C# capability assessment - MimeKit on .NET vs Python, Java, Go (+14 more)

### Community 23 - "GoldenKit Corpus Probe Stats"
Cohesion: 0.29
Nodes (4): Probe, Stats, Counts, Samples

### Community 24 - "Header Metadata Mapping"
Cohesion: 0.09
Nodes (12): GcStats, Manifest, ProfileTraceCommand, RunRecord, ADR-005: Always emit from, to, cc, and bcc metadata keys, Alternatives, Consequences, Context (+4 more)

### Community 25 - "GoldenKit Tool Entry Points"
Cohesion: 0.14
Nodes (4): GoldenKit, Generator, Inspect, Json

### Community 26 - "README Overview"
Cohesion: 0.14
Nodes (14): 1. Load — max files per second, 2. Correctness A — emoji and exotic languages, 3. Correctness B — attachment mix, EML extraction, Errors, Golden set, Layout, Migrating from the pre-0.1 API (+6 more)

### Community 27 - "Input Size Limiting Stream"
Cohesion: 0.17
Nodes (7): LimitedReadStream, BytesRead, CanRead, CanSeek, CanWrite, Length, Position

### Community 28 - "License Audit and Guard Scripts"
Cohesion: 0.13
Nodes (8): _read_stdin(), graph_packages(), load_graph(), main(), notices(), nuspec_license(), Skip-rule probe (plugin check.py fed changed_files via stdin; not committed), Task 6: adopt ydk 1.8.0

### Community 29 - "Non-Seekable Stream Test Double"
Cohesion: 0.15
Nodes (8): NonSeekableStream, BytesRead, CanRead, CanSeek, CanWrite, Disposed, Length, Position

### Community 30 - "Benchmark and Telemetry Imports"
Cohesion: 0.13
Nodes (7): Sample, ListenerMode, Activity, Meter, None, EmlExtraction.Bench, TelemetryCollectionDefinition

### Community 31 - "Core Library Namespaces"
Cohesion: 0.21
Nodes (3): EmlExtraction.Exceptions, EmlExtraction.Logics, EmlExtraction.Models

### Community 32 - "Test Project Namespace Imports"
Cohesion: 0.14
Nodes (3): RealisticSamples, ResultProjection, ExpectedFile

### Community 33 - "Throwing Stream Test Double"
Cohesion: 0.20
Nodes (6): ThrowingStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 34 - "ADR Data-Driven Optimization"
Cohesion: 0.13
Nodes (14): ADR-022: Data-driven optimization process, Alternatives, Candidate areas (hypotheses only, not commitments), Closing, Consequences, Context, Decision, Evidence (+6 more)

### Community 35 - "Body Extractor Tests"
Cohesion: 0.33
Nodes (4): BodyContent, BodyExtractor, BodyExtractorTests, ThrowingStream

### Community 36 - "Golden Requirements Checks"
Cohesion: 0.17
Nodes (8): As many headers as possible, Classic fields (To, From, etc.), Content, Metadata fields, Out of original spec (implemented anyway), Requirements (English) — coverage checklist, `X_` fields concatenated into `xfields`, GoldenTests

### Community 37 - "Golden Score Evaluator"
Cohesion: 0.23
Nodes (7): CaseScore, Earned, Failed, Id, Possible, Tags, Scorer

### Community 38 - "ADR Load Test Method"
Cohesion: 0.14
Nodes (13): ADR-015: Load-test method - DOP sweep on real mail, measured memory, committed baseline, Alternatives, Baseline and regression rule, Consequences, Context, Decision, Evidence, Garbage collector (+5 more)

### Community 39 - "ADR Profiling Method"
Cohesion: 0.14
Nodes (13): ADR-020: Profiling method - per-stage CPU, allocation, and GC hotspots on the load-test corpus, Alternatives, Consequences, Context, Decision, Evidence, Inputs, Output (+5 more)

### Community 40 - "ADR Runtime Metrics and Tracing"
Cohesion: 0.14
Nodes (13): Activities (tracing), ADR-021: Runtime metrics and tracing with BCL Meter and ActivitySource, Alternatives, Consequences, Context, Decision, Evidence, Instruments (+5 more)

### Community 43 - "Mbox Splitting and Sample Loading"
Cohesion: 0.20
Nodes (3): ProfileSamples, MboxSplitter, From

### Community 46 - "ADR MimeKit Parser Choice"
Cohesion: 0.22
Nodes (8): ADR-002: MimeKit as the MIME parser, configured leniently, Alternatives, Consequences, Context, Decision, Evidence, Open questions, Status

### Community 48 - "CSharp Best Practices Skill"
Cohesion: 0.18
Nodes (10): Authorship and license, C# Development Best Practices, C#/.NET guardrails, Definition of done, Required operating procedure, Source of truth, Workflow: fix a bug, Workflow: implement a feature (+2 more)

### Community 49 - "Extraction Exception Types"
Cohesion: 0.10
Nodes (15): ADR-023: Throw for misuse, limits, and library bugs; warn for content, Alternatives, Consequences, Context, Decision, Detection, Evidence, New exception types (+7 more)

### Community 50 - "Profile Report Oct 23 Closing"
Cohesion: 0.14
Nodes (12): ADR-022 targets, Comparison with `docs/perf-baseline.json`, Data caveats (read first), Delta table per optimization, GC pause (profile-trace, synthetic set, pause ms over the whole run; run 1 / run 2), Hardware and runs, Large-file pass (net10.0 unless noted, Workstation, DOP 1, 524,288,000), Leftovers for the owner (+4 more)

### Community 51 - "Extraction Spec"
Cohesion: 0.18
Nodes (11): Custom headers, Evidence, Exotic languages and emoji, Extraction - metadata, main content, direction, Failure handling, Input size, Main content, Metadata (+3 more)

### Community 52 - "ADR Warnings Not Exceptions"
Cohesion: 0.22
Nodes (8): ADR-004: Record warnings instead of throwing on malformed MIME, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 53 - "Large File Load Command"
Cohesion: 0.23
Nodes (4): TraceSupport, LargeFileCommand, Pass, M

### Community 55 - "Async and Concurrency Guidelines"
Cohesion: 0.20
Nodes (10): 4.1 Keep asynchronous code asynchronous, 4.2 Pass cancellation tokens end to end, 4.3 Set explicit timeouts for outbound calls, 4.4 Do not use `async void` except for event handlers, 4.5 Do not fire-and-forget background work, 4.6 Bound parallel work, 4.7 Protect shared mutable state, 4.8 Use `IAsyncEnumerable<T>` for genuine streams (+2 more)

### Community 56 - "EF Core Data Access Guidelines"
Cohesion: 0.20
Nodes (10): 9.1 Prevent N+1 queries, 9.2 Paginate collection endpoints and queries, 9.3 Parameterize SQL, 9.4 Keep transactions short and database-focused, 9.5 Use `AsNoTracking()` for read-only EF Core queries, 9.6 Save once per unit of work, not once per row, 9.7 Understand deferred execution and enumeration, 9.8 Keep `IQueryable` inside the data-access boundary (+2 more)

### Community 57 - "Read-Ahead Peek Stream"
Cohesion: 0.14
Nodes (7): ReadAheadStream, CanRead, CanSeek, CanWrite, Head, Length, Position

### Community 59 - "ADR Stream Input"
Cohesion: 0.29
Nodes (6): ADR-024: Stream input, parsed straight from the caller's stream, Consequences, Context, Decision, Status, What streaming saves and what it does not

### Community 61 - "ADR Prefer HTML Main Content"
Cohesion: 0.22
Nodes (8): ADR-003: Prefer HTML as main content in multipart/alternative, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 62 - "Tracking Memory Stream Double"
Cohesion: 0.33
Nodes (3): TrackingMemoryStream, Disposed, Seeked

### Community 63 - "ADR Text Direction Reporting"
Cohesion: 0.22
Nodes (8): ADR-006: Report subject and content text direction as ltr/rtl, Alternatives, Consequences, Context, Decision, Evidence, Rationale, Status

### Community 64 - "ADR Multi-Target Net8 Net10"
Cohesion: 0.22
Nodes (8): ADR-019: Multi-target net8.0 and net10.0, Alternatives, Consequences, Context, Decision, Evidence, Risks the Stage 02 task must verify, Status

### Community 65 - "Project Rules and Testing Strategy"
Cohesion: 0.25
Nodes (6): Product Context, Project Rules, Layers, Rules, Testing strategy, What is real and what is stubbed

### Community 66 - "Attachments Spec"
Cohesion: 0.22
Nodes (9): Attachments, Evidence, Failures, Full path: where the file sat in the message, Naming rules, Outlook TNEF wrappers, Pasted images, What the caller gets (+1 more)

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
Nodes (4): Telemetry overhead (#73), EmlExtractionTelemetry, ExtractScope, TelemetryNoListenerAllocationTests

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

### Community 78 - "Task 32 PR Body"
Cohesion: 0.50
Nodes (3): Test Plan, Verification Plugins, Verification Results

### Community 79 - "Project Overview Spec"
Cohesion: 0.25
Nodes (8): Dependencies and licensing, EML Extraction - Overview, End-to-end flow, Language choice, Problem statement, Spec map, Success criteria, Who uses it and how

### Community 80 - "Open Team Questions"
Cohesion: 0.25
Nodes (8): 1. Shared extractor contract (ADR-012), 2. Template and factory integration, 3. Target framework, 4. License, 5. `X_` headers in internal mail, 6. Telemetry naming conventions (ADR-021), Error model divergence, Open questions for the team

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

### Community 89 - "Extractor Entry and Stream Input"
Cohesion: 0.16
Nodes (8): Conventions, EmlInputTooLargeException, InputLength, MaxInputBytes, EmlExtractor, StageFault, MaxInputBytesTests, StreamInputTests

### Community 90 - "Cross-Cutting Concerns Spec"
Cohesion: 0.29
Nodes (7): Cross-cutting concerns, Diagnostics, Exceptions, Failure and abuse cases, Observability, Runtime surface, Timestamps and text

### Community 91 - "Quality Evidence Spec"
Cohesion: 0.29
Nodes (7): Authored golden set, Correctness A - emoji and exotic languages, Correctness B - realistic attachment samples, Load test, Profiling and optimization, Quality evidence - golden set, realistic samples, harvest, load test, Real-world harvest

### Community 92 - "Third-Party Notices"
Cohesion: 0.29
Nodes (6): Apache POI NOTICE, Apache POI test data (Apache-2.0), Committed samples (ADR-018), Runtime packages (shipped with the library), Test, bench and tooling packages (not shipped), Third-party notices

### Community 93 - "Failing Stream Test Double"
Cohesion: 0.20
Nodes (6): FailingStream, CanRead, CanSeek, CanWrite, Length, Position

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

### Community 101 - "Detection Spec"
Cohesion: 0.33
Nodes (6): Detection, Evidence, How it decides, What it does not do, What it returns, Why it exists

### Community 103 - "Task 146 Progress Log"
Cohesion: 0.33
Nodes (5): Build (done), Follow-up: instance classes (done), Plan (done), Review (done, 1 round), Task 146 orchestrator progress

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

### Community 113 - "Retrospective Task 64"
Cohesion: 0.50
Nodes (3): AI Analysis, Retrospective: 64, Shipped

### Community 143 - "Throughput and Overhead Benchmarks"
Cohesion: 0.15
Nodes (4): EmlBenchmarks, OverheadBenchmarks, Mode, DetectStage

### Community 144 - "ADR Shared Extractor Contract"
Cohesion: 0.17
Nodes (12): ADR-012: Shared team extractor contract (request, detection result, subfile names), Alternatives, Consequences, Context, Decision, Detection, Error model, Evidence (+4 more)

### Community 149 - "Task 149 Progress Log"
Cohesion: 0.33
Nodes (5): Build (done), Leftover fixes (done), Plan (done), Review round 1 (done), Task 149 orchestrator progress

### Community 150 - "Detector Interface"
Cohesion: 0.40
Nodes (3): 1. Detect function, Interface, IEmlDetector

### Community 179 - "DOP Sweep Command"
Cohesion: 0.20
Nodes (5): Options, SweepCommand, RepoRoot, SweepInputs, SweepParameters

### Community 180 - "Hardware Probe and Profiling"
Cohesion: 0.10
Nodes (4): ProfileCommand, HardwareProbe, HardwareProbeException, StringOrListConverter

### Community 182 - "Sweep Report and Runner"
Cohesion: 0.18
Nodes (6): GcRun, HardwareBlock, SetResult, SweepPoint, SweepReport, SweepRunner

### Community 185 - "ADR FullPath Container Path"
Cohesion: 0.13
Nodes (14): Additional location info, ADR-013: FullPath is the container path; nested messages stay opaque, Alternatives, Consequences, Context, Decision, Evidence, FullPath (+6 more)

### Community 195 - "Profile Sample Selection Math"
Cohesion: 0.23
Nodes (3): ProfileMath, SampleFeatures, ProfilePureTests

### Community 199 - "Throughput Results Doc"
Cohesion: 0.17
Nodes (12): Committed baseline and regression rule (ADR-015), DOP sweep (ADR-015), DOP sweep JSON schema (v1), Files / second (1 thread), Hardware, How to scale resources, Large-file pass and sizing formula (ADR-015), Nightly synthetic job (+4 more)

### Community 255 - "Error Handling Guidelines"
Cohesion: 0.25
Nodes (8): 2.1 Never swallow an exception, 2.2 Catch only exceptions you can handle, 2.3 Preserve stack traces and causes, 2.4 Log an unhandled failure once, at the boundary, 2.5 Do not use exceptions as normal control flow, 2.6 Fail fast with guard clauses, 2.7 Use one consistent global error response, 2. Error handling and exceptions

### Community 256 - "ADR Xfields Metadata"
Cohesion: 0.25
Nodes (7): ADR-014: xfields joins literal `X_` headers and lives in the metadata, Alternatives, Consequences, Context, Decision, Evidence, Status

### Community 258 - "Brief Gap Analysis"
Cohesion: 0.25
Nodes (8): Adopt, Avoid, Breaking API change summary (Stage 02), Brief gap analysis - EML extraction vs the team brief, Brief item to status, Not adopted, OLE lessons, Other gaps found (not brief items)

## Knowledge Gaps
- **132 isolated node(s):** `FileBytes`, `FileName`, `HasFileBytes`, `InputStream`, `ContentDirection` (+127 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 739 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **57 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EmlExtractor` connect `Extractor Entry and Stream Input` to `Extraction Request and Result Models`, `Stage Fault Wrapping Tests`, `Attachment Part Collection`, `Text Direction Detection`, `Telemetry Behavior Tests`, `Content Catch Site Tests`, `Throughput and Overhead Benchmarks`, `ADR Shared Extractor Contract`, `Per-Stage Profiling Benchmarks`, `Theory Data-Driven Tests`, `GoldenKit Corpus Probe Stats`, `README Overview`, `Core Library Namespaces`, `Throughput Runner`, `Body Extractor Tests`, `Golden Score Evaluator`, `ADR Runtime Metrics and Tracing`, `MIME Message Parser`, `Profile Report Oct 23 Closing`, `DOP Sweep Command`, `ADR Warnings Not Exceptions`, `Large File Load Command`, `Sweep Report and Runner`, `ADR Stream Input`, `Extraction Telemetry Pipeline`, `ADR Authored Golden Set`, `Golden Set Plan`?**
  _High betweenness centrality (0.116) - this node is a cross-community bridge._
- **Are the 43 inferred relationships involving `EmlExtractor` (e.g. with `.Run()` and `.Child()`) actually correct?**
  _`EmlExtractor` has 43 INFERRED edges - model-reasoned connections that need verification._
- **What connects `FileBytes`, `FileName`, `HasFileBytes` to the rest of the system?**
  _132 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Sweep Math and CPU Parsing` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `EmlDetector` connect `EML Format Detector` to `Extraction Request and Result Models`, `Stage Fault Wrapping Tests`, `Detector Legacy Equivalence Tests`, `Telemetry Behavior Tests`, `Throughput and Overhead Benchmarks`, `ADR Shared Extractor Contract`, `CSharp Capability Assessment ADR`, `GoldenKit Corpus Probe Stats`, `Detector Interface`, `README Overview`, `Core Library Namespaces`, `Throughput Runner`, `Golden Requirements Checks`, `Golden Score Evaluator`, `ADR Runtime Metrics and Tracing`, `Realistic Sample Manifest Tests`, `DOP Sweep Command`, `Extractor Entry and Stream Input`, `Detector and Layout Facts`?**
  _High betweenness centrality (0.044) - this node is a cross-community bridge._
- **Are the 17 inferred relationships involving `EmlDetector` (e.g. with `.Child()` and `.Run()`) actually correct?**
  _`EmlDetector` has 17 INFERRED edges - model-reasoned connections that need verification._
- **Should `Realistic Sample Generator` be split into smaller, more focused modules?**
  _Cohesion score 0.09175377468060394 - nodes in this community are weakly interconnected._