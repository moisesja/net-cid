namespace NetCid.Tests;

public sealed class MulticodecTests
{
    [Theory]
    [InlineData(Multicodec.Secp256k1Pub, "secp256k1-pub")]
    [InlineData(Multicodec.Bls12381G1Pub, "bls12_381-g1-pub")]
    [InlineData(Multicodec.Bls12381G2Pub, "bls12_381-g2-pub")]
    [InlineData(Multicodec.X25519Pub, "x25519-pub")]
    [InlineData(Multicodec.Ed25519Pub, "ed25519-pub")]
    [InlineData(Multicodec.P256Pub, "p256-pub")]
    [InlineData(Multicodec.P384Pub, "p384-pub")]
    public void TryGetName_ReturnsNameForKeyType(ulong code, string expectedName)
    {
        Assert.True(Multicodec.TryGetName(code, out var name));
        Assert.Equal(expectedName, name);
    }

    [Theory]
    [InlineData("secp256k1-pub", Multicodec.Secp256k1Pub)]
    [InlineData("bls12_381-g1-pub", Multicodec.Bls12381G1Pub)]
    [InlineData("bls12_381-g2-pub", Multicodec.Bls12381G2Pub)]
    [InlineData("x25519-pub", Multicodec.X25519Pub)]
    [InlineData("ed25519-pub", Multicodec.Ed25519Pub)]
    [InlineData("p256-pub", Multicodec.P256Pub)]
    [InlineData("p384-pub", Multicodec.P384Pub)]
    public void TryGetCode_ReturnsCodeForKeyType(string name, ulong expectedCode)
    {
        Assert.True(Multicodec.TryGetCode(name, out var code));
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void Prefix_PrependsVarintForEd25519()
    {
        var rawKey = new byte[] { 0x01, 0x02, 0x03 };
        var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, rawKey);

        // 0xED requires 2 varint bytes: 0xED & 0x7F | 0x80 = 0xED, 0xED >> 7 = 0x01
        // Actually: 0xED = 237. varint: 237 & 0x7F = 0x6D | 0x80 = 0xED, 237 >> 7 = 1 => 0x01
        Assert.Equal(new byte[] { 0xED, 0x01, 0x01, 0x02, 0x03 }, prefixed);
    }

    [Fact]
    public void Prefix_PrependsMultiByteVarintForP256()
    {
        var rawKey = new byte[] { 0xAA, 0xBB };
        var prefixed = Multicodec.Prefix(Multicodec.P256Pub, rawKey);

        // 0x1200 = 4608. varint: 4608 & 0x7F = 0x00 | 0x80 = 0x80, 4608 >> 7 = 36 = 0x24
        Assert.Equal(new byte[] { 0x80, 0x24, 0xAA, 0xBB }, prefixed);
    }

    [Theory]
    [InlineData(Multicodec.Secp256k1Pub)]
    [InlineData(Multicodec.Bls12381G1Pub)]
    [InlineData(Multicodec.Bls12381G2Pub)]
    [InlineData(Multicodec.X25519Pub)]
    [InlineData(Multicodec.Ed25519Pub)]
    [InlineData(Multicodec.P256Pub)]
    [InlineData(Multicodec.P384Pub)]
    public void PrefixDecode_RoundTrip(ulong codec)
    {
        var rawBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var prefixed = Multicodec.Prefix(codec, rawBytes);
        var (decodedCodec, decodedBytes) = Multicodec.Decode(prefixed);

        Assert.Equal(codec, decodedCodec);
        Assert.Equal(rawBytes, decodedBytes);
    }

    [Fact]
    public void TryDecode_ReturnsFalseOnEmptyInput()
    {
        Assert.False(Multicodec.TryDecode(ReadOnlySpan<byte>.Empty, out _, out _));
    }

    [Fact]
    public void TryDecode_ReturnsFalseOnTruncatedVarint()
    {
        // A continuation byte (0x80) with no following byte
        Assert.False(Multicodec.TryDecode(new byte[] { 0x80 }, out _, out _));
    }

    [Fact]
    public void Decode_ThrowsOnEmptyInput()
    {
        Assert.Throws<CidFormatException>(() => Multicodec.Decode(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Prefix_EmptyRawBytes()
    {
        var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, ReadOnlySpan<byte>.Empty);

        // Should just be the varint prefix with no trailing data
        var (codec, rawBytes) = Multicodec.Decode(prefixed);
        Assert.Equal(Multicodec.Ed25519Pub, codec);
        Assert.Empty(rawBytes);
    }
}
