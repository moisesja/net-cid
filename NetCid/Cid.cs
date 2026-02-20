namespace NetCid;

/// <summary>
/// Content Identifier (CID) as specified by multiformats.
/// </summary>
public sealed class Cid : IEquatable<Cid>
{
    private const int CanonicalV0ByteLength = 34;

    /// <summary>
    /// Default maximum allowed length for CID text input.
    /// </summary>
    public const int DefaultMaxInputStringLength = 4096;

    /// <summary>
    /// Default maximum allowed size for CID binary input.
    /// </summary>
    public const int DefaultMaxInputByteLength = 1_048_576;

    private readonly byte[] _bytes;

    private Cid(CidVersion version, ulong codec, MultihashDigest multihash, byte[] bytes)
    {
        Version = version;
        Codec = codec;
        Multihash = multihash;
        _bytes = bytes;
    }

    /// <summary>
    /// CID version.
    /// </summary>
    public CidVersion Version { get; }

    /// <summary>
    /// Multicodec code.
    /// </summary>
    public ulong Codec { get; }

    /// <summary>
    /// Multicodec name, when known.
    /// </summary>
    public string CodecName
    {
        get
        {
            if (Multicodec.TryGetName(Codec, out var name) && name is not null)
            {
                return name;
            }

            return $"0x{Codec:x}";
        }
    }

    /// <summary>
    /// Multihash digest.
    /// </summary>
    public MultihashDigest Multihash { get; }

    /// <summary>
    /// Binary CID bytes.
    /// </summary>
    public ReadOnlySpan<byte> Bytes => _bytes;

    public static Cid CreateV0(MultihashDigest multihash)
    {
        EnsureV0Multihash(multihash);
        return new Cid(CidVersion.V0, Multicodec.DagPb, multihash, multihash.ToByteArray());
    }

    public static Cid CreateV1(ulong codec, MultihashDigest multihash)
    {
        if (codec > Varint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(codec), codec, $"Multicodec code must be <= {Varint.MaxValue}.");
        }

        var versionLength = Varint.GetEncodedLength((ulong)CidVersion.V1);
        var codecLength = Varint.GetEncodedLength(codec);
        var hashBytes = multihash.ToByteArray();
        var bytes = new byte[versionLength + codecLength + hashBytes.Length];

        var written = Varint.Write((ulong)CidVersion.V1, bytes);
        written += Varint.Write(codec, bytes.AsSpan(written));
        hashBytes.CopyTo(bytes.AsSpan(written));

        return new Cid(CidVersion.V1, codec, multihash, bytes);
    }

    public static Cid FromContent(ReadOnlySpan<byte> data, ulong codec = Multicodec.Raw, ulong hashCode = MultihashCode.Sha2_256)
    {
        var multihash = hashCode switch
        {
            MultihashCode.Sha2_256 => MultihashDigest.Sha2_256(data),
            MultihashCode.Sha2_512 => MultihashDigest.Sha2_512(data),
            _ => throw new NotSupportedException($"Hash function 0x{hashCode:x} is not implemented.")
        };

        return CreateV1(codec, multihash);
    }

    public static Cid Parse(string value)
        => Parse(value, DefaultMaxInputStringLength, DefaultMaxInputByteLength);

    public static Cid Parse(string value, int maxInputStringLength, int maxInputByteLength)
    {
        ValidateLimit(maxInputStringLength, nameof(maxInputStringLength));
        ValidateLimit(maxInputByteLength, nameof(maxInputByteLength));

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CidFormatException("CID string cannot be null, empty, or whitespace.");
        }

        if (value.Length > maxInputStringLength)
        {
            throw new CidFormatException(
                $"CID string length {value.Length} exceeds the allowed limit of {maxInputStringLength} characters.");
        }

        if (LooksLikeV0String(value))
        {
            return Decode(Multibase.DecodeBase58Btc(value, maxInputStringLength), maxInputByteLength);
        }

        var bytes = Multibase.Decode(value, out _, maxInputStringLength);
        if (!bytes.AsSpan().IsEmpty && bytes[0] == MultihashCode.Sha2_256)
        {
            throw new CidFormatException("CIDv0 string must not be multibase-prefixed.");
        }

        return Decode(bytes, maxInputByteLength);
    }

    public static bool TryParse(string value, out Cid? cid)
        => TryParse(value, out cid, DefaultMaxInputStringLength, DefaultMaxInputByteLength);

    public static bool TryParse(string value, out Cid? cid, int maxInputStringLength, int maxInputByteLength)
    {
        cid = null;

        try
        {
            cid = Parse(value, maxInputStringLength, maxInputByteLength);
            return true;
        }
        catch (Exception ex) when (ex is CidFormatException or ArgumentException)
        {
            return false;
        }
    }

    public static Cid Decode(ReadOnlySpan<byte> bytes)
        => Decode(bytes, DefaultMaxInputByteLength);

    public static Cid Decode(ReadOnlySpan<byte> bytes, int maxInputByteLength)
    {
        ValidateLimit(maxInputByteLength, nameof(maxInputByteLength));

        if (bytes.IsEmpty)
        {
            throw new CidFormatException("CID bytes cannot be empty.");
        }

        if (bytes.Length > maxInputByteLength)
        {
            throw new CidFormatException(
                $"CID byte length {bytes.Length} exceeds the allowed limit of {maxInputByteLength} bytes.");
        }

        if (LooksLikeCanonicalV0Bytes(bytes))
        {
            var v0Multihash = MultihashDigest.Parse(bytes, out var readBytes);
            if (readBytes != bytes.Length)
            {
                throw new CidFormatException("Invalid CIDv0 bytes.");
            }

            return new Cid(CidVersion.V0, Multicodec.DagPb, v0Multihash, bytes.ToArray());
        }

        if (!Varint.TryDecode(bytes, out var version, out var versionLength))
        {
            throw new CidFormatException("CID bytes are missing a valid version field.");
        }

        if (version is 2 or 3)
        {
            throw new CidFormatException($"CID version {version} is reserved.");
        }

        if (version != (ulong)CidVersion.V1)
        {
            throw new CidFormatException($"Unsupported CID version: {version}.");
        }

        var codecSpan = bytes.Slice(versionLength);
        if (!Varint.TryDecode(codecSpan, out var codec, out var codecLength))
        {
            throw new CidFormatException("CID bytes are missing a valid codec field.");
        }

        var multihashSpan = codecSpan.Slice(codecLength);
        if (!MultihashDigest.TryParse(multihashSpan, out var multihash, out var multihashLength))
        {
            throw new CidFormatException("CID bytes contain an invalid multihash.");
        }

        if (multihashLength != multihashSpan.Length)
        {
            throw new CidFormatException("CID bytes contain trailing data.");
        }

        return new Cid(CidVersion.V1, codec, multihash, bytes.ToArray());
    }

    public static bool TryDecode(ReadOnlySpan<byte> bytes, out Cid? cid)
        => TryDecode(bytes, out cid, DefaultMaxInputByteLength);

    public static bool TryDecode(ReadOnlySpan<byte> bytes, out Cid? cid, int maxInputByteLength)
    {
        cid = null;
        try
        {
            cid = Decode(bytes, maxInputByteLength);
            return true;
        }
        catch (CidFormatException)
        {
            return false;
        }
    }

    public Cid ToV0()
    {
        if (Version == CidVersion.V0)
        {
            return this;
        }

        if (Codec != Multicodec.DagPb)
        {
            throw new InvalidOperationException("Only dag-pb CIDv1 values can be converted to CIDv0.");
        }

        EnsureV0Multihash(Multihash);
        return CreateV0(Multihash);
    }

    public Cid ToV1(ulong? codec = null)
    {
        if (Version == CidVersion.V1 && codec is null)
        {
            return this;
        }

        var targetCodec = codec ?? Codec;
        return CreateV1(targetCodec, Multihash);
    }

    public byte[] ToByteArray() => _bytes.ToArray();

    public string ToString(MultibaseEncoding encoding)
    {
        if (Version == CidVersion.V0 && encoding == MultibaseEncoding.Base58Btc)
        {
            return Multibase.EncodeBase58Btc(_bytes, includePrefix: false);
        }

        if (Version == CidVersion.V0)
        {
            return ToV1().ToString(encoding);
        }

        return Multibase.Encode(_bytes, encoding, includePrefix: true);
    }

    public override string ToString()
        => Version == CidVersion.V0
            ? Multibase.EncodeBase58Btc(_bytes, includePrefix: false)
            : Multibase.Encode(_bytes, MultibaseEncoding.Base32Lower, includePrefix: true);

    public bool Equals(Cid? other)
        => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj)
        => obj is Cid other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var current in _bytes)
        {
            hash.Add(current);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(Cid? left, Cid? right)
        => Equals(left, right);

    public static bool operator !=(Cid? left, Cid? right)
        => !Equals(left, right);

    private static bool LooksLikeV0String(string value)
        => value.Length == 46 && value.StartsWith("Qm", StringComparison.Ordinal);

    private static bool LooksLikeCanonicalV0Bytes(ReadOnlySpan<byte> bytes)
        => bytes.Length == CanonicalV0ByteLength
            && bytes[0] == MultihashCode.Sha2_256
            && bytes[1] == 0x20;

    private static void EnsureV0Multihash(MultihashDigest multihash)
    {
        if (multihash.Code != MultihashCode.Sha2_256 || multihash.DigestLength != 32)
        {
            throw new InvalidOperationException("CIDv0 requires a sha2-256 multihash with 32-byte digest.");
        }
    }

    private static void ValidateLimit(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Input size limit must be at least 1.");
        }
    }
}
