# #37 orchestrator progress
- [x] start (worktree .ydk/worktrees/37)
- [x] plan: AsyncLocal internal StageFault seam; guards parse/metadata/body/attachments; IVT tests; CheckedExtractor wrapper; GoldenKit no_internal_error check + probe internal_error count + --inject-fault <stage>
- [x] build (TDD: StageGuardTests red on missing seam -> green)
- [x] GoldenKit: score 1212/1212, checks 11131->12363 (+1232 no_internal_error, one per scored result incl. reextract); score --inject-fault {parse,metadata,body,attachments} exit 1; probe (50 msgs temp copy) exit 0 clean, exit 1 with --inject-fault body (internal_error=50). corpus/internal not present locally.
- [x] review round 1 (cavecrew-reviewer sonnet): fixed parse-empty MainContent null regression, --inject-fault validation, added multi-stage order test; leftovers -> PR body
- [ ] done
