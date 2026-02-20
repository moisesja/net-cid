# NetCid

`NetCid` is a C# (`net10.0`) implementation of the [multiformats CID specification](https://github.com/multiformats/cid).

## Features

- CIDv0 and CIDv1 parsing, encoding, and round-tripping
- CID conversion (`ToV0`, `ToV1`)
- Binary CID decode/encode
- Unsigned varint codec (multiformats-compatible, max 9-byte encoding)
- Multihash model + SHA-256 / SHA-512 hash helpers
- Multibase support for:
  - `base58btc` (`z`)
  - `base32` lower/upper (`b` / `B`)
  - `base36` lower/upper (`k` / `K`)
- Multicodec constants for common CID codecs (`raw`, `dag-pb`, `dag-cbor`, etc.)

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
```

## Specification Notes

Implementation follows the CID spec behavior, including:

- CIDv0 is always `dag-pb` + `sha2-256(32)`
- CIDv1 binary layout: `<cidv1-varint><codec-varint><multihash>`
- CIDv0 string form has no multibase prefix
- CID versions `2` and `3` are treated as reserved/invalid

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

## CI / Release

- CI workflow: `.github/workflows/ci.yml`
- Security workflows: `.github/workflows/security.yml`, `.github/workflows/codeql.yml`
- NuGet publish workflow: `.github/workflows/release.yml`

`release.yml` pushes packages when a tag like `v1.2.3` is pushed (or manual dispatch) and requires `NUGET_API_KEY` repository secret.

## Security

- Responsible disclosure: see `SECURITY.md`
- Security review and findings: see `SECURITY_AUDIT.md`
