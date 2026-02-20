using System.Text.Json;
using NetCid;

var value = new Dictionary<string, string> { ["hello"] = "world" };
var bytes = JsonSerializer.SerializeToUtf8Bytes(value);

var sha256Digest = MultihashDigest.Sha2_256(bytes);
var cid256 = Cid.CreateV1(Multicodec.DagJson, sha256Digest);

Console.WriteLine($"sha2-256 digest size: {sha256Digest.DigestLength}");
Console.WriteLine($"CID (sha2-256): {cid256}");

var sha512Digest = MultihashDigest.Sha2_512(bytes);
var cid512 = Cid.CreateV1(Multicodec.DagJson, sha512Digest);

Console.WriteLine($"sha2-512 digest size: {sha512Digest.DigestLength}");
Console.WriteLine($"CID (sha2-512): {cid512}");
