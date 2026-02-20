# Security Audit Report

## Executive Summary

Audit date: **February 20, 2026**

Scope covered:

- Library source under `NetCid/`
- Unit/integration tests
- Examples under `examples/`
- CI/CD and security workflows
- NuGet package metadata and release pipeline

Overall result: **No open critical, high, medium, low, or informational findings.**

## Methodology

1. Manual secure-code review
   - Input validation and parser hardening (`Cid`, `Multibase`, `Varint`, `MultihashDigest`)
   - Error-path behavior and malformed input handling
   - Cryptographic usage (`SHA256.HashData`, `SHA512.HashData`)
   - Allocation and parsing boundary checks
2. Static analysis and build checks
   - `dotnet build NetCid/NetCid.csproj -c Release --no-restore --tl:off`
3. Dynamic tests
   - `dotnet test NetCid.Tests/NetCid.Tests.csproj -c Release --no-build --tl:off`
   - `dotnet test NetCid.IntegrationTests/NetCid.IntegrationTests.csproj -c Release --no-build --tl:off`
4. Supply chain checks
   - `dotnet list NetCid.sln package --vulnerable --include-transitive`
   - `dotnet list NetCid.sln package --deprecated`
5. Example validation
   - `dotnet run --project examples/cid-interface/CidInterfaceExample.csproj -c Release --no-build`
   - `dotnet run --project examples/multicodec-interface/MulticodecInterfaceExample.csproj -c Release --no-build`
   - `dotnet run --project examples/multihash-interface/MultihashInterfaceExample.csproj -c Release --no-build`
   - `dotnet run --project examples/block-interface/BlockInterfaceExample.csproj -c Release --no-build`

## Resolved Findings

### Finding 1: Deprecated Test Framework Package (Resolved)

- Previous issue: `xunit` v2 package flagged as legacy/deprecated.
- Resolution: migrated tests to `xunit.v3` and updated `xunit.runner.visualstudio`.
- Verification: `dotnet list NetCid.sln package --deprecated` reports no deprecated packages.

### Finding 2: Unbounded Input Size Could Increase Memory Pressure (Resolved)

- Previous issue: parsing APIs accepted unbounded external input sizes.
- Resolution:
  - Added default input size limits in parsing surfaces:
    - `Cid.DefaultMaxInputStringLength`
    - `Cid.DefaultMaxInputByteLength`
    - `Multibase.DefaultMaxInputLength`
  - Added overloads to `Cid.Parse/TryParse/Decode/TryDecode` and `Multibase.Decode/TryDecode/DecodeBase58Btc` to enforce/max-configure limits.
  - Added unit tests asserting oversized input rejection.
- Verification: unit tests include oversized-input scenarios and pass.

## Current Security Controls

- Strict CID version handling (`v2` and `v3` rejected as reserved)
- CIDv0 canonical constraints enforced (`sha2-256`, 32-byte digest)
- Explicit input-size limits on public parsing APIs
- Unsigned varint decoder rejects non-canonical/oversized encodings
- Multibase decoder validates alphabet and trailing bits
- No unsafe blocks or native interop used
- Deterministic package builds enabled
- Security CI workflows included:
  - Dependency review on PRs
  - NuGet vulnerability scanning
  - Scheduled CodeQL analysis

## Supply Chain Results

- Vulnerability scan: **no vulnerable packages found**
- Deprecated packages: **none**

## Conclusion

The previously reported findings are fully resolved. The library is currently release-ready from a security and supply-chain perspective, with input boundary enforcement and non-deprecated test dependencies in place.
