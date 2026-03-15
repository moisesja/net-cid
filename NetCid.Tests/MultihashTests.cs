using System.Security.Cryptography;

namespace NetCid.Tests;

public sealed class MultihashTests
{
    [Fact]
    public void Encode_ProducesCorrectMultihashFormat()
    {
        var data = "hello world"u8;
        var digest = SHA256.HashData(data);
        var multihash = Multihash.Encode(MultihashCode.Sha2_256, digest);

        // varint(0x12) = [0x12], varint(32) = [0x20], digest = 32 bytes → total 34 bytes
        Assert.Equal(34, multihash.Length);
        Assert.Equal(0x12, multihash[0]);
        Assert.Equal(0x20, multihash[1]);
        Assert.Equal(digest, multihash[2..]);
    }

    [Fact]
    public void Encode_DiffersFromMulticodecPrefix()
    {
        var digest = SHA256.HashData("test"u8);

        var multihash = Multihash.Encode(MultihashCode.Sha2_256, digest);
        var multicodecPrefixed = Multicodec.Prefix(MultihashCode.Sha2_256, digest);

        // Multihash has an extra varint for digest length
        Assert.Equal(34, multihash.Length);   // varint(code) + varint(length) + digest
        Assert.Equal(33, multicodecPrefixed.Length); // varint(code) + digest (no length!)
        Assert.NotEqual(multihash, multicodecPrefixed);
    }

    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var digest = SHA256.HashData("net-cid"u8);
        var multihash = Multihash.Encode(MultihashCode.Sha2_256, digest);

        var (code, decoded) = Multihash.Decode(multihash);

        Assert.Equal(MultihashCode.Sha2_256, code);
        Assert.Equal(digest, decoded);
    }

    [Fact]
    public void TryDecode_ReturnsTrueForValidMultihash()
    {
        var digest = SHA256.HashData("test"u8);
        var multihash = Multihash.Encode(MultihashCode.Sha2_256, digest);

        Assert.True(Multihash.TryDecode(multihash, out var code, out var decoded));
        Assert.Equal(MultihashCode.Sha2_256, code);
        Assert.Equal(digest, decoded);
    }

    [Fact]
    public void TryDecode_ReturnsFalseForTruncatedInput()
    {
        // Valid header but truncated digest
        var invalid = new byte[] { 0x12, 0x20, 0x01, 0x02 };

        Assert.False(Multihash.TryDecode(invalid, out _, out _));
    }

    [Fact]
    public void Decode_ThrowsForInvalidInput()
    {
        Assert.Throws<CidFormatException>(() => Multihash.Decode(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Encode_MatchesMultihashDigestToByteArray()
    {
        var digest = SHA256.HashData("consistency"u8);

        var viaHelper = Multihash.Encode(MultihashCode.Sha2_256, digest);
        var viaStruct = new MultihashDigest(MultihashCode.Sha2_256, digest).ToByteArray();

        Assert.Equal(viaStruct, viaHelper);
    }

    [Fact]
    public void Encode_Sha512_ProducesCorrectLength()
    {
        var digest = SHA512.HashData("test"u8);
        var multihash = Multihash.Encode(MultihashCode.Sha2_512, digest);

        // varint(0x13) = [0x13], varint(64) = [0x40], digest = 64 bytes → total 66 bytes
        Assert.Equal(66, multihash.Length);
        Assert.Equal(0x13, multihash[0]);
        Assert.Equal(0x40, multihash[1]);
    }
}
