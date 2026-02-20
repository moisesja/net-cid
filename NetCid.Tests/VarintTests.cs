namespace NetCid.Tests;

public sealed class VarintTests
{
    [Fact]
    public void EncodeDecode_RoundTripsZero()
    {
        var encoded = Varint.Encode(0);
        var decoded = Varint.Decode(encoded, out var bytesRead);

        Assert.Equal(new byte[] { 0x00 }, encoded);
        Assert.Equal<ulong>(0, decoded);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void EncodeDecode_RoundTripsMaxValue()
    {
        var encoded = Varint.Encode(Varint.MaxValue);
        var decoded = Varint.Decode(encoded, out var bytesRead);

        Assert.Equal(9, encoded.Length);
        Assert.Equal(Varint.MaxValue, decoded);
        Assert.Equal(encoded.Length, bytesRead);
    }

    [Fact]
    public void TryDecode_ReturnsFalseForNonCanonicalEncoding()
    {
        var ok = Varint.TryDecode(new byte[] { 0x81, 0x00 }, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_ReturnsFalseForTenByteVarint()
    {
        var invalid = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 };
        var ok = Varint.TryDecode(invalid, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Encode_ThrowsForOutOfRangeValue()
    {
        var outOfRange = Varint.MaxValue + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => Varint.Encode(outOfRange));
    }

    [Fact]
    public void Write_ThrowsWhenDestinationTooSmall()
    {
        var destination = new byte[0];
        Assert.Throws<ArgumentException>(() => Varint.Write(1, destination));
    }
}
