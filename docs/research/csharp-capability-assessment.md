# C# capability assessment - MimeKit on .NET vs Python, Java, Go

Date: 2026-10-18. Answers brief item 15 (`docs/research/brief-gap-analysis.md`): is there a big capability gap that justifies improving a C# library or moving a capability to another language? Linked from ADR-001 Alternatives. Acceptance: `ydk:req:research/capability-assessment`.

## Method and limits

- Primary sources only: official library docs, project READMEs, package docs, and this repo's own measurements. Each cell links its source. Fetched 2026-10-18.
- Support level: **yes** = the primary source documents the capability; **partial** = documented with a stated limit, only through a lower-level API, or not verifiable on the pages checked; **no** = the primary docs checked do not document it (absence in docs, not proof the code cannot do it; a third-party add-on may exist and was not assessed).
- No cross-language benchmark was run. Throughput is measured for .NET only (`docs/perf-results.md`); other languages are marked "not measured". No numbers are invented.
- Candidates, as named in the task: MimeKit on .NET 10; Python `email` stdlib; Java Jakarta Mail (Angus Mail implementation) plus Apache POI HMEF for TNEF; Go `net/mail` plus `enmime`.

## Libraries and licenses

| Stack | Library | License | Source |
| --- | --- | --- | --- |
| .NET 10 | MimeKit | MIT | [MimeKit README](https://github.com/jstedfast/MimeKit/blob/master/README.md) |
| Python | `email` (stdlib) | PSF License v2 | [Python license page](https://docs.python.org/3/license.html) |
| Java | Jakarta Mail API / Angus Mail | Angus Mail: mostly EPL-2.0 (per-file headers apply) | [Angus Mail](https://github.com/eclipse-ee4j/angus-mail), [Jakarta Mail 2.1](https://jakarta.ee/specifications/mail/2.1/) |
| Java | Apache POI HMEF (TNEF) | The HMEF page states no license text, only an ASF copyright; Apache License 2.0 is the ASF default but was not confirmed there | [POI HMEF](https://poi.apache.org/components/hmef/index.html) |
| Go | `net/mail` (stdlib) | Go project license (BSD-style); not re-verified here | [net/mail](https://pkg.go.dev/net/mail) |
| Go | `enmime` | MIT | [enmime README](https://github.com/jhillyerd/enmime/blob/main/README.md) |

MimeKit, enmime, the Python stdlib, and Angus Mail are confirmed open source. The Go stdlib and Apache POI HMEF license terms are pending confirmation (the Go project and Apache POI are open source in general, but the pages fetched did not state it). Angus Mail's dual-license options, if any, were not seen on the page fetched.

## Capability matrix

Cell format: support level - evidence link. License per cell is the library license from the table above.

### Detection (is this an EML)

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | partial | Parser API only; detection is our own sniff (`EmlDetector`). [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | partial | Parser only; own sniff needed. [email](https://docs.python.org/3/library/email.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | Parser API only (Angus Mail implements Jakarta Mail 2.1+, [Angus Mail](https://github.com/eclipse-ee4j/angus-mail)); own sniff needed. | EPL-2.0 / Apache (unconfirmed) |
| Go net/mail + enmime | partial | `ReadMessage` parses headers; own sniff needed. [net/mail](https://pkg.go.dev/net/mail) | Go (BSD-style) / MIT |

No candidate documents an "is this an EML" predicate; the brief's detector is application code (ADR-012), so this row does not separate the stacks.

### Header metadata

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | yes | MIME tree with lazily decoded headers. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | yes | Structured headers; RFC 5322, 6532, 2047, 2231. [email](https://docs.python.org/3/library/email.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | Unverified: the Jakarta Mail 2.1 spec page covers the API artifact and Angus Mail only. Needs Javadoc. | EPL-2.0 |
| Go net/mail + enmime | yes | `Header.Get`, `AddressList`, `ParseDate`; RFC 2047 via optional `WordDecoder`. [net/mail](https://pkg.go.dev/net/mail) | Go (BSD-style) |

### Main content (text/HTML body)

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | yes | Body traversal over the MIME tree. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | yes | `get_body(preferencelist)`. [email.message](https://docs.python.org/3/library/email.message.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | Unverified: the Jakarta Mail 2.1 spec page covers the API artifact and Angus Mail only, so it is not evidence for this cell. Needs Javadoc. | EPL-2.0 |
| Go net/mail + enmime | yes | `Envelope.Text` and `Envelope.HTML`. [enmime](https://pkg.go.dev/github.com/jhillyerd/enmime) | MIT |

### Sub-files: ordinary attachments

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | yes | MIME tree traversal. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | yes | `iter_attachments()`, `is_attachment()`. [email.message](https://docs.python.org/3/library/email.message.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | Unverified: the Jakarta Mail 2.1 spec page covers the API artifact and Angus Mail only, so it is not evidence for this cell. Needs Javadoc. | EPL-2.0 |
| Go net/mail + enmime | yes | `Envelope.Attachments`, `Inlines`, `OtherParts`. [enmime](https://pkg.go.dev/github.com/jhillyerd/enmime) | MIT |

### Sub-files: TNEF (`winmail.dat`)

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | yes | `TnefPart`, `TnefMessage`, `ConvertToMime()`; `TnefOptions` limits nesting depth, attachment count, and data sizes. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | no | Not documented in the `email` package docs. [email](https://docs.python.org/3/library/email.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | POI HMEF reads TNEF body and attachments, read-only; self-described "work-in-progress"; requires poi-scratchpad. [POI HMEF](https://poi.apache.org/components/hmef/index.html) | Apache POI (license text not on page) |
| Go net/mail + enmime | no | Not documented for enmime or net/mail. [enmime](https://pkg.go.dev/github.com/jhillyerd/enmime) | MIT |

### Sub-files: nested mail (`message/rfc822`)

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | partial | Generic MIME tree; the README does not address `message/rfc822` explicitly. Repo decision: nested mail stays opaque (ADR-013). [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | yes | Payload is a list holding the embedded `EmailMessage`; `walk()` descends. [email.message](https://docs.python.org/3/library/email.message.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | No primary source checked documents this; unverified. | EPL-2.0 |
| Go net/mail + enmime | no | Not documented for enmime. [enmime](https://pkg.go.dev/github.com/jhillyerd/enmime) | MIT |

### Unicode (charsets, emoji)

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | partial | The README fetched does not describe charset handling; fidelity is established by this repo's golden tests, not by library docs. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | yes | "Structured tree of Unicode text and binary attachments"; RFC 6532. [email](https://docs.python.org/3/library/email.html) | PSF v2 |
| Java Jakarta Mail + POI | partial | Not verified for Jakarta Mail; POI HMEF warns Unicode for non-English TNEF is untested. [POI HMEF](https://poi.apache.org/components/hmef/index.html) | EPL-2.0 / Apache POI |
| Go net/mail + enmime | yes | Converts non-UTF-8 parts to UTF-8; `ErrorCharsetConversion` reported. [enmime](https://pkg.go.dev/github.com/jhillyerd/enmime) | MIT |

### Throughput

| Stack | Supported | Evidence | License |
| --- | --- | --- | --- |
| MimeKit / .NET 10 | yes (measured) | Single thread, M1 Pro, .NET 10.0.12: about 48,700 extracts/s tiny mail, about 16,900/s 20 KB HTML, about 259/s with a 1 MB attachment. [perf-results.md](../perf-results.md). MimeKit states performance is a core design goal and ships a BenchmarkDotNet suite. [README](https://github.com/jstedfast/MimeKit/blob/master/README.md) | MIT |
| Python email | not measured | No measurement exists in this repo; no benchmark link cited (none run).  | PSF v2 |
| Java Jakarta Mail + POI | not measured | No measurement exists in this repo; no benchmark link cited (none run).  | EPL-2.0 / Apache POI |
| Go net/mail + enmime | not measured | No measurement exists in this repo; no benchmark link cited (none run).  | Go (BSD-style) / MIT |

## Findings

1. MimeKit is the only candidate whose own documentation covers TNEF, with hardening limits (nesting depth, attachment count, data size), in the same library. Java reaches TNEF only through a separate, self-described work-in-progress POI component. Python and Go docs do not document TNEF.
2. For headers, body, attachments, and unicode, at least one non-C# stack matches MimeKit on documented support; none is shown to exceed it.
3. The only measured throughput is .NET. No evidence shows another language is faster, and the throughput target is to be sized from our own DOP sweep (ADR-015, implementation pending, thresholds provisional), not from a language comparison.
4. Nested `message/rfc822` is not documented by the MimeKit README. This repo treats nested messages as opaque by decision (ADR-013), so it is not a gap against the brief.

## Unresolved evidence

- Jakarta Mail MIME-level behavior (headers, body, attachments, nested mail, charsets) is not verified; it needs a Javadoc read before the "partial" cells can become yes or no.
- Go standard library license text and the POI HMEF license were not confirmed on the pages fetched.
- Cross-language throughput is not measured. If the team wants it, open a follow-up task with an equal corpus per language. It is not needed for the conclusion because no capability above favors a move.
- Third-party TNEF add-ons for Python and Go were not assessed.

## Conclusion

The matrix supports this for detection, headers, body, attachments, unicode, and TNEF. For nested `message/rfc822` (Python yes, MimeKit partial) the conclusion rests on the ADR-013 decision to keep nested mail opaque, not on the matrix. Throughput is unmeasured outside .NET; the conclusion makes no throughput claim.

no big gap - stay on C#
