# ADR-010: Custom `library` and `warning` component schemas

## Status
Accepted (Stage 01 Rewrite)

## Context
YDK's built-in `ext` schema requires `base_url`, HTTP `endpoints` (method enum GET/POST/PUT/PATCH/DELETE), `rate_limits` and `retry_strategy`. MimeKit and the Pkcs pin are in-process NuGet packages with none of these. The built-in `error` schema requires an HTTP `status_code` and a `response_shape`. This library has no transport: it records warnings as strings on the result instead of returning error responses (ADR-004).

Filling those required fields would mean inventing a base URL, HTTP verbs, and status codes that do not exist.

## Decision
- Add `.ydk/schemas/library.yaml` for in-process package dependencies: package id, registry, exact version, licence, direct vs pin-only, features used, configuration, failure modes, dependents.
- Add `.ydk/schemas/warning.yaml` for non-fatal diagnostics: code prefix, exact message template, triggers, effect on the returned result, related components.
- All other components use built-in types: `entity`, `contract`, `req`, `nfr`, `crosscut`, `test`.

## Alternatives
- Use `ext` / `error` with placeholder values (`base_url: n/a`, `status_code: 0`). Rejected: the manifests would carry false information.
- Describe dependencies and warnings only in prose. Rejected: this breaks the no-tech-in-prose rule and loses structure.

## Consequences
Generators or ignition packs that consume `ext`/`error` will not see these components. None are installed for this project.
