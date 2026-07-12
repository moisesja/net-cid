using System.Security.Cryptography;
using System.Text.Json;
using NetCid;

var value = new Dictionary<string, string> { ["hello"] = "world" };
var bytes = JsonSerializer.SerializeToUtf8Bytes(value);

// --- MultihashDigest (high-level API) ---

Console.WriteLine("MultihashDigest (high-level API):\n");

var sha256Digest = MultihashDigest.Sha2_256(bytes);
var cid256 = Cid.CreateV1(Multicodec.DagJson, sha256Digest);

Console.WriteLine($"  sha2-256 digest size: {sha256Digest.DigestLength}");
Console.WriteLine($"  CID (sha2-256): {cid256}");

var sha512Digest = MultihashDigest.Sha2_512(bytes);
var cid512 = Cid.CreateV1(Multicodec.DagJson, sha512Digest);

Console.WriteLine($"  sha2-512 digest size: {sha512Digest.DigestLength}");
Console.WriteLine($"  CID (sha2-512): {cid512}");

// --- Multihash.Encode (low-level wire format) ---

Console.WriteLine("\nMultihash.Encode (low-level wire format):\n");

var rawDigest = SHA256.HashData(bytes);
var multihash = Multihash.Encode(MultihashCode.Sha2_256, rawDigest);

Console.WriteLine($"  Raw SHA-256 digest:  {rawDigest.Length} bytes");
Console.WriteLine($"  Multihash encoded:   {multihash.Length} bytes (varint code + varint length + digest)");
Console.WriteLine($"  Hex: {Convert.ToHexString(multihash).ToLowerInvariant()}");

// Show the difference vs Multicodec.Prefix (which lacks the digest length varint)
var incorrectPrefix = Multicodec.Prefix(MultihashCode.Sha2_256, rawDigest);
Console.WriteLine($"\n  Multicodec.Prefix (WRONG for multihash): {incorrectPrefix.Length} bytes — missing digest length varint");
Console.WriteLine($"  Multihash.Encode  (CORRECT):             {multihash.Length} bytes — includes digest length varint");

// --- Multihash.EncodeBase58Btc (multihash → base58btc in one call) ---

Console.WriteLine("\nMultihash.EncodeBase58Btc (multihash → base58btc in one call):\n");

// Known-answer vector from issue #62
var knownInput = System.Text.Encoding.UTF8.GetBytes("z6MkhaXgBZDvotDkL5257faiztiGiC2QtKLGpbnnEGta2doK");
var knownDigest = SHA256.HashData(knownInput);

// The multibase flag is required: bare base58btc and 'z'-multibase are different wire formats.
var bare = Multihash.EncodeBase58Btc(MultihashCode.Sha2_256, knownDigest, includeMultibasePrefix: false);
var multibase = Multihash.EncodeBase58Btc(MultihashCode.Sha2_256, knownDigest, includeMultibasePrefix: true);

Console.WriteLine($"  bare base58btc (protocol says base58btc(...)):  {bare}");
Console.WriteLine($"  multibase      (protocol says multibase(...)):  {multibase}");
Console.WriteLine($"  Expected bare: QmWYHJqmhJHuzQHMQ33piy86hYQwwNBKEFmCKzRMTi7UHN — match: {bare == "QmWYHJqmhJHuzQHMQ33piy86hYQwwNBKEFmCKzRMTi7UHN"}");

// --- Multihash.Decode round-trip ---

Console.WriteLine("\nMultihash.Decode round-trip:\n");

var (code, decoded) = Multihash.Decode(multihash);
Console.WriteLine($"  Code:          0x{code:X2} (sha2-256)");
Console.WriteLine($"  Digest length: {decoded.Length}");
Console.WriteLine($"  Match:         {rawDigest.SequenceEqual(decoded)}");
