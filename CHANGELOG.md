# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

- `Multibase` now validates the raw base36 payload as ASCII letters/digits **before** case-folding. Previously `ToLowerInvariant`/`ToUpperInvariant` ran first, so the two non-ASCII code points whose invariant fold lands inside a base36 alphabet — U+212A KELVIN SIGN (→ `k`) and U+017F LATIN SMALL LETTER LONG S (→ `S`) — were silently accepted, letting a distinct non-ASCII string decode to the same CID (`Cid.Parse`), the base36 analogue of the #42 string-malleability vector. Such payloads now throw `CidFormatException`; ASCII mixed-case base36 input is still accepted, and a full-BMP scan test pins Kelvin/long-s as the only fold-survivors ([#43](https://github.com/moisesja/net-cid/issues/43)).
- `Multibase` now rejects base32 payloads that carry an incomplete trailing symbol (payload length ≡ {1,3,6} mod 8). A canonical RFC 4648 no-padding base32 length is always ≡ {0,2,4,5,7} mod 8; the previous validator checked only that the unused trailing *bits* were zero, so appending a single zero-valued symbol (`a`/`A`) produced a distinct string that decoded to the same bytes — two distinct strings mapping to one CID (`Cid.Parse`), a CID string-malleability vector. Decoding such a non-canonical payload now throws `CidFormatException`; canonical CIDs are unaffected ([#42](https://github.com/moisesja/net-cid/issues/42)).
- Made the NuGet dependency vulnerability scan fail the build when `dotnet list --vulnerable --include-transitive` reports an advisory of any severity, in both the `ci` and `security` workflows; previously the scan only printed its findings and always exited `0`, so a newly vulnerable transitive dependency could merge cleanly ([#25](https://github.com/moisesja/net-cid/issues/25)).
- Added a release-workflow guard that verifies the pushed git tag matches the package `<Version>` in `NetCid.csproj` before packing and publishing, so a tag/version mismatch fails fast instead of mispublishing or being silently swallowed by `--skip-duplicate` ([#26](https://github.com/moisesja/net-cid/issues/26)).

## [1.6.0] - 2026-06-07

### Added

- `Multikey` static class (`Encode` / `Decode` / `TryDecode`) implementing the W3C Controlled Identifiers 1.0 Multikey verification method and `did:key` `publicKeyMultibase` encoding: `base58btc( varint(keyCodec) ‖ rawPublicKey )`. Validates that the codec is one of the eight supported key-type multicodecs (`ed25519-pub`, `x25519-pub`, `secp256k1-pub`, `p256-pub`, `p384-pub`, `p521-pub`, `bls12_381-g1-pub`, `bls12_381-g2-pub`) and that the raw key length matches that codec; rejects non-base58btc multibases and content-type codecs ([#14](https://github.com/moisesja/net-cid/issues/14))
- Full RFC 8785 §3.2.2.3 / ECMA-262 §6.1.6.1.20 (`Number.prototype.toString`) support in `JcsCanonicalizer` — JSON values containing fractional, exponential, or out-of-`ulong` numbers (monetary amounts, geo coordinates, scores in Verifiable Credentials) now canonicalize, resolving the v1 follow-up tracked from 1.4.0 ([#13](https://github.com/moisesja/net-cid/issues/13))
- Internal `EcmaScriptNumber.ToCanonicalString(double)` helper implementing the ECMA-262 §6.1.6.1.20 algorithm against .NET's shortest-round-trip digit string
- `jcs-number-conformance` workflow that downloads and (when `CONFORMANCE_EXPECTED_SHA256` is set) SHA-256-verifies cyberphone's `es6testfile100m.txt.gz` (100M-vector RFC 8785 conformance set) and runs it on PRs that touch the number formatter, plus `workflow_dispatch` and a weekly backstop. The SHA-256 pin is empty at merge time and tracked as a follow-up
- Deterministic-seed 100k bit-pattern fuzz in `EcmaScriptNumberTests` that runs in every CI build
- `JcsCanonicalizer.DefaultMaxOutputByteLength` (1 MiB) and `maxOutputBytes` overloads on every `Canonicalize` method, mirroring the configurable input-size limits on the CID/Multibase parse paths. Canonicalization whose UTF-8 output would exceed the limit throws `JcsFormatException`; the crossing byte is rejected before it is committed, so a caller-supplied `IBufferWriter<byte>` never receives more than the limit. Callers processing known-safe, larger documents can raise the cap per call ([#16](https://github.com/moisesja/net-cid/issues/16))
- `Cid.FromCanonicalJson` gained an optional `maxOutputBytes` parameter (default `JcsCanonicalizer.DefaultMaxOutputByteLength`) so the convenience CID-from-JSON path can raise the same cap; existing call sites are unaffected ([#16](https://github.com/moisesja/net-cid/issues/16))

### Changed

- JSON integer literals with magnitude greater than 2<sup>53</sup> (the largest integer exactly representable as a double) now round to the nearest IEEE-754 double before serialization, as RFC 8785 §3.2.2.3 / ECMA-262 §6.1.6.1.20 require. Previously such literals were either emitted verbatim (when they fit in `long`/`ulong`) or threw `JcsFormatException` "outside the supported range". Concretely:
  - `"9007199254740993"` (= 2<sup>53</sup>+1) now canonicalizes as `"9007199254740992"` (was `"9007199254740993"`).
  - `"18446744073709551615"` (`ulong.MaxValue`) now canonicalizes as `"18446744073709552000"` (was `"18446744073709551615"`).
  - `"1000000000000000000000"` (> `ulong.MaxValue`) now canonicalizes as `"1e+21"` (was a `JcsFormatException`).
  - The same value written as `9007199254740993` or `9007199254740993.0` now yields identical bytes — the determinism guarantee the v1 fast path silently broke.
  - Literals so large they parse to `±∞` (e.g. a 400-digit integer) still throw the existing infinity error.

### Security

- `JcsCanonicalizer` now enforces a maximum JSON nesting depth of 64 (matching `System.Text.Json`'s default `MaxDepth`). Input nested deeper — in either the `NaN`/`±infinity` validation pre-pass or the core serialization walk — throws `JcsFormatException` instead of recursing without bound and overflowing the stack. Because JCS processes untrusted credential JSON and a `StackOverflowException` cannot be caught in .NET (it terminates the process), the previous unbounded recursion was a denial-of-service vector ([#16](https://github.com/moisesja/net-cid/issues/16))
- `JcsCanonicalizer` also caps canonical output at `DefaultMaxOutputByteLength` (1 MiB) by default, as defense-in-depth against runaway output from untrusted JSON. **Potentially breaking:** callers that previously canonicalized documents producing more than 1 MiB of output must now pass an explicit `maxOutputBytes` (e.g. `int.MaxValue`) — on the new `Canonicalize` overloads or via the new `Cid.FromCanonicalJson` `maxOutputBytes` parameter ([#16](https://github.com/moisesja/net-cid/issues/16))
- `JcsCanonicalizer` now rejects JSON objects with duplicate member names (throws `JcsFormatException`) instead of emitting them. RFC 8785 builds on I-JSON (RFC 7493 §2.3), which forbids duplicate names; `System.Text.Json`'s `JsonDocument.Parse` preserves duplicates, so the previous behavior emitted non-canonical JSON that different parsers could read differently — a signature-confusion vector for the data-integrity pipeline. Both the `JsonElement` overload and the `JsonNode?` / `IBufferWriter<byte>` overloads now fail closed ([#17](https://github.com/moisesja/net-cid/issues/17))
- `JcsCanonicalizer` now rejects strings and object member names containing invalid UTF-16 (an unpaired surrogate) with `JcsFormatException` instead of letting `System.Text.Json` silently substitute U+FFFD. Because an unpaired surrogate has no UTF-8 representation, the previous behavior collapsed two distinct malformed inputs (and a legitimately-supplied U+FFFD) to identical canonical bytes — a collision / signature-confusion vector. The guard fires across both the `JsonNode` and `JsonElement` paths (and `Cid.FromCanonicalJson`), for `string`- and `char`-backed values; valid surrogate pairs and a legitimate U+FFFD are unchanged byte-for-byte. Note: a `JsonValue` wrapping a raw CLR object (e.g. `JsonValue.Create(someObject)`) is expanded by `System.Text.Json`, which substitutes U+FFFD before this validation can inspect its members, so canonicalize parsed JSON or primitive-built nodes when the UTF-16 is untrusted ([#18](https://github.com/moisesja/net-cid/issues/18))
- Reviewed the base64url multibase decode path for the non-canonical trailing-bit (CID malleability) class the base32 path already guards against, and confirmed it is closed: `System.Buffers.Text.Base64Url.DecodeFromChars` rejects (does not mask) non-zero unused trailing bits, so `Multibase.Decode` / `Cid.Parse` reject non-canonical base64url payloads. Added regression tests pinning this and a `SECURITY_AUDIT.md` strictness note; no behavior change ([#19](https://github.com/moisesja/net-cid/issues/19))

## [1.5.0] - 2026-05-22

### Added

- `Multicodec.P521Pub` (`0x1202`) and the `"p521-pub"` name mapping, completing the NIST P-curve public-key set alongside the existing `P256Pub` / `P384Pub` ([#11](https://github.com/moisesja/net-cid/issues/11))

## [1.4.0] - 2026-05-21

### Added

- `JcsCanonicalizer` — RFC 8785 JSON Canonicalization Scheme (JCS) producing a deterministic UTF-8 serialization of any supported JSON value, with overloads for `JsonNode?`, `JsonElement`, and direct `IBufferWriter<byte>` writes ([#9](https://github.com/moisesja/net-cid/issues/9))
- `Cid.FromCanonicalJson(JsonNode?, codec, hashCode)` convenience overload that canonicalizes JSON and computes the resulting CID in one call
- `JcsFormatException` for values JCS cannot represent deterministically (`NaN`, `±infinity`, fractional/exponential numbers in v1, out-of-range integers)

### Notes

- v1 scope covers objects, arrays, strings, integer-valued numbers within `[long.MinValue, ulong.MaxValue]`, booleans, and null. Fractional/IEEE 754 numbers throw `JcsFormatException` — full support landed in [1.6.0](#160---2026-06-07) ([#13](https://github.com/moisesja/net-cid/issues/13)).

## [1.3.0] - 2026-03-15

### Added

- `Multihash.Encode(ulong hashFunctionCode, ReadOnlySpan<byte> digest)` for constructing spec-compliant multihash bytes: `varint(code) || varint(digestLength) || digest` ([#7](https://github.com/moisesja/net-cid/issues/7))
- `Multihash.Decode` and `Multihash.TryDecode` for parsing multihash byte sequences back into code and digest

## [1.2.1] - 2026-03-08

### Fixed

- Base36 decoding is now case-insensitive per the multibase spec, allowing mixed-case payloads (e.g., from DNS systems) to decode correctly ([#3](https://github.com/moisesja/net-cid/issues/3))
- BLS public-key multicodec names corrected from `bls12-381-g1-pub` / `bls12-381-g2-pub` to `bls12_381-g1-pub` / `bls12_381-g2-pub` to match the official multicodec registry ([#5](https://github.com/moisesja/net-cid/issues/5))

## [1.2.0] - 2026-03-08

### Added

- Base64url multibase encoding and decoding (prefix `u`)
- Key-type multicodec constants and name lookups (secp256k1, BLS12-381, x25519, ed25519, P-256, P-384)
- `Multicodec.Prefix` and `Multicodec.Decode` for multicodec-prefixed byte buffers

## [1.1.0] - 2025-11-01

### Added

- Base36 multibase encoding and decoding (prefixes `k` and `K`)

## [1.0.0] - 2025-10-01

### Added

- Initial release with CIDv0 and CIDv1 support
- Base32 and Base58btc multibase encoding/decoding
- SHA-256 and SHA-512 multihash support
- Core multicodec constants (raw, dag-pb, dag-cbor, etc.)

[Unreleased]: https://github.com/moisesja/net-cid/compare/v1.6.0...HEAD
[1.6.0]: https://github.com/moisesja/net-cid/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/moisesja/net-cid/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/moisesja/net-cid/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/moisesja/net-cid/compare/v1.2.1...v1.3.0
[1.2.1]: https://github.com/moisesja/net-cid/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/moisesja/net-cid/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/moisesja/net-cid/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/moisesja/net-cid/releases/tag/v1.0.0
