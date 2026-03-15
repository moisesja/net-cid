# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Released]

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

[Unreleased]: https://github.com/moisesja/net-cid/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/moisesja/net-cid/compare/v1.2.1...v1.3.0
[1.2.1]: https://github.com/moisesja/net-cid/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/moisesja/net-cid/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/moisesja/net-cid/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/moisesja/net-cid/releases/tag/v1.0.0
