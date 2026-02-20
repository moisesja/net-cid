using System.Security.Cryptography;

namespace NetCid;

/// <summary>
/// Represents a multihash digest (code + digest length + digest bytes).
/// </summary>
public readonly struct MultihashDigest : IEquatable<MultihashDigest>
{
    private readonly byte[]? _digest;

    public MultihashDigest(ulong code, ReadOnlySpan<byte> digest)
    {
        if (code > Varint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, $"Multihash code must be <= {Varint.MaxValue}.");
        }

        Code = code;
        _digest = digest.ToArray();
    }

    /// <summary>
    /// Multihash function code.
    /// </summary>
    public ulong Code { get; }

    /// <summary>
    /// Digest size in bytes.
    /// </summary>
    public int DigestLength => DigestSpan.Length;

    /// <summary>
    /// Digest bytes.
    /// </summary>
    public ReadOnlySpan<byte> DigestSpan => _digest ?? Array.Empty<byte>();

    public static MultihashDigest Parse(ReadOnlySpan<byte> source, out int bytesRead)
    {
        if (!TryParse(source, out var digest, out bytesRead))
        {
            throw new CidFormatException("Invalid multihash encoding.");
        }

        return digest;
    }

    public static bool TryParse(ReadOnlySpan<byte> source, out MultihashDigest digest, out int bytesRead)
    {
        digest = default;
        bytesRead = 0;

        if (!Varint.TryDecode(source, out var code, out var codeLength))
        {
            return false;
        }

        var lengthSpan = source.Slice(codeLength);
        if (!Varint.TryDecode(lengthSpan, out var digestLength, out var digestLengthFieldSize))
        {
            return false;
        }

        if (digestLength > int.MaxValue)
        {
            return false;
        }

        int totalSize;
        try
        {
            totalSize = checked(codeLength + digestLengthFieldSize + (int)digestLength);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (source.Length < totalSize)
        {
            return false;
        }

        var digestBytes = source.Slice(codeLength + digestLengthFieldSize, (int)digestLength);
        digest = new MultihashDigest(code, digestBytes);
        bytesRead = totalSize;
        return true;
    }

    public byte[] GetDigestBytes() => DigestSpan.ToArray();

    public byte[] ToByteArray()
    {
        var codeSize = Varint.GetEncodedLength(Code);
        var digestLengthSize = Varint.GetEncodedLength((ulong)DigestLength);
        var bytes = new byte[codeSize + digestLengthSize + DigestLength];

        var written = Varint.Write(Code, bytes);
        written += Varint.Write((ulong)DigestLength, bytes.AsSpan(written));
        DigestSpan.CopyTo(bytes.AsSpan(written));
        return bytes;
    }

    public static MultihashDigest Sha2_256(ReadOnlySpan<byte> bytes)
        => new(MultihashCode.Sha2_256, SHA256.HashData(bytes));

    public static MultihashDigest Sha2_512(ReadOnlySpan<byte> bytes)
        => new(MultihashCode.Sha2_512, SHA512.HashData(bytes));

    public bool Equals(MultihashDigest other)
        => Code == other.Code && DigestSpan.SequenceEqual(other.DigestSpan);

    public override bool Equals(object? obj)
        => obj is MultihashDigest other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Code);
        foreach (var current in DigestSpan)
        {
            hashCode.Add(current);
        }

        return hashCode.ToHashCode();
    }

    public static bool operator ==(MultihashDigest left, MultihashDigest right) => left.Equals(right);

    public static bool operator !=(MultihashDigest left, MultihashDigest right) => !left.Equals(right);
}
