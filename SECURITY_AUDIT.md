# Security Audit Report

## Executive Summary

Audit date: **February 20, 2026**

Scope covered:

- Library source under `NetCid/`
- Unit/integration tests
- CI/CD and security workflows
- NuGet package metadata and release pipeline

Overall result: **No critical or high-risk issues identified** in the shipped library code. One low-risk maintenance finding was identified in test dependencies.

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
5. CI security controls review
   - `.github/workflows/security.yml`
   - `.github/workflows/codeql.yml`

## Findings

### Finding 1: Deprecated Test Framework Package (Low)

- Severity: **Low**
- Component: `NetCid.Tests`, `NetCid.IntegrationTests`
- Detail: `xunit` v2 is flagged as legacy/deprecated by NuGet metadata.
- Impact: No production runtime impact (test-only dependency), but future maintenance risk.
- Recommendation: Upgrade tests to xUnit v3 when migration is scheduled.

### Finding 2: Unbounded Input Size Could Increase Memory Pressure (Informational)

- Severity: **Informational**
- Component: parsing APIs accepting arbitrary external input strings/bytes
- Detail: Extremely large attacker-controlled input may cause high memory use before rejection.
- Impact: Potential denial-of-service risk in hostile input environments.
- Recommendation: Enforce caller-side maximum CID input lengths at trust boundaries (API gateway, transport parser, etc.).

## Implemented Security Controls

- Strict CID version handling (`v2` and `v3` rejected as reserved)
- CIDv0 canonical constraints enforced (`sha2-256`, 32-byte digest)
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
- Deprecated packages: **xunit (test projects only)**

## Conclusion

The library is ready for release from a security posture perspective with current controls. Addressing the low-risk test dependency deprecation and optionally adding input-size limits would further strengthen long-term maintainability and resilience.
