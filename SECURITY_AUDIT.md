# Security Audit Report

## Executive Summary

Audit date: **February 20, 2026**

This audit is a fresh post-change review after migrating multibase primitives to `SimpleBase` with NetCid wrapper validation.

Overall result: **No open critical, high, medium, low, or informational findings.**

Decision: **Release-ready** from a security perspective, with dependency and parser-hardening controls validated.

## Scope

- Core library source under `NetCid/`
- Unit and integration tests under `NetCid.Tests/` and `NetCid.IntegrationTests/`
- Example projects under `examples/`
- CI/security workflows under `.github/workflows/`
- Dependency posture after adding `SimpleBase` `5.6.0`

## Change Context

Recent security-relevant change:

- Replaced internal base32/base36/base58 primitive implementation with `SimpleBase` in `NetCid/Multibase.cs`.
- Preserved strict wrapper checks for CID semantics:
  - prefix allowlist (`b`, `B`, `k`, `K`, `z`)
  - base32 padding rejection
  - base32 non-zero trailing-bit rejection
  - case-specific base36 validation
  - input-size limits and exception normalization

## Methodology

### 1. Manual secure-code review

Reviewed parsing and error paths in:

- `NetCid/Cid.cs`
- `NetCid/Multibase.cs`
- `NetCid/Varint.cs`
- `NetCid/MultihashDigest.cs`

Focus areas:

- Input boundary enforcement and argument validation
- Parser canonicalization behavior
- Exception safety (`Try*` APIs should fail safely)
- Integer overflow and allocation boundaries
- Cryptographic primitive selection

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

Coverage details:

- `100,000`-iteration ASCII malformed corpus for multibase and CID parse/decode APIs
- `20,000`-iteration random Unicode malformed corpus for `Cid.Parse`, `Cid.TryParse`, and `Multibase.TryDecode`

Purpose:

- Validate malformed input does not trigger unexpected exception classes
- Confirm `Try*` APIs do not throw for malformed input
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
- Unit tests: **33 passed, 0 failed**
- Integration tests: **3 passed, 0 failed**
- No build warnings when building the core project with `-warnaserror`

## Fuzz/negative testing posture

- `Multibase.TryDecode`: **0 throws** across random malformed input corpus (ASCII + Unicode)
- `Multibase.Decode`: **0 unexpected exception types**
- `Multibase.DecodeBase58Btc`: **0 unexpected exception types**
- `Cid.TryParse`: **0 throws**
- `Cid.Parse`: **0 unexpected exception types**
- `Cid.TryDecode`: **0 throws**
- `Cid.Decode`: **0 unexpected exception types**

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
  - base32 trailing non-zero bits rejected
  - base36 case-specific alphabets enforced
- Cryptography:
  - hash generation uses `SHA256.HashData` and `SHA512.HashData`
- Unsafe/runtime interop:
  - no `unsafe` blocks, no P/Invoke/native interop in library code

## Findings

### Open findings

- **None**

### Notes and residual risk (non-finding)

- Base58 and similar positional-base decoding are computationally heavier than fixed-radix encodings; this remains bounded by explicit input-size limits (`4096` chars by default).
- The project now relies on one third-party runtime dependency (`SimpleBase`). Existing CI dependency scanning and CodeQL coverage reduce supply-chain blind spots, but periodic version review remains advisable.

## CI/Security Automation Status

Current workflows provide:

- PR dependency review (`actions/dependency-review-action`)
- NuGet vulnerability scan on CI/security workflows
- Scheduled CodeQL analysis (weekly)
- Build + tests + package generation in CI

## Conclusion

This Round 2 audit confirms the library remains security-hardened after `SimpleBase` integration. Validation wrappers preserve strict CID parsing semantics, malformed inputs fail safely, dependency posture is clean, and automated security checks are in place.

Final status: **No open findings; release-ready.**
