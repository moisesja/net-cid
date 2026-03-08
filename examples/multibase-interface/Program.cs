using System.Text;
using NetCid;

// --- Encode bytes with different multibase encodings ---

var content = Encoding.UTF8.GetBytes("Hello, multiformats!");

Console.WriteLine("Encoding the same bytes with different multibase encodings:\n");

var encodings = new[]
{
    MultibaseEncoding.Base32Lower,
    MultibaseEncoding.Base32Upper,
    MultibaseEncoding.Base36Lower,
    MultibaseEncoding.Base58Btc,
    MultibaseEncoding.Base64Url
};

foreach (var encoding in encodings)
{
    var encoded = Multibase.Encode(content, encoding, includePrefix: true);
    Console.WriteLine($"  {encoding,-12} (prefix '{Multibase.GetPrefix(encoding)}'): {encoded}");
}

// --- Decode with auto-detection ---

Console.WriteLine("\nAuto-detecting encoding from multibase prefix:\n");

var base64UrlEncoded = Multibase.Encode(content, MultibaseEncoding.Base64Url, includePrefix: true);
var decoded = Multibase.Decode(base64UrlEncoded, out var detectedEncoding);

Console.WriteLine($"  Input:    {base64UrlEncoded}");
Console.WriteLine($"  Detected: {detectedEncoding}");
Console.WriteLine($"  Decoded:  {Encoding.UTF8.GetString(decoded)}");

// --- Compare output sizes ---

Console.WriteLine("\nOutput size comparison (20 random bytes):\n");

var randomBytes = new byte[20];
Random.Shared.NextBytes(randomBytes);

foreach (var encoding in encodings)
{
    var encoded = Multibase.Encode(randomBytes, encoding, includePrefix: true);
    Console.WriteLine($"  {encoding,-12}: {encoded.Length,3} chars");
}
