# ADR-024: Stream input, parsed straight from the caller's stream

## Status
Accepted by owner 2026-10-23 (#148, source issue #143). Implemented. Supersedes the streaming-input deferral in ADR-022 ("Streaming") and `docs/specs/scope.md` (OUT: streaming input; additional request input sources, stream part). Extends ADR-012 (Requests). Streaming output stays deferred.

## Context
A reviewer (#143) noted that most extractor libraries take a `Stream`. ADR-012 left room on `EmlExtractionRequest` for a stream source. ADR-022 and `scope.md` deferred streaming unless the measured memory ceiling was exceeded. A caller holding a file or a network stream had to read the whole message into a `byte[]` first, so the whole input was resident next to everything the parser holds.

On 2026-10-23 the owner decided to implement true streaming input: the library parses straight from the caller's stream. That reverses the deferral, so it is recorded here.

## Decision
- `EmlExtractionRequest` gains `Stream? InputStream`. `FileBytes` is no longer `required`. Exactly one of `FileBytes` and `InputStream` must be set; both or neither throws `ArgumentException` naming `request` (misuse, ADR-023). Setting `FileBytes`, even to empty, counts as set.
- `IEmlExtractor` / `EmlExtractor` gain `Extract(Stream eml)`, a thin wrapper like the bytes overload: it builds a request with that stream and calls the primary method. A null stream throws `ArgumentNullException`.
- The stream is read forward from its current position to its end. It need not be seekable. The library never seeks, closes or disposes it; the caller owns it. Sync only; async is out of scope.
- The stream path parses non-persistently (a persistent parse needs a seekable stream the library owns). The bytes path stays persistent, as before.
- The mbox `From ` check (`ydk:req:extraction/mbox-from-line`) peeks the first 8 bytes (optional UTF-8 BOM plus `From `) through a small read-ahead wrapper that replays them, so non-seekable streams work.
- `maxInputBytes` is enforced while reading: a counting wrapper caps each read so at most limit + 1 bytes are taken, and throws `EmlInputTooLargeException` (`InputLength` = bytes read, i.e. limit + 1; `MaxInputBytes`; `ParamName` `request`) once more than the limit is read. The parse stage lets it through: it is an `ArgumentException`, but it is neither a `parse_failed` warning nor wrapped in `EmlExtractionException`. MimeKit does not catch it.
- Telemetry: the input-size histogram records the bytes actually read (the size is unknown up front), on success and on refusal. The extract activity has no `eml_extraction.input.size` tag on the stream path.
- Reader exceptions from the caller's stream (for example `IOException`) are content failures and become a `parse_failed` warning, like any parse failure.

## What streaming saves and what it does not
- Saves: the whole-input copy. The caller no longer holds the full message as a `byte[]` while it is parsed.
- Does not save: MimeKit's non-persistent parse still buffers each part's encoded content in memory, and decoding still produces each attachment's `byte[]`. So the stream path allocates more than the bytes path (the bytes path's persistent parse reads part content as views over the caller's array), and an oversized input is read up to the limit before it is refused (the bytes path refuses on length without reading).
- Evidence (ADR-022): `large` vs `large --stream` in `docs/perf-results.md`.

## Consequences
- Callers that read from disk or the network pass the stream and skip `File.ReadAllBytes`. Callers that already hold bytes keep the bytes path, which allocates less.
- The request record no longer marks `FileBytes` as `required`; an empty request now compiles and throws at run time.
- `Extract(Stream)` and a request with `InputStream` give the same results as the bytes path on every golden input, seekable and non-seekable (`StreamInputTests`).
