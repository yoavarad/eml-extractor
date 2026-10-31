# ADR-025: Structured participants on a new result property

## Status
Accepted by owner 2026-10-31 (#14, story #12, source issue yoavarad/eml-extractor#10). Implemented. Extends ADR-012 (result contract) and ADR-005; ADR-005 and ADR-016 stand unchanged.

## Context
Metadata holds each participant header (from, to, cc, bcc, reply-to, sender) as one string, `Name <addr>` joined by `; ` (ADR-005, ADR-016). Callers that need the display name, the address, or its user and domain parts had to re-parse that string. Issue #10 asks for four fields per participant.

## Decision
- New public sealed record `EmailParticipant(string Nickname, string Address, string User, string Domain)` in `EmlExtraction.Models`.
- New init-only property `EmlExtractionResult.Participants`: `IReadOnlyDictionary<string, IReadOnlyList<EmailParticipant>>`. It is not positional, so the record constructor is unchanged (ADR-012).
- Keys `from`, `to`, `cc`, `bcc`, `reply-to`, `sender` (the `MetadataKeys` constants) are always present in an extractor result; a list is empty when the header is absent, unparseable, or the message failed to parse (`parse_failed`).
- Nickname = the mailbox display name as decoded by MimeKit, `""` when none. Address = the mailbox address. It is split at the last `@`: User before, Domain after. No `@`: User = Address, Domain = `""`. No case change, no IDN/punycode conversion.
- Built in `HeaderMapper` from the same effective address list that formats the metadata string, including the decoded re-parse of encoded-words (one parse, shared). Group members are flattened in order and the group name is dropped. Text that never parses as a mailbox gives no entry; Metadata still keeps the text.
- Metadata strings stay exactly as before.

## Alternatives
- Change the metadata value type or add structured keys to Metadata: rejected, it breaks ADR-005 and the `IReadOnlyDictionary<string, string>` contract.
- Positional record parameter: rejected, it changes the constructor of a shared contract type.

## Consequences
- Callers read `result.Participants[MetadataKeys.To]` instead of parsing strings.
- A result built directly by a caller (not by the extractor) gets an empty dictionary unless it sets the property.
- Small extra allocation in the metadata stage for the lists.

## Evidence
`tests/EmlExtraction.Tests/ParticipantsTests.cs` (net8.0 and net10.0), golden suite unchanged.
