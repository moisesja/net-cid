using NetCid;

// --- Simulate an Ed25519 public key (32 bytes) ---

var rawPublicKey = new byte[32];
Random.Shared.NextBytes(rawPublicKey);

Console.WriteLine("=== did:key construction ===\n");
Console.WriteLine($"  Raw Ed25519 public key: {Convert.ToHexString(rawPublicKey).ToLowerInvariant()}");

// --- Step 1: Prefix the raw key with the multicodec tag ---

var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, rawPublicKey);
Console.WriteLine($"  Multicodec-prefixed:    {Convert.ToHexString(prefixed).ToLowerInvariant()}");

// --- Step 2: Encode with base58btc for did:key format ---

var multibaseEncoded = Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);
var didKey = $"did:key:{multibaseEncoded}";
Console.WriteLine($"  did:key identifier:     {didKey}");

// --- Step 3: Decode it back ---

Console.WriteLine("\n=== Decoding did:key ===\n");

var multibasePart = didKey["did:key:".Length..];
var decodedPrefixed = Multibase.Decode(multibasePart, out var encoding);
Console.WriteLine($"  Multibase encoding:     {encoding}");

var (codec, recoveredKey) = Multicodec.Decode(decodedPrefixed);
Multicodec.TryGetName(codec, out var codecName);
Console.WriteLine($"  Key type codec:         0x{codec:X} ({codecName})");
Console.WriteLine($"  Recovered public key:   {Convert.ToHexString(recoveredKey).ToLowerInvariant()}");
Console.WriteLine($"  Keys match:             {rawPublicKey.AsSpan().SequenceEqual(recoveredKey)}");

// --- Step 4: base64url encoding for Data Integrity proofs ---

Console.WriteLine("\n=== Alternative encoding (base64url for Data Integrity) ===\n");

var base64UrlEncoded = Multibase.Encode(prefixed, MultibaseEncoding.Base64Url, includePrefix: true);
Console.WriteLine($"  base64url (multibase):  {base64UrlEncoded}");

// Round-trip verification
var decodedFromBase64Url = Multibase.Decode(base64UrlEncoded, out var encoding2);
var (codec2, recoveredKey2) = Multicodec.Decode(decodedFromBase64Url);
Console.WriteLine($"  Detected encoding:      {encoding2}");
Console.WriteLine($"  Round-trip matches:     {rawPublicKey.AsSpan().SequenceEqual(recoveredKey2)}");
