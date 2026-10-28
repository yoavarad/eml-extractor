# Task 6: adopt ydk 1.8.0

- `ydk ci init --force` run; ydk-* workflows now pin YDK_VERSION 1.8.0 / header v1.8.0.
- ci.yml: regenerated output dropped hand edits (net8 SDK, license audit, GoldenKit gate, test logger). Reverted; only header bumped to v1.8.0. scaffold-state ci.yml hash updated to match.
- .ydk/config.yaml: no verification.dotnet.target override.
- `ydk verify run --no-cache --save-proof --task-id 6`: dotnet-build, dotnet-format, dotnet-test pass; detail.target = EmlExtraction.slnx for all three (proof: verification.json).

## Skip-rule probe (plugin check.py fed changed_files via stdin; not committed)
Plugins skip unless a changed file endswith (.cs, .csproj, .sln).
- README.md only: SKIP (expected)
- .editorconfig only: SKIP  (does NOT run; format-affecting file missed)
- EmlExtraction.slnx only: SKIP (.slnx does not match ".sln" suffix)
- Directory.Build.props only: SKIP
- a.cs: runs
Note: `ydk verify run` diffs against local `main` (stale here vs origin/main), so end-to-end probe was confounded by .ydk/config.yaml in diff; direct plugin probe is authoritative.
Upstream ydk gap: _DOTNET_EXTENSIONS should include .slnx, .editorconfig, .props/.targets, global.json.
