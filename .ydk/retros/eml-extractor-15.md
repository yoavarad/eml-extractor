# Retrospective: eml-extractor-15

Epic #15 "Metadata key constants" in yoavarad/eml-extractor. Retro file is prefixed because issue numbering restarted and `15.md` belongs to an older epic.

## Shipped
- 17: Add public MetadataKeys const-string class and use it in HeaderMapper (PR #19)
- 18: Use MetadataKeys in tests, GoldenKit and bench instead of metadata key literals (PR #22)

## AI Analysis
No AI analysis available.

## Analysis (Claude Code session, no API provider)
### What went well
- Enum was rejected for const-string MetadataKeys: `reply-to` is not a valid identifier and the public API stays string-keyed.
- The dotnet-tdd-guard pre-commit forced new HeaderMapperTests and GoldenKit CatalogTests/MimeBuildTests.
- GoldenKit is a net10-only exe, so its sources are link-compiled into the test project to make them testable.
- `.ydk/proofs/` was gitignored and untracked (in PR #20), removing noisy artifacts from PRs.

### What went wrong
- A stale verification cache replayed ENDOFLINE dotnet-format failures twice (filed as yoavarad/ydk#643).
- The tdd-guard demanded tests for a pure refactor, adding effort beyond the constant swap.

### Lessons
- Prefer const strings over enums when keys are external strings that are not valid identifiers.
- When a gate fails with an error already fixed, suspect the cache and clear it before re-editing.
- Link-compile sources from exe-only projects into test projects when they need coverage.
- Gitignore generated proof/artifact dirs at the start.

### Summary
MetadataKeys is public and used across library, tests, GoldenKit and bench. Main cost was tooling friction (stale cache, guard hooks).

## Process note
Retro filename keyed by epic number collides across issue-number resets, so `ydk memory retrospective --missing` gave a false negative (ydk issue being filed).
