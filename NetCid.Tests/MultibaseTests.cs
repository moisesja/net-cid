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
    public void EncodeDecode_Base36RoundTrip()
    {
        var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 255 };
        var encoded = Multibase.Encode(bytes, MultibaseEncoding.Base36Lower, includePrefix: true);
        var decoded = Multibase.Decode(encoded, out var encoding);

        Assert.Equal(MultibaseEncoding.Base36Lower, encoding);
        Assert.Equal(bytes, decoded);
    }
}
