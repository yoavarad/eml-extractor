# ADR-016: One canonical metadata key per header

## Status
Accepted (per project owner, 2026-10-17). Implementation pending, tracked as Stage 02 work. Amends ADR-005 and ADR-006. Amended by ADR-023 (2026-10-18): oversized input throws, so there is no `input_too_large` result.

## Context
The metadata map has three problems:
- Duplicate keys for one header. Every header is copied under its name lower-cased with `-` replaced by `_` (`reply_to`, `message_id`), and the classic overlay adds a second, punctuation-free key (`replyto`, `messageid`) for the same header.
- Collisions. Folding `-` into `_` maps `X-Foo` and `X_Foo` to the same key `x_foo`, and their values are merged.
- Computed values mixed into the header map. `subject_direction` and `content_direction` are not headers; a message with a header named `Subject-Direction` would collide.

In addition, on `parse_failed` the map is empty, so the "always present" address keys of ADR-005 are not always present.

## Decision
Per project owner, 2026-10-17:
- **Key = header field name, trimmed and lower-cased (invariant culture). Nothing else is changed.** `-` and `_` are kept: `X-Foo` gives `x-foo`, `X_Foo` gives `x_foo`, `Message-ID` gives `message-id`. Header names are case-insensitive in RFC 5322, so lower-casing never merges two distinct headers.
- One key per header. Repeated headers with the same key are joined with `\n` in order.
- The classic overlay writes its parsed form into the header's own canonical key and adds no second key. Overlaid keys: `from`, `to`, `cc`, `bcc`, `reply-to`, `sender` (formatted mailbox lists), `date` (ISO 8601 round-trip when it parses, otherwise the raw value), `message-id`, `in-reply-to` (angle brackets removed), `references` (ids joined by one space). Every other header keeps its decoded, unfolded raw value, including `content-type`, `mime-version`, `return-path`, `delivered-to`, and `received`.
- Reserved computed key: `xfields` only (ADR-014). A header whose name is `xfields` in any case is stored under `header:xfields`. RFC 5322 field names cannot contain `:`, so that key never collides.
- Direction values leave the map. They are only on the result's `SubjectDirection` and `ContentDirection` properties.
- Always-present keys: `from`, `to`, `cc`, `bcc`, `xfields`. They are present in every result, including `parse_failed` and `input_too_large` (value `""` when unknown).

## Alternatives
- Keep `-` to `_` folding and resolve collisions by suffixing. Rejected: keys would depend on header order.
- Keep both raw and overlay keys (status quo). Rejected: callers do not know which key to read.
- Punctuation-free keys for everything (`messageid`, `xfoo`). Rejected: more collisions, not fewer.
- Keep direction keys in the map for convenience. Rejected: computed values in a header map can collide with real headers, and the properties already exist.

## Consequences
- Key names change (`messageid` to `message-id`, `replyto` to `reply-to`, `dkim_signature` to `dkim-signature`). Golden metadata expectations are regenerated in Stage 02.
- `contenttype` (top-level MIME type) disappears; `content-type` holds the raw header and `MainContentMimeType` holds the body's type.
- Breaking change, covered by ADR-012.
- Manifests: `ydk:entity:extraction/Metadata`, `ydk:req:metadata/canonical-keys`, `ydk:req:metadata/all-headers`, `ydk:req:metadata/classic-fields`, `ydk:req:content/text-direction`.

## Evidence
`src/EmlExtraction/HeaderMapper.cs`, `src/EmlExtraction/EmlExtractor.cs`, `docs/research/brief-gap-analysis.md` (gap 8). Project owner decision, 2026-10-17.
