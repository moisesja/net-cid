namespace NetCid.Tests;

public sealed class MultibaseTests
{
    private const string KnownCidBytesHex = "015512206e6ff7950a36187a801613426e858dce686cd7d7e3c0fc42ee0330072d245c95";
    private const string KnownCidBase58 = "zb2rhe5P4gXftAwvA4eXQ5HJwsER2owDyS9sKaQRRVQPn93bA";
    private const string KnownCidBase32 = "bafkreidon73zkcrwdb5iafqtijxildoonbwnpv7dyd6ef3qdgads2jc4su";

    [Fact]
    public void Encode_ProducesKnownBase58Vector()
    {
        var bytes = Convert.FromHexString(KnownCidBytesHex);
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base58Btc, includePrefix: true);

        Assert.Equal(KnownCidBase58, encoded);
    }

    [Fact]
    public void Encode_ProducesKnownBase32Vector()
    {
        var bytes = Convert.FromHexString(KnownCidBytesHex);
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base32Lower, includePrefix: true);

        Assert.Equal(KnownCidBase32, encoded);
    }

    [Fact]
    public void Decode_DecodesKnownBase58Vector()
    {
        var decoded = Multibase.Decode(KnownCidBase58, out var encoding);

        Assert.Equal(MultibaseEncoding.Base58Btc, encoding);
        Assert.Equal(KnownCidBytesHex, Convert.ToHexString(decoded).ToLowerInvariant());
    }

    [Fact]
    public void Decode_ThrowsOnUnsupportedPrefix()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("fabc"));
    }

    [Fact]
    public void Decode_ThrowsOnInvalidBase32Character()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("babc!"));
    }

    [Fact]
    public void Decode_ThrowsOnBase32Padding()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("bmfrgg==="));
    }

    [Fact]
    public void Decode_ThrowsOnInvalidBase32TrailingBits()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("bc"));
    }

    [Fact]
    public void EncodeDecode_Base36RoundTrip()
    {
        var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 255 };
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base36Lower, includePrefix: true);
        var decoded = Multibase.Decode(encoded, out var encoding);

        Assert.Equal(MultibaseEncoding.Base36Lower, encoding);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Decode_AcceptsMixedCasePayloadForLowerBase36()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var canonical = Multibase.Encode(bytes, MultibaseEncoding.Base36Lower, includePrefix: true);
        var mixedCase = "k" + canonical[1..].ToUpperInvariant();

        var decoded = Multibase.Decode(mixedCase, out var encoding);

        Assert.Equal(MultibaseEncoding.Base36Lower, encoding);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Decode_AcceptsMixedCasePayloadForUpperBase36()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var canonical = Multibase.Encode(bytes, MultibaseEncoding.Base36Upper, includePrefix: true);
        var mixedCase = "K" + canonical[1..].ToLowerInvariant();

        var decoded = Multibase.Decode(mixedCase, out var encoding);

        Assert.Equal(MultibaseEncoding.Base36Upper, encoding);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Decode_RejectsOversizedInput()
    {
        var oversized = "z" + new string('1', Multibase.DefaultMaxInputLength);

        var exception = Assert.Throws<CidFormatException>(() => Multibase.Decode(oversized));
        Assert.Contains("supported multibase", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodeBase58Btc_RejectsOversizedInput()
    {
        var oversized = new string('1', Multibase.DefaultMaxInputLength + 1);

        var exception = Assert.Throws<CidFormatException>(() => Multibase.DecodeBase58Btc(oversized));
        Assert.Contains("exceeds the allowed limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encode_ProducesKnownBase64UrlVector()
    {
        var bytes = Convert.FromHexString(KnownCidBytesHex);
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base64Url, includePrefix: true);

        Assert.StartsWith("u", encoded);
        // Decode back and verify round-trip
        var decoded = Multibase.Decode(encoded, out var encoding);
        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void EncodeDecode_Base64UrlRoundTrip()
    {
        var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 255 };
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base64Url, includePrefix: true);
        var decoded = Multibase.Decode(encoded, out var encoding);

        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Decode_RecognizesBase64UrlPrefix()
    {
        // "hello" in base64url is "aGVsbG8"
        var encoded = "u" + "aGVsbG8";
        var decoded = Multibase.Decode(encoded, out var encoding);

        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Equal("hello"u8.ToArray(), decoded);
    }

    [Fact]
    public void Decode_ThrowsOnInvalidBase64UrlCharacter_Padding()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("uaGVsbG8="));
    }

    [Fact]
    public void Decode_ThrowsOnInvalidBase64UrlCharacter_Plus()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("uaGV+bG8"));
    }

    [Fact]
    public void Decode_ThrowsOnInvalidBase64UrlCharacter_Slash()
    {
        Assert.Throws<CidFormatException>(() => Multibase.Decode("uaGV/bG8"));
    }

    [Fact]
    public void EncodeDecode_Base64UrlEmptyInput()
    {
        var bytes = Array.Empty<byte>();
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base64Url, includePrefix: true);

        Assert.Equal("u", encoded);

        var decoded = Multibase.Decode(encoded, out var encoding);
        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Empty(decoded);
    }

    [Theory]
    [InlineData("uAB")]   // 2-char group; final char's low 4 bits = 0001
    [InlineData("uAP")]   // 2-char group; final char's low 4 bits = 1111
    [InlineData("uAAB")]  // 3-char group; final char's low 2 bits = 01
    [InlineData("uAAC")]  // 3-char group; final char's low 2 bits = 10
    public void Decode_ThrowsOnInvalidBase64UrlTrailingBits(string nonCanonical)
    {
        // System.Buffers.Text.Base64Url.DecodeFromChars validates (does not mask) the unused
        // trailing bits of the final partial group, so a non-canonical base64url payload is
        // rejected — the same CID-malleability class the base32 path closes
        // (cf. Decode_ThrowsOnInvalidBase32TrailingBits). See issue #19.
        Assert.Throws<CidFormatException>(() => Multibase.Decode(nonCanonical));
    }

    [Fact]
    public void Decode_RejectsBase64UrlMalleabilityCollision()
    {
        // "uAA" and "uAB" would both decode to the single byte 0x00 if the decoder masked the
        // unused trailing bits. The canonical form decodes; the non-canonical form must throw,
        // so two distinct strings cannot collide to the same CID bytes.
        var canonical = Multibase.Decode("uAA", out var encoding);
        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Equal(new byte[] { 0x00 }, canonical);

        Assert.Throws<CidFormatException>(() => Multibase.Decode("uAB"));
    }

    [Theory]
    [InlineData("uAA", 0x00)]  // canonical single zero byte
    [InlineData("uAQ", 0x01)]  // canonical single byte 0x01 (final char's trailing bits legitimately zero)
    public void Decode_AcceptsCanonicalShortBase64UrlPayload(string canonical, int expected)
    {
        var decoded = Multibase.Decode(canonical, out var encoding);

        Assert.Equal(MultibaseEncoding.Base64Url, encoding);
        Assert.Equal(new[] { (byte)expected }, decoded);
    }

    [Fact]
    public void Decode_RejectsBase32IncompleteTrailingSymbolCollision()
    {
        // A canonical base32 CID has a payload length ≡ {0,2,4,5,7} mod 8. Appending one zero-valued
        // symbol ('a') makes it ≡ 3 mod 8 — a whole incomplete trailing symbol that SimpleBase would
        // silently drop, decoding to the SAME bytes as the canonical form. That is CID string
        // malleability: two distinct strings → one CID. The canonical form decodes; the mutated form
        // must throw. See issue #42.
        var canonical = Multibase.Decode(KnownCidBase32, out var encoding);
        Assert.Equal(MultibaseEncoding.Base32Lower, encoding);
        Assert.Equal(KnownCidBytesHex, Convert.ToHexString(canonical).ToLowerInvariant());

        Assert.Throws<CidFormatException>(() => Multibase.Decode(KnownCidBase32 + "a"));
    }

    [Theory]
    [InlineData("ba")]        // payload len 1  (≡ 1 mod 8) — single dangling zero symbol
    [InlineData("baaa")]      // payload len 3  (≡ 3 mod 8)
    [InlineData("baaaaaa")]   // payload len 6  (≡ 6 mod 8)
    public void Decode_ThrowsOnBase32IncompleteTrailingSymbol(string nonCanonical)
    {
        // Payload lengths ≡ {1,3,6} mod 8 carry an incomplete trailing base32 symbol (≥5 unused bits)
        // that cannot occur in a canonical no-padding encoding. Even when those bits are zero (so the
        // non-zero-trailing-bits check passes), the dangling symbol must be rejected to prevent
        // malleability. See issue #42.
        Assert.Throws<CidFormatException>(() => Multibase.Decode(nonCanonical));
    }

    [Theory]
    [InlineData("baa", new byte[] { 0x00 })]                          // payload len 2 (≡ 2 mod 8)
    [InlineData("baaaa", new byte[] { 0x00, 0x00 })]                  // payload len 4 (≡ 4 mod 8)
    [InlineData("baaaaa", new byte[] { 0x00, 0x00, 0x00 })]           // payload len 5 (≡ 5 mod 8)
    [InlineData("baaaaaaa", new byte[] { 0x00, 0x00, 0x00, 0x00 })]   // payload len 7 (≡ 7 mod 8)
    public void Decode_AcceptsCanonicalBase32PartialGroupLengths(string canonical, byte[] expected)
    {
        // The legitimate partial-group lengths ({2,4,5,7} mod 8) leave <5 unused (zero) bits and must
        // still decode — the incomplete-trailing-symbol guard added for #42 must not over-reject them.
        var decoded = Multibase.Decode(canonical, out var encoding);

        Assert.Equal(MultibaseEncoding.Base32Lower, encoding);
        Assert.Equal(expected, decoded);
    }
}
