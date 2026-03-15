namespace NetCid;

/// <summary>
/// Static helpers for multihash encoding and decoding.
/// A multihash is: varint(hashFunctionCode) || varint(digestLength) || digest.
/// See https://multiformats.io/multihash/
/// </summary>
public static class Multihash
{
    /// <summary>
    /// Encode a digest as a multihash: varint(hashFunctionCode) || varint(digestLength) || digest.
    /// </summary>
    public static byte[] Encode(ulong hashFunctionCode, ReadOnlySpan<byte> digest)
        => new MultihashDigest(hashFunctionCode, digest).ToByteArray();

    /// <summary>
    /// Decode a multihash byte sequence into its hash function code and raw digest.
    /// </summary>
    public static (ulong Code, byte[] Digest) Decode(ReadOnlySpan<byte> multihash)
    {
        var parsed = MultihashDigest.Parse(multihash, out _);
        return (parsed.Code, parsed.GetDigestBytes());
    }

    /// <summary>
    /// Try to decode a multihash byte sequence.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> multihash, out ulong code, out byte[]? digest)
    {
        if (MultihashDigest.TryParse(multihash, out var parsed, out _))
        {
            code = parsed.Code;
            digest = parsed.GetDigestBytes();
            return true;
        }

        code = 0;
        digest = null;
        return false;
    }
}
