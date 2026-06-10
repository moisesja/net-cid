namespace NetCid.Tests;

public sealed class MultikeyTests
{
    public static TheoryData<ulong, int> KeyCodecsAndLengths => new()
    {
        { Multicodec.Ed25519Pub, 32 },
        { Multicodec.X25519Pub, 32 },
        { Multicodec.Secp256k1Pub, 33 },
        { Multicodec.P256Pub, 33 },
        { Multicodec.P384Pub, 49 },
        { Multicodec.P521Pub, 67 },
        { Multicodec.Bls12381G1Pub, 48 },
        { Multicodec.Bls12381G2Pub, 96 },
    };

    [Theory]
    [MemberData(nameof(KeyCodecsAndLengths))]
    public void Encode_Decode_RoundTrips_ForEveryKeyCodec(ulong codec, int expectedLength)
    {
        var rawKey = DeterministicKey(codec, expectedLength);

        var multibase = Multikey.Encode(codec, rawKey);

        Assert.StartsWith("z", multibase, StringComparison.Ordinal);

        var (decodedCodec, decodedKey) = Multikey.Decode(multibase);
        Assert.Equal(codec, decodedCodec);
        Assert.Equal(rawKey, decodedKey);
    }

    [Theory]
    [MemberData(nameof(KeyCodecsAndLengths))]
    public void TryDecode_Returns_True_ForRoundTrippedEncoding(ulong codec, int expectedLength)
    {
        var rawKey = DeterministicKey(codec, expectedLength);
        var multibase = Multikey.Encode(codec, rawKey);

        Assert.True(Multikey.TryDecode(multibase, out var decodedCodec, out var decodedKey));
        Assert.Equal(codec, decodedCodec);
        Assert.Equal(rawKey, decodedKey);
    }

    [Fact]
    public void Encode_Throws_OnUnknownCodec()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Multikey.Encode(Multicodec.Raw, new byte[32]));

        Assert.Equal("keyCodec", ex.ParamName);
    }

    [Fact]
    public void Encode_Throws_OnContentCodecDagPb()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Multikey.Encode(Multicodec.DagPb, new byte[32]));

        Assert.Equal("keyCodec", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(KeyCodecsAndLengths))]
    public void Encode_Throws_OnWrongKeyLength(ulong codec, int expectedLength)
    {
        var wrong = new byte[expectedLength + 1];

        var ex = Assert.Throws<ArgumentException>(
            () => Multikey.Encode(codec, wrong));

        Assert.Equal("rawKey", ex.ParamName);
    }

    [Fact]
    public void TryDecode_Returns_False_OnWrongLengthPayload()
    {
        // Build base58btc(varint(Ed25519Pub) || 16-byte payload) — codec is valid, length is not.
        var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, new byte[16]);
        var multibase = Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);

        Assert.False(Multikey.TryDecode(multibase, out var codec, out var raw));
        Assert.Equal(0UL, codec);
        Assert.Null(raw);
    }

    [Fact]
    public void TryDecode_Returns_False_OnNonBase58Multibase()
    {
        // Same prefixed bytes, but encoded as base32 ('b' prefix) instead of base58btc.
        var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, DeterministicKey(32));
        var base32 = Multibase.Encode(prefixed, MultibaseEncoding.Base32Lower, includePrefix: true);

        Assert.False(Multikey.TryDecode(base32, out _, out _));
    }

    [Fact]
    public void TryDecode_Returns_False_OnBase64UrlMultibase()
    {
        // Multikey is base58btc only — base64url ('u') must be rejected even with a valid key payload.
        var prefixed = Multicodec.Prefix(Multicodec.Ed25519Pub, DeterministicKey(32));
        var base64url = Multibase.Encode(prefixed, MultibaseEncoding.Base64Url, includePrefix: true);

        Assert.False(Multikey.TryDecode(base64url, out _, out _));
    }

    [Fact]
    public void TryDecode_Returns_False_OnContentCodecPrefix()
    {
        // Multicodec.Raw (0x55) is a content codec, not a key type. Reject even with a 32-byte payload.
        var prefixed = Multicodec.Prefix(Multicodec.Raw, DeterministicKey(32));
        var multibase = Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);

        Assert.False(Multikey.TryDecode(multibase, out _, out _));
    }

    [Fact]
    public void TryDecode_Returns_False_OnEmptyOrNullInput()
    {
        Assert.False(Multikey.TryDecode(string.Empty, out _, out _));
        Assert.False(Multikey.TryDecode(null!, out _, out _));
    }

    [Fact]
    public void Decode_Throws_CidFormatException_OnInvalidInput()
    {
        Assert.Throws<CidFormatException>(() => Multikey.Decode("not-a-multikey"));
    }

    [Fact]
    public void Encode_ProducesMultikeySignaturePrefix_ForEd25519()
    {
        // Every 32-byte Ed25519 publicKeyMultibase begins with "z6Mk" because the
        // varint multicodec prefix is [0xED, 0x01] for ed25519-pub (0xED).
        var rawKey = DeterministicKey(32);
        var multibase = Multikey.Encode(Multicodec.Ed25519Pub, rawKey);

        Assert.StartsWith("z6Mk", multibase, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_DeterministicEd25519Key_RoundTrips()
    {
        // Deterministic 32-byte raw key (bytes 0..31) encoded with the new API,
        // decoded with TryDecode, then asserted byte-equal. Catches any prefix-handling regression.
        var rawKey = new byte[32];
        for (var i = 0; i < rawKey.Length; i++)
        {
            rawKey[i] = (byte)i;
        }

        var multibase = Multikey.Encode(Multicodec.Ed25519Pub, rawKey);

        Assert.True(Multikey.TryDecode(multibase, out var codec, out var decoded));
        Assert.Equal(Multicodec.Ed25519Pub, codec);
        Assert.NotNull(decoded);
        Assert.Equal(32, decoded!.Length);
        Assert.Equal(rawKey, decoded);
    }

    public static TheoryData<ulong, int> Sec1KeyCodecsAndLengths => new()
    {
        { Multicodec.Secp256k1Pub, 33 },
        { Multicodec.P256Pub, 33 },
        { Multicodec.P384Pub, 49 },
        { Multicodec.P521Pub, 67 },
    };

    public static TheoryData<ulong, int, byte> Sec1CodecsWithInvalidPrefixes()
    {
        var data = new TheoryData<ulong, int, byte>();
        foreach (var prefix in new byte[] { 0x00, 0x01, 0x04, 0x05, 0xFF })
        {
            data.Add(Multicodec.Secp256k1Pub, 33, prefix);
            data.Add(Multicodec.P256Pub, 33, prefix);
            data.Add(Multicodec.P384Pub, 49, prefix);
            data.Add(Multicodec.P521Pub, 67, prefix);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Sec1CodecsWithInvalidPrefixes))]
    public void Encode_Throws_OnInvalidSec1PointPrefix(ulong codec, int length, byte prefix)
    {
        // W3C Controlled Identifiers / vc-di-ecdsa mandate the SEC1 *compressed* point form
        // (leading byte 0x02/0x03) for the Weierstrass-curve codecs; 0x04 (uncompressed) and
        // every other prefix must be rejected, not silently minted into a did:key. See issue #45.
        var rawKey = DeterministicKey(codec, length);
        rawKey[0] = prefix;

        var ex = Assert.Throws<ArgumentException>(() => Multikey.Encode(codec, rawKey));

        Assert.Equal("rawKey", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(Sec1CodecsWithInvalidPrefixes))]
    public void TryDecode_Returns_False_OnInvalidSec1PointPrefix(ulong codec, int length, byte prefix)
    {
        // Build the multibase value manually (bypassing Encode's own validation) so the
        // consumer path is exercised independently of the producer path. See issue #45.
        var rawKey = DeterministicKey(codec, length);
        rawKey[0] = prefix;
        var prefixed = Multicodec.Prefix(codec, rawKey);
        var multibase = Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);

        Assert.False(Multikey.TryDecode(multibase, out var decodedCodec, out var decodedKey));
        Assert.Equal(0UL, decodedCodec);
        Assert.Null(decodedKey);
        Assert.Throws<CidFormatException>(() => Multikey.Decode(multibase));
    }

    [Theory]
    [MemberData(nameof(Sec1KeyCodecsAndLengths))]
    public void EncodeDecode_RoundTrips_BothCompressedPrefixes(ulong codec, int length)
    {
        foreach (var prefix in new byte[] { 0x02, 0x03 })
        {
            var rawKey = DeterministicKey(codec, length);
            rawKey[0] = prefix;

            var multibase = Multikey.Encode(codec, rawKey);

            Assert.True(Multikey.TryDecode(multibase, out var decodedCodec, out var decodedKey));
            Assert.Equal(codec, decodedCodec);
            Assert.Equal(rawKey, decodedKey);
        }
    }

    [Theory]
    [InlineData((byte)0x00)]
    [InlineData((byte)0x01)]
    public void P521_AcceptsFieldRangeTopByte(byte topByte)
    {
        var rawKey = DeterministicKey(Multicodec.P521Pub, 67);
        rawKey[1] = topByte;

        var multibase = Multikey.Encode(Multicodec.P521Pub, rawKey);

        Assert.True(Multikey.TryDecode(multibase, out _, out var decodedKey));
        Assert.Equal(rawKey, decodedKey);
    }

    [Fact]
    public void P521_RejectsCoordinateAboveFieldRange()
    {
        // P-521's 66-byte big-endian x-coordinate is bounded by 2^521 - 1, so its top byte is
        // 0x00 or 0x01; a larger top byte cannot be a field element. See issue #45.
        var rawKey = DeterministicKey(Multicodec.P521Pub, 67);
        rawKey[1] = 0x02;

        Assert.Throws<ArgumentException>(() => Multikey.Encode(Multicodec.P521Pub, rawKey));

        var prefixed = Multicodec.Prefix(Multicodec.P521Pub, rawKey);
        var multibase = Multibase.Encode(prefixed, MultibaseEncoding.Base58Btc, includePrefix: true);
        Assert.False(Multikey.TryDecode(multibase, out _, out _));
    }

    [Theory]
    [InlineData(Multicodec.Ed25519Pub, 32)]
    [InlineData(Multicodec.X25519Pub, 32)]
    [InlineData(Multicodec.Bls12381G1Pub, 48)]
    [InlineData(Multicodec.Bls12381G2Pub, 96)]
    public void NonSec1KeyTypes_AcceptAnyLeadingByte(ulong codec, int length)
    {
        // ed25519/x25519/BLS keys are not SEC1 points — the prefix guard must not apply (#45).
        foreach (var leading in new byte[] { 0x00, 0x04, 0xFF })
        {
            var rawKey = DeterministicKey(codec, length);
            rawKey[0] = leading;

            var multibase = Multikey.Encode(codec, rawKey);

            Assert.True(Multikey.TryDecode(multibase, out var decodedCodec, out var decodedKey));
            Assert.Equal(codec, decodedCodec);
            Assert.Equal(rawKey, decodedKey);
        }
    }

    private static byte[] DeterministicKey(int length) => DeterministicKey(Multicodec.Ed25519Pub, length);

    private static byte[] DeterministicKey(ulong codec, int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(i + 1);
        }

        if (codec is Multicodec.Secp256k1Pub or Multicodec.P256Pub or Multicodec.P384Pub or Multicodec.P521Pub)
        {
            bytes[0] = 0x02; // SEC1 compressed-point prefix

            if (codec == Multicodec.P521Pub)
            {
                bytes[1] = 0x01; // top byte of the 66-byte x-coordinate must be 0x00/0x01
            }
        }

        return bytes;
    }
}
