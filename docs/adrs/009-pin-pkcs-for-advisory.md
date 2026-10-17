# ADR-009: Pin System.Security.Cryptography.Pkcs 10.0.13

## Status
Accepted (recorded retroactively, Stage 01 Rewrite)

## Context
Commit `a381aa0`: "fix(deps): pin System.Security.Cryptography.Pkcs to 10.0.13 to clear GHSA-846f-f764-76mx". CI fails on High/Critical vulnerable packages, transitive ones included.

## Decision
Add a direct package reference to `System.Security.Cryptography.Pkcs` 10.0.13 in `src/EmlExtraction/EmlExtraction.csproj`, overriding the transitive version that MimeKit brings in. No code calls the package directly.

## Alternatives
Not recorded in the repo.

## Consequences
Revisit the pin on every MimeKit upgrade, and remove it once the transitive version is no longer vulnerable.

## Evidence
Commit `a381aa0`, `src/EmlExtraction/EmlExtraction.csproj`, `.github/workflows/ci.yml` (vulnerability audit step).
