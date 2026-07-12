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
    /// <remarks>
    /// Not to be confused with <see cref="Multicodec.Prefix"/>, which produces
    /// varint(codec) || data with no digest-length varint and is not a multihash.
    /// For a SHA-256 digest a multihash is 34 bytes (<c>0x12 0x20 || digest</c>);
    /// the multicodec-tagged form is 33 bytes.
    /// The digest length is encoded from the actual span, never validated against
    /// <paramref name="hashFunctionCode"/> — supply a digest of the correct length
    /// for the declared hash function.
    /// </remarks>
    public static byte[] Encode(ulong hashFunctionCode, ReadOnlySpan<byte> digest)
        => new MultihashDigest(hashFunctionCode, digest).ToByteArray();

    /// <summary>
    /// Encode a digest as a complete multihash (varint(hashFunctionCode) || varint(digestLength) || digest)
    /// and render it as base58btc text in one call.
    /// </summary>
    /// <param name="hashFunctionCode">The multihash function code, e.g. <see cref="MultihashCode.Sha2_256"/>.</param>
    /// <param name="digest">
    /// The raw digest bytes. The length is encoded from the actual span, never validated
    /// against <paramref name="hashFunctionCode"/> — supply a digest of the correct length
    /// for the declared hash function.
    /// </param>
    /// <param name="includeMultibasePrefix">
    /// <see langword="true"/> to return a self-describing multibase string with a leading <c>'z'</c>
    /// (use when the governing protocol says <c>multibase(...)</c>);
    /// <see langword="false"/> to return bare base58btc with no <c>'z'</c>
    /// (use when the protocol says <c>base58btc(...)</c> — for a SHA-256 multihash the result
    /// commonly begins with <c>Qm</c>). The parameter has no default on purpose: whether the
    /// prefix belongs depends on the protocol, so state the intent at every call site.
    /// </param>
    /// <returns>The base58btc text of the complete multihash.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="hashFunctionCode"/> exceeds the varint range.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Equivalent to <c>Multibase.EncodeBase58Btc(Multihash.Encode(hashFunctionCode, digest), includeMultibasePrefix)</c>.
    /// Do not substitute <see cref="Multicodec.Prefix"/> for the multihash step: it omits the
    /// digest-length varint and does not produce a multihash.
    /// </para>
    /// <para>
    /// For multicodec-tagged public keys (<c>did:key</c>, <c>publicKeyMultibase</c>) use
    /// <see cref="Multikey"/> instead — there the correct shape is a multicodec prefix plus
    /// <c>'z'</c> multibase, not a multihash.
    /// </para>
    /// <para>
    /// To decode, compose the existing primitives: <see cref="Multibase.DecodeBase58Btc(string)"/>
    /// (bare) or <see cref="Multibase.Decode(string)"/> (multibase) followed by
    /// <see cref="Decode"/>. No combined decoder is provided because a bare base58btc string can
    /// itself begin with <c>'z'</c>, making a decoder that accepts either form ambiguous.
    /// </para>
    /// </remarks>
    public static string EncodeBase58Btc(ulong hashFunctionCode, ReadOnlySpan<byte> digest, bool includeMultibasePrefix)
        => Multibase.EncodeBase58Btc(Encode(hashFunctionCode, digest), includeMultibasePrefix);

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
