# Architecture

## Objectives

NetCid provides a spec-compliant, production-ready implementation of the [multiformats](https://multiformats.io/) Content Identifier (CID) stack for .NET. The library enables .NET applications to create, parse, encode, and exchange CIDs that are interoperable with implementations in other languages (JavaScript, Go, Rust).

The library also serves as a shared foundation for higher-level protocols that depend on multiformats primitives — including the W3C Decentralized Identifier (DID) ecosystem, IPFS, and Verifiable Credentials.

## Requirements

### Spec Conformance

| Specification | Coverage |
|---------------|----------|
| [CID](https://github.com/multiformats/cid) | CIDv0 and CIDv1 create, parse, encode, decode, version conversion |
| [Multibase](https://github.com/multiformats/multibase) | base32 (lower/upper), base36 (lower/upper), base58btc, base64url |
| [Multicodec](https://github.com/multiformats/multicodec) | Content-type codecs (dag-pb, raw, dag-cbor, etc.), key-type codecs (ed25519-pub, p256-pub, etc.), varint prefix/decode API |
| [Multihash](https://github.com/multiformats/multihash) | Digest model, SHA-256, SHA-512 |
| [Unsigned Varint](https://github.com/multiformats/unsigned-varint) | Encode/decode, max 9-byte encoding, canonical form validation |
| [RFC 8785 (JCS)](https://www.rfc-editor.org/rfc/rfc8785) | JSON Canonicalization Scheme: member-name sorting, ECMA-262 number serialization, UTF-8 output; rejects NaN/±∞, unpaired surrogates, duplicate member names, and over-deep nesting |
| [W3C Controlled Identifiers / Multikey](https://www.w3.org/TR/controlled-identifiers/) | `Multikey` / `did:key` `publicKeyMultibase` encode/decode over base58btc with per-codec raw-key-length validation |

### Design Goals

- **Correctness** — Strict adherence to multiformats specifications, including canonical varint encoding and input validation.
- **Safety** — Input size limits on all parse/decode entry points to prevent memory-pressure attacks from untrusted data.
- **Efficiency** — `Span<T>`-based APIs to minimize allocations on hot paths.
- **Simplicity** — Static utility classes with no dependency injection or complex object graphs.
- **Interoperability** — Byte-level compatibility with reference implementations (`js-multiformats`, `go-cid`).

## Module Design

```
┌───────────────────────────────────────┐   ┌──────────────────────────────┐
│                  Cid                   │   │             JCS              │
│  Top-level model: version + codec +    │   │  JcsCanonicalizer            │
│  multihash                             │◀──│  + EcmaScriptNumber          │
│  Parse / Decode / Create / ToString /  │   │  + JcsFormatException        │
│  ToByteArray / FromCanonicalJson       │   │  RFC 8785 JSON → UTF-8 bytes │
├──────────────┬──────────────┬──────────┤   └──────────────────────────────┘
│  Multibase   │  Multicodec  │ Multihash │     Cid.FromCanonicalJson(json)
│  Encode text │  Codec IDs   │ Digest    │     canonicalizes JSON, then
│  Decode text │  Prefix API  │ SHA-2     │     hashes it into a CID.
├──────────────┴──────┬───────┴──────────┤
│                  Varint                 │
│  Unsigned varint encode / decode        │
│  (LEB128 variant)                        │
└──────────────────────────────────────────┘
```

### Class Responsibilities

| Class | File | Responsibility |
|-------|------|----------------|
| `Varint` | `NetCid/Varint.cs` | Foundational layer. Encodes/decodes unsigned variable-length integers per the multiformats unsigned-varint spec. All numeric fields in CID binary format flow through this. |
| `MultihashCode` | `NetCid/MultihashCode.cs` | Constants for hash function identifiers (SHA-256 = `0x12`, SHA-512 = `0x13`, etc.). |
| `MultihashDigest` | `NetCid/MultihashDigest.cs` | Immutable model representing a multihash: `[varint(code)][varint(digestLength)][digest]`. Provides `Sha2_256()` and `Sha2_512()` factory methods. |
| `Multihash` | `NetCid/Multihash.cs` | Static `Encode()` / `Decode()` / `TryDecode()` helpers over the raw multihash byte form (`varint(code) || varint(length) || digest`), delegating to `MultihashDigest` for callers that want bytes/tuples rather than the model. |
| `Multicodec` | `NetCid/Multicodec.cs` | Constants for content-type codecs (dag-pb, raw, etc.) and key-type codecs (ed25519-pub, p256-pub, etc.). Bidirectional name/code lookup table. `Prefix()` / `Decode()` / `TryDecode()` API for varint-tagging arbitrary byte buffers. |
| `Multikey` | `NetCid/Multikey.cs` | W3C Controlled Identifiers Multikey / `did:key` `publicKeyMultibase` helpers. `Encode()` / `Decode()` / `TryDecode()` compose `Multicodec.Prefix` + `Multibase` (base58btc, strict) and add per-codec raw-key-length validation for the eight supported public-key codecs. |
| `MultibaseEncoding` | `NetCid/MultibaseEncoding.cs` | Enum of supported base encodings. |
| `Multibase` | `NetCid/Multibase.cs` | Encodes byte arrays to multibase-prefixed strings and decodes them back. Supports base32, base36, base58btc, and base64url. Auto-detects encoding from the single-character prefix on decode. |
| `CidVersion` | `NetCid/CidVersion.cs` | Enum: `V0`, `V1`. |
| `Cid` | `NetCid/Cid.cs` | Top-level CID model composing version + codec + multihash. Provides `Parse()` / `TryParse()` (from strings), `Decode()` / `TryDecode()` (from bytes), `CreateV0()` / `CreateV1()` / `FromContent()` (construction), `ToString()` / `ToByteArray()` (serialization), and `ToV0()` / `ToV1()` (version conversion). |
| `CidFormatException` | `NetCid/CidFormatException.cs` | Domain-specific `FormatException` subclass for all parse/decode failures. |
| `JcsCanonicalizer` | `NetCid/JcsCanonicalizer.cs` | RFC 8785 JSON canonicalization → stable UTF-8 bytes for hashing/signing. `Canonicalize()` over `string` / `JsonElement` / `JsonNode`; sorts member names, normalizes numbers, rejects duplicate keys, unpaired surrogates, and over-deep nesting. |
| `EcmaScriptNumber` | `NetCid/EcmaScriptNumber.cs` | Internal ECMA-262 §6.1.6.1.20 number-to-string helper used by JCS (RFC 8785 §3.2.2.3) to render IEEE-754 doubles in the exact canonical textual form. |
| `JcsFormatException` | `NetCid/JcsFormatException.cs` | `FormatException` subclass for values JCS cannot represent deterministically (NaN/±∞, unpaired surrogates, duplicate member names, over-deep or over-large input). |

### Encoding Flow

**Creating a CID string from content bytes:**

```
content bytes
    │
    ▼
MultihashDigest.Sha2_256(content)     ← hash the content
    │
    ▼
Cid.CreateV1(codec, multihash)        ← compose CID model
    │
    ▼
cid.ToByteArray()                     ← binary: [varint(1)][varint(codec)][multihash-bytes]
    │
    ▼
Multibase.Encode(cidBytes, encoding)  ← text: prefix + base-encoded string
```

**Parsing a CID string back:**

```
multibase-prefixed string (e.g. "bafkrei...")
    │
    ▼
Multibase.Decode(text)                ← strip prefix, decode base → raw bytes
    │
    ▼
Cid.Decode(bytes)                     ← parse: varint(version), varint(codec), multihash
    │
    ▼
Cid instance                          ← .Version, .Codec, .Multihash accessible
```

**Multicodec prefix/decode flow (used by DID methods):**

```
raw public key bytes
    │
    ▼
Multicodec.Prefix(Ed25519Pub, rawKey) ← [varint(0xED)] + rawKey
    │
    ▼
Multibase.Encode(prefixed, Base58Btc) ← "z" + base58btc-encoded
    │
    ▼
"did:key:z..."                        ← full did:key identifier
```

`Multikey.Encode(Ed25519Pub, rawKey)` is the one-call equivalent of the
two middle steps — it also validates the codec is one of the eight
supported key types and that `rawKey.Length` matches that codec, so DID
consumers get a single API instead of hand-assembling the prefix.

## Design Decisions

### Static Classes

All utility classes (`Varint`, `Multibase`, `Multicodec`, `MultihashCode`, `JcsCanonicalizer`) are static. These are pure functions over byte (or JSON) data with no state — there is nothing to inject or configure. This keeps the API surface minimal and usage straightforward.

### JSON Canonicalization (JCS)

`JcsCanonicalizer` lives in this library because content-addressing of JSON requires the *same* deterministic byte sequence that CID hashing already depends on. To hash, sign, or compute a CID over a JSON document, two independent writers must produce byte-for-byte identical output for the same logical value; RFC 8785 (JCS) defines that canonical form (sorted member names, normalized number text, UTF-8). Co-locating it with the multiformats stack lets `Cid.FromCanonicalJson` go straight from JSON to a CID without callers reaching for a separate canonicalization dependency, and reuses the same defense-in-depth posture (depth and output-size caps) as the CID/Multibase parse paths.

The v1 canonicalizer handled integer-valued numbers only. Version 1.6.0 widened it to the full ECMA-262 §6.1.6.1.20 number scope (the textual form RFC 8785 §3.2.2.3 mandates for IEEE-754 doubles), via the `EcmaScriptNumber` helper, while continuing to reject values JCS cannot represent deterministically (`NaN`, `±∞`) with `JcsFormatException`.

### Span-Based APIs

Public methods accept `ReadOnlySpan<byte>` or `ReadOnlySpan<char>` where possible. This allows callers to pass slices of larger buffers without allocating intermediate arrays, which matters for high-throughput CID processing (e.g., IPFS block stores).

### SimpleBase Dependency

Base encoding/decoding (base32, base36, base58) delegates to the [SimpleBase](https://www.nuget.org/packages/SimpleBase) library rather than rolling custom implementations. SimpleBase is well-tested and handles the non-trivial edge cases in positional base encodings (leading zeros, alphabet validation). Base64url uses .NET's built-in `System.Buffers.Text.Base64Url` (available since .NET 8).

### Input Limits

All parse/decode entry points enforce configurable maximum input sizes (`DefaultMaxInputStringLength`, `DefaultMaxInputByteLength`, `DefaultMaxInputLength`). This is a defense-in-depth measure for applications that parse CIDs from untrusted network input — a maliciously large input could otherwise cause excessive memory allocation. Callers can override limits via method overloads when processing known-safe data.

`JcsCanonicalizer` applies the same defense-in-depth posture to nesting: it caps JSON nesting depth at 64 (matching `System.Text.Json`'s default `MaxDepth`) and throws `JcsFormatException` on deeper input. Without the cap, deeply nested untrusted JSON would recurse without bound and overflow the stack — and a `StackOverflowException` cannot be caught in .NET, so it terminates the process (a denial-of-service vector, since JCS processes untrusted credential JSON). It additionally caps the canonical UTF-8 output at `DefaultMaxOutputByteLength` (1 MiB), enforced through a single counting `IBufferWriter<byte>` wrapper that rejects the crossing byte before it is committed; callers can raise the cap via the `maxOutputBytes` overloads when processing known-safe data.

### Try-Pattern Methods

Every throwing parse/decode method has a corresponding `TryParse` / `TryDecode` variant that returns `bool` instead of throwing. This follows .NET conventions and lets callers choose between exception-based and return-code-based error handling without a performance penalty.

### Immutable Models

`Cid` and `MultihashDigest` are immutable. Once constructed, their byte representations are fixed. This makes them safe to cache, share across threads, and use as dictionary keys.
