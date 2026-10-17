# ADR-005: Always emit from, to, cc, and bcc metadata keys

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
Commit `c3b39bb` ("Report RTL/LTR direction and always emit from, to, cc, and bcc.") states: "Callers can rely on those four address keys (empty when missing)". The code comment in `HeaderMapper.EnsureAddress` says: "Always emit the key so callers can read from/to/cc/bcc without a lookup miss."

## Decision
- Metadata always contains the four address keys.
- Value precedence: first the parsed mailbox list, formatted as `Name <addr>` and joined by `; ` (groups as `Group: members;`); if the parser found no mailboxes, the raw header value; if the header is absent, the empty string.

## Alternatives
Omitting absent keys. The commit message implies this was the behaviour before `c3b39bb`.

## Consequences
Callers can index these keys without a missing-key check. An empty string means "header absent or empty", not "parse failed".

## Evidence
Commit `c3b39bb`, `src/EmlExtraction/HeaderMapper.cs`, tests `ExtractionTests.FromToCcBccAreAlwaysPresent`, `ExtractsFromToCcBccIncludingMultiplesAndRtlNames`, `RawBccHeaderIsExtracted`.
