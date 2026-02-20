using System.Text;

namespace NetCid.Tests;

public sealed class MultihashDigestTests
{
    [Fact]
    public void Sha2_256_ProducesExpectedCodeAndLength()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        var digest = MultihashDigest.Sha2_256(bytes);

        Assert.Equal(MultihashCode.Sha2_256, digest.Code);
        Assert.Equal(32, digest.DigestLength);
    }

    [Fact]
    public void ToByteArray_Parse_RoundTrips()
    {
        var bytes = Encoding.UTF8.GetBytes("net-cid");
        var digest = MultihashDigest.Sha2_256(bytes);
        var encoded = digest.ToByteArray();
        var parsed = MultihashDigest.Parse(encoded, out var bytesRead);

        Assert.Equal(encoded.Length, bytesRead);
        Assert.Equal(digest, parsed);
    }

    [Fact]
    public void TryParse_ReturnsFalseWhenDigestBytesAreMissing()
    {
        var invalid = new byte[] { 0x12, 0x20, 0x01, 0x02 };
        var ok = MultihashDigest.TryParse(invalid, out _, out _);

        Assert.False(ok);
    }
}
