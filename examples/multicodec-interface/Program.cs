using System.Text;
using System.Text.Json;
using NetCid;

var jsonCodec = new SimpleCodec<Dictionary<string, string>>(
    Name: "json",
    Code: 0x0200,
    Encode: static value => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)),
    Decode: static bytes => JsonSerializer.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(bytes))
        ?? throw new InvalidOperationException("Decoded JSON value was null."));

var value = new Dictionary<string, string> { ["hello"] = "world" };
var encoded = jsonCodec.Encode(value);
var decoded = jsonCodec.Decode(encoded);

Console.WriteLine($"Codec name: {jsonCodec.Name}");
Console.WriteLine($"Codec code: 0x{jsonCodec.Code:x}");
Console.WriteLine($"Round-trip value: {JsonSerializer.Serialize(decoded)}");

var cid = Cid.CreateV1(jsonCodec.Code, MultihashDigest.Sha2_256(encoded));
Console.WriteLine($"CID with custom codec: {cid}");

internal sealed record SimpleCodec<T>(
    string Name,
    ulong Code,
    Func<T, byte[]> Encode,
    Func<byte[], T> Decode);
