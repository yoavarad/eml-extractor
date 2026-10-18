# #71 progress
- [x] start: worktree .ydk/worktrees/71
- [x] plan: EmlExtractionTelemetry static class; record in Extract(request)+Extract(MimeMessage) finish helper, InternalError helper, Detect/IsEml. part_decode test via internal MimeMessage overload w/ throwing content stream.
- [x] build: TDD red->green, 10 TelemetryTests, both TFMs 126 pass; README + manifests
- [x] CI local: Release TWAE build ok, tests 126/126 both TFMs, GoldenKit 1212/1212
- [ ] review pending
- [x] review: 0 red, 3 yellow; fixed parse-stage test + README; listener-throw risk -> owner decision
