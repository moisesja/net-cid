# Examples

These examples mirror the `js-multiformats/examples` guide in C# using `NetCid`.

## Included Examples

- `examples/cid-interface`: CID create/parse/base conversion/version conversion
- `examples/multicodec-interface`: custom codec shape and CID creation from encoded bytes
- `examples/multihash-interface`: multihash digest usage (`sha2-256`, `sha2-512`)
- `examples/block-interface`: minimal block-style encode/decode/create flow using CID verification
- `examples/multibase-interface`: multibase encoding comparison across all supported bases including base64url
- `examples/did-key-interface`: did:key construction using multicodec key-type prefixing and multibase encoding

## Run

```bash
dotnet run --project examples/cid-interface/CidInterfaceExample.csproj
dotnet run --project examples/multicodec-interface/MulticodecInterfaceExample.csproj
dotnet run --project examples/multihash-interface/MultihashInterfaceExample.csproj
dotnet run --project examples/block-interface/BlockInterfaceExample.csproj
dotnet run --project examples/multibase-interface/MultibaseInterfaceExample.csproj
dotnet run --project examples/did-key-interface/DidKeyInterfaceExample.csproj
```
