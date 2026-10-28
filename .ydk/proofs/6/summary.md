# Task 6: adopt ydk 1.8.0

- `ydk ci init --force` run; ydk-* workflows now pin YDK_VERSION 1.8.0 / header v1.8.0.
- ci.yml: regenerated output dropped hand edits (net8 SDK, license audit, GoldenKit gate, test logger). Reverted; only header bumped to v1.8.0. scaffold-state ci.yml hash updated to match.
- .ydk/config.yaml: no verification.dotnet.target override.
- `ydk verify run --no-cache --save-proof --task-id 6`: dotnet-build, dotnet-format, dotnet-test pass; detail.target = EmlExtraction.slnx for all three (proof: verification.json).

## Skip-rule probe (plugin check.py fed changed_files via stdin; not committed)
ydk 1.8.0 plugins share `_dotnet_target.affects_dotnet_build`: runs on .cs/.csproj/.fsproj/.sln/.slnx/.props/.targets/.editorconfig, global.json, nuget.config.
- README.md only: SKIP (expected)
- .editorconfig only: build PASS, format PASS (target EmlExtraction.slnx)
- EmlExtraction.slnx only: build PASS, format PASS
- Directory.Build.props only: build PASS, format PASS
Note: `ydk verify run` diffs against local `main` (stale here vs origin/main), so the direct plugin probe is authoritative.
