# NetCid

`NetCid` is a C# (`net10.0`) implementation of the [various specifications](SPECIFICATIONS.md).

## Features

- CIDv0 and CIDv1 parsing, encoding, and round-tripping
- CID conversion (`ToV0`, `ToV1`)
- Binary CID decode/encode
- Unsigned varint codec (multiformats-compatible, max 9-byte encoding)
- Multihash model + SHA-256 / SHA-512 hash helpers
- `Multihash.Encode` / `Decode` for spec-compliant multihash wire format (`varint(code) || varint(digestLength) || digest`)
- `Multihash.EncodeBase58Btc` — complete multihash → base58btc text in one call, with bare-vs-`z`-multibase stated explicitly at the call site (see [Multihash vs Multicodec.Prefix](#multihash-vs-multicodecprefix-and-bare-vs-multibase-base58btc))
- Multibase support for:
  - `base58btc` (`z`)
  - `base32` lower/upper (`b` / `B`)
  - `base36` lower/upper (`k` / `K`)
  - `base64url` (`u`)
- Multicodec constants for common CID codecs (`raw`, `dag-pb`, `dag-cbor`, etc.)
- Multicodec key-type constants (`ed25519-pub`, `p256-pub`, `secp256k1-pub`, etc.)
- Multicodec prefix/decode API for varint-tagged byte buffers
- `Multikey` — encode/decode W3C Controlled Identifiers `publicKeyMultibase` (base58btc(varint(keyCodec) ‖ rawKey)) with per-codec key-length validation; one call replaces the manual `Multicodec.Prefix` + `Multibase.Encode` dance for `did:key` construction
- `JcsCanonicalizer` — RFC 8785 JSON Canonicalization Scheme for stable content-addressing of JSON values, and `Cid.FromCanonicalJson` convenience

For the full list of specifications this library implements, the version/reference each targets, the governing body, and that specification's standardization status, see [`SPECIFICATIONS.md`](SPECIFICATIONS.md).

## Install

```bash
dotnet add package NetCid
```

## Quick Start

```csharp
using NetCid;
using System.Text;

// Parse existing CIDs
var v0 = Cid.Parse("QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n");
var v1 = Cid.Parse("bafkreidon73zkcrwdb5iafqtijxildoonbwnpv7dyd6ef3qdgads2jc4su");

// Convert versions
var v0AsV1 = v0.ToV1();
var v1AsV0 = v0AsV1.ToV0();

// Build from content bytes
var content = Encoding.UTF8.GetBytes("hello world");
var cid = Cid.FromContent(content, codec: Multicodec.Raw, hashCode: MultihashCode.Sha2_256);

// Serialize
string text = cid.ToString(); // CIDv1 defaults to base32 lower
byte[] bytes = cid.ToByteArray();

// Content-address a JSON value with stable bytes (JCS / RFC 8785)
var entry = new System.Text.Json.Nodes.JsonObject
{
    ["seq"] = 1,
    ["op"]  = "wallet.mint_identity",
};
var jsonCid = Cid.FromCanonicalJson(entry);
```

## Multihash vs Multicodec.Prefix, and bare vs multibase base58btc

Two API pairs look interchangeable but produce different wire formats. Mixing them up
produces values that look plausible and even round-trip against themselves, yet fail
interoperability (this bit did:webvh SCIDs downstream in
[net-did#95](https://github.com/moisesja/net-did/issues/95)).

**Axis 1 — tagged bytes vs complete multihash.** `Multicodec.Prefix` emits
`varint(codec) || data` with no digest-length varint; a multihash additionally encodes the
digest length. For SHA-256:

```text
Multicodec.Prefix(0x12, digest):   0x12 || <32 bytes>          = 33 bytes (NOT a multihash)
Multihash.Encode(0x12, digest):    0x12 || 0x20 || <32 bytes>  = 34 bytes (multihash)
```

**Axis 2 — bare base58btc vs base58btc multibase.** "base58btc" in a protocol spec does not
automatically mean multibase: multibase adds a separate leading `z`. Beware the differing
defaults — `Multibase.Encode(..., includePrefix: true)` vs
`Multibase.EncodeBase58Btc(..., includePrefix: false)` — and prefer passing the argument
explicitly.

```csharp
// Multicodec-tagged payload (for formats that require only a codec tag)
var tagged = Multicodec.Prefix(codec, payload);

// Complete multihash (algorithm code + digest length + digest)
var multihash = Multihash.Encode(MultihashCode.Sha2_256, sha256Digest);

// Bare base58btc, when the governing protocol says base58btc(...)
var bare = Multibase.Encode(multihash, MultibaseEncoding.Base58Btc, includePrefix: false);

// Multibase base58btc, when the governing protocol says multibase(...)
var selfDescribing = Multibase.Encode(multihash, MultibaseEncoding.Base58Btc, includePrefix: true);

// Recommended one-call composition for multihash → base58btc; the multibase flag is
// required so the intent is visible at every call site.
var scid = Multihash.EncodeBase58Btc(MultihashCode.Sha2_256, sha256Digest, includeMultibasePrefix: false);
```

Known-answer example: SHA-256 of the UTF-8 string
`z6MkhaXgBZDvotDkL5257faiztiGiC2QtKLGpbnnEGta2doK` gives digest
`79d8444cc417275da1aa2ed425afb37ba26353270bb09fa29ef8aa4318e13f41`, multihash bytes
`1220` + digest, bare base58btc `QmWYHJqmhJHuzQHMQ33piy86hYQwwNBKEFmCKzRMTi7UHN`
(multibase form is the same string with a leading `z`). Bare base58btc of a SHA-256
multihash commonly begins with `Qm`.

`Multicodec.Prefix` + `z`-multibase is not wrong everywhere — it is exactly the required
shape for multicodec-tagged public keys (`did:key`, `publicKeyMultibase`); use `Multikey`
for those.

## Specification Notes

Implementation follows the CID spec behavior, including:

- CIDv0 is always `dag-pb` + `sha2-256(32)`
- CIDv1 binary layout: `<cidv1-varint><codec-varint><multihash>`
- CIDv0 string form has no multibase prefix
- CID versions `2` and `3` are treated as reserved/invalid

## Input Limits

Parsing APIs enforce default size limits to reduce memory-pressure risk from untrusted input:

- `Cid.DefaultMaxInputStringLength`
- `Cid.DefaultMaxInputByteLength`
- `Multibase.DefaultMaxInputLength`
- `JcsCanonicalizer.DefaultMaxOutputByteLength`

Overloads on parse/decode methods let callers provide custom limits when needed.

`JcsCanonicalizer` additionally caps JSON nesting depth at 64 levels, throwing `JcsFormatException` on deeper input rather than overflowing the stack on hostile, deeply nested JSON, and caps the canonical output at `DefaultMaxOutputByteLength` (1 MiB) — raise it per call via the `maxOutputBytes` overloads. It also rejects JSON objects with duplicate member names (RFC 8785 builds on I-JSON / RFC 7493, which forbids them), throwing `JcsFormatException` rather than emitting ambiguous, non-canonical output. Strings and member names must be well-formed UTF-16: an unpaired surrogate throws `JcsFormatException` rather than being silently replaced with U+FFFD (which would let two distinct malformed inputs collapse to the same canonical bytes).

References:

- https://github.com/multiformats/cid
- https://multiformats.readthedocs.io/en/latest/api/multiformats.cid.html

## Development

```bash
dotnet restore NetCid.sln
dotnet build NetCid.sln -c Release
dotnet test NetCid.Tests/NetCid.Tests.csproj -c Release
dotnet test NetCid.IntegrationTests/NetCid.IntegrationTests.csproj -c Release
```

## Examples

Reference examples are available under `examples/` and mirror the `js-multiformats` example set:

- `examples/cid-interface`
- `examples/multicodec-interface`
- `examples/multihash-interface`
- `examples/block-interface`
- `examples/multibase-interface`
- `examples/did-key-interface`
- `examples/jcs-interface`

See `examples/README.md` for run commands.

## Contributing

See `contributors.md` for contributor workflow, quality checklist, and PR expectations.

## CI / Release

- CI workflow: `.github/workflows/ci.yml`
- Security workflows: `.github/workflows/security.yml`, `.github/workflows/codeql.yml`
- NuGet publish workflow: `.github/workflows/release.yml`

`release.yml` pushes packages when a tag like `v1.2.3` is pushed (or manual dispatch) and requires `NUGET_API_KEY` repository secret.

## Security

- Responsible disclosure: see `SECURITY.md`
- Security review and findings: see `SECURITY_AUDIT.md`
