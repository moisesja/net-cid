using System.Text;

namespace NetCid.IntegrationTests;

public sealed class CidIntegrationTests
{
    private const string KnownCidBytesHex = "015512206e6ff7950a36187a801613426e858dce686cd7d7e3c0fc42ee0330072d245c95";
    private const string KnownV1Base58 = "zb2rhe5P4gXftAwvA4eXQ5HJwsER2owDyS9sKaQRRVQPn93bA";
    private const string KnownV1Base32 = "bafkreidon73zkcrwdb5iafqtijxildoonbwnpv7dyd6ef3qdgads2jc4su";

    [Fact]
    public void KnownVectors_AreInteroperableAcrossBases()
    {
        var cidFromBase58 = Cid.Parse(KnownV1Base58);
        var cidFromBase32 = Cid.Parse(KnownV1Base32);
        var expectedBytes = Convert.FromHexString(KnownCidBytesHex);

        Assert.Equal(CidVersion.V1, cidFromBase58.Version);
        Assert.Equal(Multicodec.Raw, cidFromBase58.Codec);
        Assert.Equal(expectedBytes, cidFromBase58.ToByteArray());
        Assert.Equal(KnownV1Base32, cidFromBase58.ToString(MultibaseEncoding.Base32Lower));
        Assert.Equal(KnownV1Base58, cidFromBase32.ToString(MultibaseEncoding.Base58Btc));
        Assert.Equal(cidFromBase58, cidFromBase32);
    }

    [Fact]
    public void BuildParseRoundTrip_FromContent()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        var cid = Cid.FromContent(bytes, codec: Multicodec.Raw, hashCode: MultihashCode.Sha2_256);

        var text = cid.ToString();
        var parsed = Cid.Parse(text);

        Assert.Equal(CidVersion.V1, cid.Version);
        Assert.Equal(Multicodec.Raw, cid.Codec);
        Assert.Equal(cid, parsed);
    }

    [Fact]
    public void CidV0V1_ConversionsRoundTrip()
    {
        var multihash = MultihashDigest.Sha2_256(Encoding.UTF8.GetBytes("dag-pb payload"));
        var v0 = Cid.CreateV0(multihash);
        var v1 = v0.ToV1();
        var roundTrippedV0 = v1.ToV0();

        Assert.Equal(CidVersion.V1, v1.Version);
        Assert.Equal(Multicodec.DagPb, v1.Codec);
        Assert.Equal(v0, roundTrippedV0);
    }
}
