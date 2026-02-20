using System.Text;

namespace NetCid.Tests;

public sealed class CidTests
{
    private const string KnownV0 = "QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n";
    private const string KnownV1Base58 = "zb2rhe5P4gXftAwvA4eXQ5HJwsER2owDyS9sKaQRRVQPn93bA";
    private const string KnownV1Base32 = "bafkreidon73zkcrwdb5iafqtijxildoonbwnpv7dyd6ef3qdgads2jc4su";

    [Fact]
    public void Parse_RecognizesCidV0()
    {
        var cid = Cid.Parse(KnownV0);

        Assert.Equal(CidVersion.V0, cid.Version);
        Assert.Equal(Multicodec.DagPb, cid.Codec);
        Assert.Equal(KnownV0, cid.ToString());
    }

    [Fact]
    public void Parse_RecognizesCidV1Base32()
    {
        var cid = Cid.Parse(KnownV1Base32);

        Assert.Equal(CidVersion.V1, cid.Version);
        Assert.Equal(Multicodec.Raw, cid.Codec);
        Assert.Equal(KnownV1Base32, cid.ToString());
    }

    [Fact]
    public void Parse_RecognizesCidV1Base58()
    {
        var cid = Cid.Parse(KnownV1Base58);

        Assert.Equal(CidVersion.V1, cid.Version);
        Assert.Equal(KnownV1Base58, cid.ToString(MultibaseEncoding.Base58Btc));
    }

    [Fact]
    public void Parse_RejectsMultibasePrefixedCidV0()
    {
        Assert.Throws<CidFormatException>(() => Cid.Parse($"z{KnownV0}"));
    }

    [Fact]
    public void Decode_RejectsReservedVersion()
    {
        var bytes = new byte[] { 0x02, 0x70, 0x12, 0x20 };
        var exception = Assert.Throws<CidFormatException>(() => Cid.Decode(bytes));

        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateV0_RejectsNonSha256Digest()
    {
        var digest = new MultihashDigest(MultihashCode.Sha2_512, new byte[64]);
        Assert.Throws<InvalidOperationException>(() => Cid.CreateV0(digest));
    }

    [Fact]
    public void ToV0_RejectsUnsupportedCodec()
    {
        var cid = Cid.FromContent(Encoding.UTF8.GetBytes("hello world"), codec: Multicodec.Raw);
        Assert.Throws<InvalidOperationException>(() => cid.ToV0());
    }

    [Fact]
    public void ToString_UsesVersionDefaults()
    {
        var v0 = Cid.Parse(KnownV0);
        var v1 = Cid.Parse(KnownV1Base32);

        Assert.Equal(KnownV0, v0.ToString());
        Assert.Equal(KnownV1Base32, v1.ToString());
    }

    [Fact]
    public void TryParse_ReturnsFalseForInvalidValue()
    {
        var ok = Cid.TryParse("not-a-cid", out var cid);

        Assert.False(ok);
        Assert.Null(cid);
    }

    [Fact]
    public void Parse_RejectsOversizedInputString()
    {
        var oversized = "b" + new string('a', Cid.DefaultMaxInputStringLength);

        var exception = Assert.Throws<CidFormatException>(() => Cid.Parse(oversized));
        Assert.Contains("exceeds the allowed limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_RejectsOversizedInputBytes()
    {
        var oversized = new byte[Cid.DefaultMaxInputByteLength + 1];

        var exception = Assert.Throws<CidFormatException>(() => Cid.Decode(oversized));
        Assert.Contains("exceeds the allowed limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_ReturnsFalseWhenLimitIsTooSmall()
    {
        var ok = Cid.TryParse(KnownV1Base32, out var cid, maxInputStringLength: 8, maxInputByteLength: Cid.DefaultMaxInputByteLength);

        Assert.False(ok);
        Assert.Null(cid);
    }
}
