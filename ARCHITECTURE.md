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

### Design Goals

- **Correctness** — Strict adherence to multiformats specifications, including canonical varint encoding and input validation.
- **Safety** — Input size limits on all parse/decode entry points to prevent memory-pressure attacks from untrusted data.
- **Efficiency** — `Span<T>`-based APIs to minimize allocations on hot paths.
- **Simplicity** — Static utility classes with no dependency injection or complex object graphs.
- **Interoperability** — Byte-level compatibility with reference implementations (`js-multiformats`, `go-cid`).

## Module Design

```
┌─────────────────────────────────────────────────────┐
│                       Cid                           │
│  Top-level model: version + codec + multihash       │
│  Parse / Decode / Create / ToString / ToByteArray   │
├──────────────┬──────────────┬───────────────────────┤
│  Multibase   │  Multicodec  │   MultihashDigest     │
│  Encode text │  Codec IDs   │   Hash code + digest  │
│  Decode text │  Prefix API  │   SHA-256 / SHA-512   │
├──────────────┴──────┬───────┴───────────────────────┤
│                  Varint                              │
│  Unsigned varint encode / decode (LEB128 variant)   │
└─────────────────────────────────────────────────────┘
```

### Class Responsibilities

| Class | File | Responsibility |
|-------|------|----------------|
| `Varint` | `NetCid/Varint.cs` | Foundational layer. Encodes/decodes unsigned variable-length integers per the multiformats unsigned-varint spec. All numeric fields in CID binary format flow through this. |
| `MultihashCode` | `NetCid/MultihashCode.cs` | Constants for hash function identifiers (SHA-256 = `0x12`, SHA-512 = `0x13`, etc.). |
| `MultihashDigest` | `NetCid/MultihashDigest.cs` | Immutable model representing a multihash: `[varint(code)][varint(digestLength)][digest]`. Provides `Sha2_256()` and `Sha2_512()` factory methods. |
| `Multicodec` | `NetCid/Multicodec.cs` | Constants for content-type codecs (dag-pb, raw, etc.) and key-type codecs (ed25519-pub, p256-pub, etc.). Bidirectional name/code lookup table. `Prefix()` / `Decode()` / `TryDecode()` API for varint-tagging arbitrary byte buffers. |
| `MultibaseEncoding` | `NetCid/MultibaseEncoding.cs` | Enum of supported base encodings. |
| `Multibase` | `NetCid/Multibase.cs` | Encodes byte arrays to multibase-prefixed strings and decodes them back. Supports base32, base36, base58btc, and base64url. Auto-detects encoding from the single-character prefix on decode. |
| `CidVersion` | `NetCid/CidVersion.cs` | Enum: `V0`, `V1`. |
| `Cid` | `NetCid/Cid.cs` | Top-level CID model composing version + codec + multihash. Provides `Parse()` / `TryParse()` (from strings), `Decode()` / `TryDecode()` (from bytes), `CreateV0()` / `CreateV1()` / `FromContent()` (construction), `ToString()` / `ToByteArray()` (serialization), and `ToV0()` / `ToV1()` (version conversion). |
| `CidFormatException` | `NetCid/CidFormatException.cs` | Domain-specific `FormatException` subclass for all parse/decode failures. |

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

## Design Decisions

### Static Classes

All utility classes (`Varint`, `Multibase`, `Multicodec`, `MultihashCode`) are static. These are pure functions over byte data with no state — there is nothing to inject or configure. This keeps the API surface minimal and usage straightforward.

### Span-Based APIs

Public methods accept `ReadOnlySpan<byte>` or `ReadOnlySpan<char>` where possible. This allows callers to pass slices of larger buffers without allocating intermediate arrays, which matters for high-throughput CID processing (e.g., IPFS block stores).

### SimpleBase Dependency

Base encoding/decoding (base32, base36, base58) delegates to the [SimpleBase](https://www.nuget.org/packages/SimpleBase) library rather than rolling custom implementations. SimpleBase is well-tested and handles the non-trivial edge cases in positional base encodings (leading zeros, alphabet validation). Base64url uses .NET's built-in `System.Buffers.Text.Base64Url` (available since .NET 8).

### Input Limits

All parse/decode entry points enforce configurable maximum input sizes (`DefaultMaxInputStringLength`, `DefaultMaxInputByteLength`, `DefaultMaxInputLength`). This is a defense-in-depth measure for applications that parse CIDs from untrusted network input — a maliciously large input could otherwise cause excessive memory allocation. Callers can override limits via method overloads when processing known-safe data.

### Try-Pattern Methods

Every throwing parse/decode method has a corresponding `TryParse` / `TryDecode` variant that returns `bool` instead of throwing. This follows .NET conventions and lets callers choose between exception-based and return-code-based error handling without a performance penalty.

### Immutable Models

`Cid` and `MultihashDigest` are immutable. Once constructed, their byte representations are fixed. This makes them safe to cache, share across threads, and use as dictionary keys.
