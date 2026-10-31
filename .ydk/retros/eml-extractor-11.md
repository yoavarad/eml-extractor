# Retrospective: eml-extractor-11

Epic #11 "Decoded, structured email participants (issue #10)" in yoavarad/eml-extractor. Retro file is prefixed because issue numbering restarted and `11.md` belongs to an older epic.

## Shipped
- 13: Decode RFC 2047 encoded-words that cover a whole mailbox or address in participant headers (PR #20)
- 14: Expose participants as nickname, address, user and domain on a new result property (PR #21)

## AI Analysis
No AI analysis available.

## Analysis (Claude Code session, no API provider)
### What went well
- Split into decode (#13) and structured exposure (#14) kept each PR small and reviewable.
- PR #20 was rebased onto PR #19 so it uses MetadataKeys constants instead of literals.

### What went wrong
- PR #20 first used hard-coded metadata keys, then needed a rebase onto #19.
- Leftover: a mixed header with an undecodable encoded token drops that token.
- PR #21 merged before its gap work (bench and read-only lists) landed. The metadata stage regressed +20-30% time and +50% alloc. A follow-up quick task was opened.
- The orchestrator kept working on a PR the human could merge at any time.

### Lessons
- Check PR state (`gh pr view`) before every push; do not keep working on a PR that may already be merged.
- Land perf-sensitive gap work (bench, read-only collections) before merge, or gate the merge on the bench.
- Declare dependencies between parallel PRs up front so shared constants are used from the start.
- Decide the policy for undecodable tokens in mixed headers (keep raw vs drop) and test it.

### Summary
Participants are decoded and exposed in structured form. Open items: the dropped undecodable token and the metadata-stage perf regression (follow-up task).
