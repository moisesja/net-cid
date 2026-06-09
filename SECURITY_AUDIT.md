# Security Audit Report

## Executive Summary

Audit date: **June 8, 2026**

This audit refreshes the prior review (dated February 20, 2026) to cover the JSON Canonicalization Scheme (JCS) surface and the cryptographic-key encoding surface that landed *after* that snapshot was taken. The earlier review predated the JCS canonicalizer entirely, so `JcsCanonicalizer` and its supporting types had never been security-reviewed until now.

Surface added since the prior audit and reviewed here:

- `JcsCanonicalizer` (RFC 8785 JSON Canonicalization Scheme), introduced in `1.4.0` and completed in `1.6.0` with full ECMA-262 §6.1.6.1.20 (`Number.prototype.toString`) number serialization (`EcmaScriptNumber`).
- The P-521 public-key multicodec (`p521-pub`, `0x1202`), added in `1.5.0`, completing the NIST P-curve public-key set.
- `Multikey` — the W3C Controlled Identifiers 1.0 Multikey / `did:key` `publicKeyMultibase` codec — added in `1.6.0` (issue #14).

This refresh found and tracked **four** parser-hardening findings (S1–S4) in the JCS / multibase surface. **All four are now resolved and shipped in `1.6.0`.**

Overall result: **No open findings.** Four findings (one LOW/MEDIUM, one MEDIUM, two HIGH) were identified in the newly-reviewed JCS/multibase surface and are all **Resolved/Closed** in `1.6.0`.

Decision: **Release-ready** from a security perspective as of this audit, with the JCS surface now reviewed, the S1–S4 hardening changes verified, and dependency and parser-hardening controls validated.

## Scope

- Core library source under `NetCid/` — every source file is in scope this round:
  - `NetCid/Cid.cs`
  - `NetCid/CidFormatException.cs`
  - `NetCid/CidVersion.cs`
  - `NetCid/EcmaScriptNumber.cs` *(newly reviewed — ECMA-262 number canonicalization)*
  - `NetCid/JcsCanonicalizer.cs` *(newly reviewed — most signature-critical surface)*
  - `NetCid/JcsFormatException.cs` *(newly reviewed — JCS failure-mode surface)*
  - `NetCid/Multibase.cs`
  - `NetCid/MultibaseEncoding.cs`
  - `NetCid/Multicodec.cs`
  - `NetCid/Multihash.cs`
  - `NetCid/MultihashCode.cs`
  - `NetCid/MultihashDigest.cs`
  - `NetCid/Multikey.cs` *(newly reviewed — `did:key` / Multikey key encoding)*
  - `NetCid/Varint.cs`
- Unit and integration tests under `NetCid.Tests/` and `NetCid.IntegrationTests/`
- Example projects under `examples/`
- CI/security workflows under `.github/workflows/`
- Dependency posture after adding `SimpleBase` `5.6.0`

## Change Context

The prior audit (February 20, 2026) reviewed the multibase/CID surface after migrating the base32/base36/base58 primitives to `SimpleBase`. That snapshot **predated** the JCS canonicalizer and the P-521 / Multikey key-encoding work, none of which it reviewed. This round adds the following change context.

Carried-forward multibase context (still applies):

- Replaced internal base32/base36/base58 primitive implementation with `SimpleBase` in `NetCid/Multibase.cs`.
- Preserved strict wrapper checks for CID semantics:
  - prefix allowlist (`b`, `B`, `k`, `K`, `z`)
  - base32 padding rejection
  - base32 non-zero trailing-bit rejection
  - case-specific base36 validation
  - input-size limits and exception normalization

New surface reviewed this round:

- **JCS canonicalizer (RFC 8785)** — `NetCid/JcsCanonicalizer.cs`, added in `1.4.0`. v1 covered objects, arrays, strings, integer-valued numbers within `[long.MinValue, ulong.MaxValue]`, booleans, and null. `1.6.0` completed it with full RFC 8785 §3.2.2.3 / ECMA-262 §6.1.6.1.20 (`Number.prototype.toString`) number serialization for fractional, exponential, and out-of-`ulong` values, via the new `NetCid/EcmaScriptNumber.cs` helper (issue #13). `NetCid/JcsFormatException.cs` is the failure mode for values JCS cannot represent deterministically.
- **P-521 multicodec** — `Multicodec.P521Pub` (`0x1202`) and the `"p521-pub"` name mapping, added in `1.5.0`, completing the NIST P-curve public-key set alongside `P256Pub` / `P384Pub` (issue #11).
- **Multikey codec** — `NetCid/Multikey.cs`, added in `1.6.0` (issue #14). Implements the W3C Controlled Identifiers 1.0 Multikey verification method and `did:key` `publicKeyMultibase` encoding: `base58btc( varint(keyCodec) ‖ rawPublicKey )`. Validates the codec is one of the eight supported key-type multicodecs and that the raw key length matches that codec; rejects non-base58btc multibases and content-type codecs.

Security hardening changes shipped in `1.6.0` (tracked as findings S1–S4 below):

- **S1 (issue #16, HIGH):** JCS recursion-depth limit (`MaxDepth = 64`) plus a 1 MiB default output cap, closing an unbounded-recursion / uncatchable-`StackOverflowException` DoS on untrusted JSON.
- **S2 (issue #17, HIGH):** JCS rejects duplicate object member names, closing a non-canonical-output / signature-confusion vector.
- **S3 (issue #18, MEDIUM):** JCS rejects invalid UTF-16 (unpaired surrogates) instead of silently substituting U+FFFD, closing a collision / signature-confusion vector.
- **S4 (issue #19, LOW/MEDIUM):** confirmed the base64url decode path rejects non-canonical trailing bits (CID malleability class) and pinned it with regression tests.

## Methodology

### 1. Manual secure-code review

Reviewed parsing, canonicalization, and error paths across **every** file under `NetCid/`:

- `NetCid/Cid.cs`
- `NetCid/CidFormatException.cs`
- `NetCid/CidVersion.cs`
- `NetCid/EcmaScriptNumber.cs` — **newly reviewed**; ECMA-262 §6.1.6.1.20 number-to-string canonicalization (determinism-critical: any two JSON spellings of the same number must produce identical bytes)
- `NetCid/JcsCanonicalizer.cs` — **newly reviewed and the most signature-critical surface**; produces the bytes that downstream data-integrity / verifiable-credential signatures cover, so any non-canonical or ambiguous output is a signature-confusion vector
- `NetCid/JcsFormatException.cs` — **newly reviewed**; fail-closed exception type for values JCS cannot represent deterministically
- `NetCid/Multibase.cs`
- `NetCid/MultibaseEncoding.cs`
- `NetCid/Multicodec.cs`
- `NetCid/Multihash.cs`
- `NetCid/MultihashCode.cs`
- `NetCid/MultihashDigest.cs`
- `NetCid/Multikey.cs` — **newly reviewed**; `did:key` / Multikey key-codec allowlist and raw-key-length validation
- `NetCid/Varint.cs`

Focus areas:

- Input boundary enforcement and argument validation
- Parser and JCS canonicalization behavior (determinism, duplicate-key handling, UTF-16 validity, number serialization)
- Recursion-depth and output-size bounds on untrusted JSON
- Exception safety (`Try*` APIs should fail safely)
- Integer overflow and allocation boundaries
- Cryptographic primitive and key-codec selection

### 2. Supply-chain and dependency checks

Executed:

- `dotnet list NetCid.sln package --vulnerable --include-transitive`
- `dotnet list NetCid.sln package --deprecated`
- `dotnet list NetCid/NetCid.csproj package --include-transitive`

### 3. Build and test verification

Executed:

- `dotnet build NetCid/NetCid.csproj -c Release --no-restore --tl:off -warnaserror`
- `dotnet build NetCid.Tests/NetCid.Tests.csproj -c Release --no-restore --tl:off`
- `dotnet build NetCid.IntegrationTests/NetCid.IntegrationTests.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/cid-interface/CidInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/multicodec-interface/MulticodecInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/multihash-interface/MultihashInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/block-interface/BlockInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/did-key-interface/DidKeyInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/jcs-interface/JcsInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet build examples/multibase-interface/MultibaseInterfaceExample.csproj -c Release --no-restore --tl:off`
- `dotnet test NetCid.Tests/NetCid.Tests.csproj -c Release`
- `dotnet test NetCid.IntegrationTests/NetCid.IntegrationTests.csproj -c Release`

### 4. Targeted negative/fuzz-style robustness checks

Executed ad-hoc malformed input probes against:

- `Multibase.TryDecode`
- `Multibase.Decode`
- `Multibase.DecodeBase58Btc`
- `Cid.TryParse`
- `Cid.Parse`
- `Cid.TryDecode`
- `Cid.Decode`
- `JcsCanonicalizer.Canonicalize` (all overloads)

Coverage details:

- `100,000`-iteration ASCII malformed corpus for multibase and CID parse/decode APIs
- `20,000`-iteration random Unicode malformed corpus for `Cid.Parse`, `Cid.TryParse`, and `Multibase.TryDecode`
- JCS canonicalizer corpus exercising:
  - random / structurally-malformed JSON
  - deeply-nested JSON that crosses the `MaxDepth = 64` recursion limit (depth-limit DoS class — S1)
  - objects containing duplicate member names (S2)
  - strings and member names containing invalid UTF-16 (unpaired surrogates) (S3)
- Deterministic-seed `100,000`-iteration bit-pattern fuzz over `EcmaScriptNumber.ToCanonicalString(double)` (runs in every CI build) plus the cyberphone 100M-vector RFC 8785 number conformance set (`jcs-number-conformance` workflow)

Purpose:

- Validate malformed input does not trigger unexpected exception classes
- Confirm `Try*` APIs do not throw for malformed input
- Confirm the JCS canonicalizer fails closed (throws `JcsFormatException`, never overflows the stack or emits non-canonical bytes) on hostile JSON
- Verify wrapper behavior remains stable after third-party codec integration

### 5. Security workflow coverage review

Reviewed:

- `.github/workflows/security.yml`
- `.github/workflows/codeql.yml`
- `.github/workflows/ci.yml`

## Results

## Dependency posture

- Vulnerability scan: **no vulnerable packages found**
- Deprecated package scan: **none**
- Runtime dependency graph for `NetCid`: only `SimpleBase` `5.6.0` top-level, no transitive dependencies

`SimpleBase` package metadata (local NuGet cache):

- version: `5.6.0`
- license: `Apache-2.0`
- dependency groups: empty for `net8.0/net9.0/net10.0`

## Build and test posture

- All audited projects built successfully in `Release`
- Unit tests: **230 passed, 0 failed, 1 skipped, 231 total**
- Integration tests: **6 passed, 0 failed, 6 total**
- The 1 skipped unit test is the JCS number conformance test, which is skipped unless the cyberphone 100M-vector conformance set is present locally; it runs in the dedicated `jcs-number-conformance` workflow
- No build warnings when building the core project with `-warnaserror`

## Fuzz/negative testing posture

- `Multibase.TryDecode`: **0 throws** across random malformed input corpus (ASCII + Unicode)
- `Multibase.Decode`: **0 unexpected exception types**
- `Multibase.DecodeBase58Btc`: **0 unexpected exception types**
- `Cid.TryParse`: **0 throws**
- `Cid.Parse`: **0 unexpected exception types**
- `Cid.TryDecode`: **0 throws**
- `Cid.Decode`: **0 unexpected exception types**
- `JcsCanonicalizer.Canonicalize`: **0 unexpected exception types / 0 process-terminating faults** across the random, deeply-nested (depth-limit), duplicate-key, and invalid-UTF-16 (unpaired-surrogate) JSON corpus — over-depth, duplicate-name, and unpaired-surrogate inputs fail closed with `JcsFormatException`, never `StackOverflowException`
- `EcmaScriptNumber.ToCanonicalString(double)`: **0 deviations** across the deterministic-seed 100k bit-pattern fuzz

## Control verification matrix

- Input length enforcement:
  - `Cid.DefaultMaxInputStringLength`, `Cid.DefaultMaxInputByteLength`, `Multibase.DefaultMaxInputLength` enforced in parse/decode paths
- Canonical varint validation:
  - `Varint.TryDecode` rejects oversized/non-canonical encodings and enforces 9-byte cap
- CID version restrictions:
  - CID versions `2` and `3` rejected as reserved
- CIDv0 constraints:
  - enforced `dag-pb` + `sha2-256` (32-byte digest) requirements
- Multibase strictness retained despite dependency swap:
  - base32 padding rejected
  - base32 non-zero trailing bits rejected
  - base64url non-zero trailing bits rejected (validated by the `System.Buffers.Text.Base64Url` decoder, not a wrapper check; the decoder rejects rather than masks the unused bits; pinned by regression tests — issue #19, finding S4)
  - base36 case-specific alphabets enforced
- JCS canonicalization strictness (RFC 8785, untrusted-JSON surface):
  - recursion-depth bound: `JcsCanonicalizer` enforces `MaxDepth = 64` in both the `NaN`/`±infinity` validation pre-pass and the serialization walk, throwing `JcsFormatException` rather than overflowing the stack (finding S1)
  - output-size bound: canonical output capped at `JcsCanonicalizer.DefaultMaxOutputByteLength` (1 MiB) by default, configurable via `maxOutputBytes`; the crossing byte is rejected before it is committed to a caller-supplied `IBufferWriter<byte>` (finding S1, defense-in-depth)
  - duplicate object member names rejected on both the `JsonElement` and `JsonNode?` / `IBufferWriter<byte>` paths (finding S2)
  - invalid UTF-16 (unpaired surrogates) rejected in strings and member names across the `JsonNode` and `JsonElement` paths, for `string`- and `char`-backed values, instead of silent U+FFFD substitution (finding S3; documented residual gap for `JsonValue` wrapping a raw CLR object)
  - number serialization: integer- and IEEE-754-valued numbers canonicalized per ECMA-262 §6.1.6.1.20 so distinct spellings of one value yield identical bytes
- Key-encoding strictness:
  - `Multikey` accepts only the eight supported key-type multicodecs and validates raw-key length per codec; rejects non-base58btc multibases and content-type codecs
- Cryptography:
  - hash generation uses `SHA256.HashData` and `SHA512.HashData`
- Unsafe/runtime interop:
  - no `unsafe` blocks, no P/Invoke/native interop in library code

## Findings

### Open findings

- **None.** All findings identified this round (S1–S4) are resolved and shipped in `1.6.0`.

### Resolved findings (this audit round)

The JCS canonicalizer surface had never been security-reviewed prior to this round (the February 20, 2026 snapshot predated it). Reviewing it surfaced four findings, all now closed.

#### S1 — Unbounded JCS recursion → uncatchable `StackOverflowException` (DoS)

- **Severity:** HIGH
- **State:** Resolved (issue #16, `1.6.0`)
- **Description:** `JcsCanonicalizer` recursed without a depth limit while validating and serializing JSON. Because JCS processes untrusted credential JSON and a .NET `StackOverflowException` cannot be caught (it terminates the process), deeply-nested input was a denial-of-service vector.
- **Resolution:** Enforces `MaxDepth = 64` (matching `System.Text.Json`'s default) in both the `NaN`/`±infinity` validation pre-pass and the core serialization walk, throwing `JcsFormatException` instead of recursing. Adds a 1 MiB default output cap (`DefaultMaxOutputByteLength`, configurable via `maxOutputBytes`) as defense-in-depth.

#### S2 — JCS did not reject duplicate object member names (signature confusion)

- **Severity:** HIGH
- **State:** Resolved (issue #17, `1.6.0`)
- **Description:** RFC 8785 builds on I-JSON (RFC 7493 §2.3), which forbids duplicate member names, but `JsonDocument.Parse` preserves duplicates. The canonicalizer emitted them, producing non-canonical JSON that different parsers could read differently — a signature-confusion vector for the data-integrity pipeline.
- **Resolution:** Rejects objects with duplicate member names (throws `JcsFormatException`, fails closed) on both the `JsonElement` overload and the `JsonNode?` / `IBufferWriter<byte>` overloads.

#### S3 — JCS silently substituted U+FFFD for invalid UTF-16 (collision / signature confusion)

- **Severity:** MEDIUM
- **State:** Resolved (issue #18, `1.6.0`)
- **Description:** An unpaired surrogate has no UTF-8 representation; `System.Text.Json` silently substituted U+FFFD, collapsing two distinct malformed inputs (and a legitimately-supplied U+FFFD) to identical canonical bytes — a collision / signature-confusion vector.
- **Resolution:** Rejects strings and object member names containing unpaired surrogates with `JcsFormatException` across both the `JsonNode` and `JsonElement` paths (and `Cid.FromCanonicalJson`), for `string`- and `char`-backed values. Valid surrogate pairs and a legitimate U+FFFD are unchanged byte-for-byte. **Residual gap (documented):** a `JsonValue` wrapping a raw CLR object is expanded by `System.Text.Json`, which substitutes U+FFFD before this validation can inspect its members; callers should canonicalize parsed JSON or primitive-built nodes when the UTF-16 is untrusted.

#### S4 — base64url non-canonical trailing-bit (CID malleability) class

- **Severity:** LOW/MEDIUM
- **State:** Resolved / confirmed-mitigated (issue #19, `1.6.0`)
- **Description:** The base32 path already guards against non-canonical trailing bits (a CID-malleability class); the base64url decode path was reviewed for the same class.
- **Resolution:** Confirmed `System.Buffers.Text.Base64Url.DecodeFromChars` rejects (does not mask) non-zero unused trailing bits, so `Multibase.Decode` / `Cid.Parse` reject non-canonical base64url payloads. Pinned with regression tests; no behavior change.

### Notes and residual risk (non-finding)

- Base58 and similar positional-base decoding are computationally heavier than fixed-radix encodings; this remains bounded by explicit input-size limits (`4096` chars by default).
- The S3 `JsonValue`-wrapping-a-raw-CLR-object residual gap above is a documented usage constraint, not an open finding: canonicalize parsed JSON or primitive-built nodes when the UTF-16 is untrusted.
- The project now relies on one third-party runtime dependency (`SimpleBase`). Existing CI dependency scanning and CodeQL coverage reduce supply-chain blind spots, but periodic version review remains advisable.

## CI/Security Automation Status

Current workflows provide:

- PR dependency review (`actions/dependency-review-action`)
- NuGet vulnerability scan on CI/security workflows
- Scheduled CodeQL analysis (weekly)
- Build + tests + package generation in CI

## Conclusion

The prior "no open findings" snapshot (February 20, 2026) was accurate for the surface it reviewed, but it **predated the JCS canonicalizer entirely** — `JcsCanonicalizer` and its supporting types (`EcmaScriptNumber`, `JcsFormatException`) had never been security-reviewed, and the P-521 multicodec and `Multikey` codec also landed afterward. This refresh reviewed that new surface and surfaced four parser-hardening findings (S1–S4) in the JCS / multibase paths: two HIGH (unbounded recursion / DoS; duplicate-member-name signature confusion), one MEDIUM (invalid-UTF-16 collision), and one LOW/MEDIUM (base64url trailing-bit malleability). All four were found and fixed in `1.6.0`.

Aside from those findings, the library remains security-hardened after `SimpleBase` integration: validation wrappers preserve strict CID parsing semantics, the JCS canonicalizer now fails closed on hostile JSON, malformed inputs fail safely, dependency posture is clean, and automated security checks are in place.

Final status: **No open findings as of this audit; S1–S4 resolved in `1.6.0`; the JCS / key-encoding surface is now reviewed and release-ready.**
