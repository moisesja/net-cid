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
}
