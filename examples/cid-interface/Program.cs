using System.Text.Json;
using NetCid;

var value = new Dictionary<string, string> { ["hello"] = "world" };
var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
var hash = MultihashDigest.Sha2_256(bytes);
var cid = Cid.CreateV1(Multicodec.DagJson, hash);

Console.WriteLine($"Example CID: {cid}");

var cidBase58 = cid.ToString(MultibaseEncoding.Base58Btc);
Console.WriteLine($"base58 encoded CID: {cidBase58}");

var decodedCid = Cid.Parse(cidBase58);
Console.WriteLine($"Decoded CID equal to original base32: {decodedCid == cid}");

var v1 = Cid.Parse(cid.ToString());
Console.WriteLine($"CIDv1 default encoding: {v1}");

var v0 = Cid.Parse("QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n");
Console.WriteLine($"CIDv0 default encoding: {v0}");
Console.WriteLine($"CIDv0 -> CIDv1: {v0.ToV1()}");
