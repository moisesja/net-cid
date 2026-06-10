namespace NetCid;

/// <summary>
/// Helpers for the W3C Controlled Identifiers 1.0 Multikey verification method
/// and the equivalent <c>did:key</c> identifier encoding:
/// <c>publicKeyMultibase = base58btc( varint(keyCodec) || rawPublicKey )</c>.
/// </summary>
public static class Multikey
{
    /// <summary>
    /// Encode a raw public key of the given key-type multicodec as a Multikey
    /// <c>publicKeyMultibase</c> string (base58btc, leading <c>'z'</c>).
    /// </summary>
    /// <param name="keyCodec">One of the eight supported key-type multicodecs.</param>
    /// <param name="rawKey">Raw public-key bytes. Edwards/Montgomery curves use their raw
    /// 32-byte encoding; NIST P-curves and secp256k1 use compressed-point form; BLS keys
    /// use their canonical 48-/96-byte serialization.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="keyCodec"/> is not a supported key-type multicodec,
    /// <paramref name="rawKey"/> has the wrong length for that codec, or — for the
    /// secp256k1/P-256/P-384/P-521 codecs — <paramref name="rawKey"/> is not a SEC1
    /// compressed point (leading byte 0x02/0x03).
    /// </exception>
    public static string Encode(ulong keyCodec, ReadOnlySpan<byte> rawKey)
    {
        EnsureKnownKeyType(keyCodec, nameof(keyCodec));
        EnsureKeyLength(keyCodec, rawKey.Length, nameof(rawKey));
        EnsureEcPointShape(keyCodec, rawKey, nameof(rawKey));
        var prefixed = Multicodec.Prefix(keyCodec, rawKey);
        return Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);
    }

    /// <summary>
    /// Decode a Multikey <c>publicKeyMultibase</c> string into its key-type multicodec
    /// and raw public key. Validates the multibase prefix, multicodec category, and
    /// per-codec key length.
    /// </summary>
    /// <exception cref="CidFormatException">The input is not a valid Multikey value.</exception>
    public static (ulong KeyCodec, byte[] RawKey) Decode(string publicKeyMultibase)
    {
        if (!TryDecode(publicKeyMultibase, out var codec, out var raw))
        {
            throw new CidFormatException("Invalid publicKeyMultibase value.");
        }

        return (codec, raw!);
    }

    /// <summary>
    /// Try to decode a Multikey <c>publicKeyMultibase</c> string. Returns <see langword="false"/>
    /// for any non-base58btc multibase, any non-key-type multicodec, any payload whose raw
    /// length does not match the codec, or — for the secp256k1/P-256/P-384/P-521 codecs — a raw
    /// key that is not a SEC1 compressed point (leading byte 0x02/0x03).
    /// </summary>
    public static bool TryDecode(string publicKeyMultibase, out ulong keyCodec, out byte[]? rawKey)
    {
        keyCodec = 0;
        rawKey = null;

        if (!Multibase.TryDecode(publicKeyMultibase, out var bytes, out var encoding))
        {
            return false;
        }

        if (encoding != MultibaseEncoding.Base58Btc)
        {
            return false;
        }

        if (!Multicodec.TryDecode(bytes, out var codec, out var raw))
        {
            return false;
        }

        if (!IsKnownKeyType(codec))
        {
            return false;
        }

        if (raw is null || !KeyLengthMatches(codec, raw.Length))
        {
            return false;
        }

        if (!EcPointShapeValid(codec, raw))
        {
            return false;
        }

        keyCodec = codec;
        rawKey = raw;
        return true;
    }

    /// <summary>Expected raw-key byte length for a supported Multikey codec, or -1 if unknown.</summary>
    private static int ExpectedLength(ulong codec) => codec switch
    {
        Multicodec.Ed25519Pub => 32,
        Multicodec.X25519Pub => 32,
        Multicodec.Secp256k1Pub => 33,
        Multicodec.P256Pub => 33,
        Multicodec.P384Pub => 49,
        Multicodec.P521Pub => 67,
        Multicodec.Bls12381G1Pub => 48,
        Multicodec.Bls12381G2Pub => 96,
        _ => -1,
    };

    private static bool IsKnownKeyType(ulong codec) => ExpectedLength(codec) >= 0;

    private static bool KeyLengthMatches(ulong codec, int length) => ExpectedLength(codec) == length;

    /// <summary>
    /// The four Weierstrass-curve codecs whose raw key is a SEC1 point. The W3C Controlled
    /// Identifiers / vc-di-ecdsa specs mandate the compressed form, whose first byte is the
    /// sign prefix 0x02 or 0x03; any other prefix (e.g. 0x04 uncompressed) MUST be rejected.
    /// Edwards/Montgomery (ed25519/x25519) and BLS keys have no SEC1 prefix.
    /// </summary>
    private static bool IsSec1PointCodec(ulong codec) => codec is
        Multicodec.Secp256k1Pub or Multicodec.P256Pub or Multicodec.P384Pub or Multicodec.P521Pub;

    private static bool EcPointShapeValid(ulong codec, ReadOnlySpan<byte> rawKey)
    {
        if (!IsSec1PointCodec(codec))
        {
            return true;
        }

        if (rawKey[0] is not (0x02 or 0x03))
        {
            return false;
        }

        // P-521's x-coordinate occupies 66 bytes but the field prime is 2^521 - 1, so the
        // big-endian top byte can only be 0x00 or 0x01; anything larger is not a field element.
        if (codec == Multicodec.P521Pub && rawKey[1] > 0x01)
        {
            return false;
        }

        return true;
    }

    private static void EnsureEcPointShape(ulong codec, ReadOnlySpan<byte> rawKey, string paramName)
    {
        if (EcPointShapeValid(codec, rawKey))
        {
            return;
        }

        Multicodec.TryGetName(codec, out var name);
        var reason = rawKey[0] is not (0x02 or 0x03)
            ? $"leading byte must be the SEC1 compressed-point prefix 0x02 or 0x03 (got 0x{rawKey[0]:X2}); " +
              "uncompressed or invalid point encodings are not allowed for Multikey/did:key"
            : $"x-coordinate top byte 0x{rawKey[1]:X2} exceeds the P-521 field (must be 0x00 or 0x01)";
        throw new ArgumentException(
            $"Raw key for {name ?? $"0x{codec:X}"} is not a valid SEC1 compressed point: {reason}.",
            paramName);
    }

    private static void EnsureKnownKeyType(ulong codec, string paramName)
    {
        if (IsKnownKeyType(codec))
        {
            return;
        }

        var name = Multicodec.TryGetName(codec, out var n) ? $"{n} (0x{codec:X})" : $"0x{codec:X}";
        throw new ArgumentException(
            $"Multicodec {name} is not a supported Multikey key type. " +
            "Supported codecs: ed25519-pub, x25519-pub, secp256k1-pub, p256-pub, p384-pub, p521-pub, bls12_381-g1-pub, bls12_381-g2-pub.",
            paramName);
    }

    private static void EnsureKeyLength(ulong codec, int actual, string paramName)
    {
        var expected = ExpectedLength(codec);
        if (expected == actual)
        {
            return;
        }

        Multicodec.TryGetName(codec, out var name);
        throw new ArgumentException(
            $"Raw key length {actual} does not match the expected {expected} bytes for {name ?? $"0x{codec:X}"}.",
            paramName);
    }
}
