using System.Numerics;
using System.Text;

namespace NetCid;

/// <summary>
/// Multibase encode/decode helpers for CID strings.
/// </summary>
public static class Multibase
{
    private const string Base32LowerAlphabet = "abcdefghijklmnopqrstuvwxyz234567";
    private const string Base32UpperAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Base36LowerAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const string Base36UpperAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Base58BtcAlphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly IReadOnlyDictionary<char, int> Base36LowerIndex = BuildAlphabetIndex(Base36LowerAlphabet);
    private static readonly IReadOnlyDictionary<char, int> Base36UpperIndex = BuildAlphabetIndex(Base36UpperAlphabet);
    private static readonly IReadOnlyDictionary<char, int> Base58BtcIndex = BuildAlphabetIndex(Base58BtcAlphabet);

    public static string Encode(ReadOnlySpan<byte> bytes, MultibaseEncoding encoding, bool includePrefix = true)
    {
        var payload = EncodeWithoutPrefix(bytes, encoding);
        if (!includePrefix)
        {
            return payload;
        }

        return string.Concat(GetPrefix(encoding), payload);
    }

    public static string EncodeBase58Btc(ReadOnlySpan<byte> bytes, bool includePrefix = false)
        => Encode(bytes, MultibaseEncoding.Base58Btc, includePrefix);

    public static byte[] Decode(string text) => Decode(text, out _);

    public static byte[] Decode(string text, out MultibaseEncoding encoding)
    {
        if (!TryDecode(text, out var bytes, out encoding))
        {
            throw new CidFormatException("Input is not a valid supported multibase string.");
        }

        return bytes;
    }

    public static bool TryDecode(string text, out byte[] bytes, out MultibaseEncoding encoding)
    {
        bytes = Array.Empty<byte>();
        encoding = default;

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!TryGetEncoding(text[0], out encoding))
        {
            return false;
        }

        var payload = text.AsSpan(1);
        try
        {
            bytes = DecodeWithoutPrefix(payload, encoding);
            return true;
        }
        catch (CidFormatException)
        {
            bytes = Array.Empty<byte>();
            encoding = default;
            return false;
        }
    }

    public static byte[] DecodeBase58Btc(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        return DecodeWithoutPrefix(payload, MultibaseEncoding.Base58Btc);
    }

    public static bool TryGetEncoding(char prefix, out MultibaseEncoding encoding)
    {
        encoding = prefix switch
        {
            'b' => MultibaseEncoding.Base32Lower,
            'B' => MultibaseEncoding.Base32Upper,
            'k' => MultibaseEncoding.Base36Lower,
            'K' => MultibaseEncoding.Base36Upper,
            'z' => MultibaseEncoding.Base58Btc,
            _ => default
        };

        return prefix is 'b' or 'B' or 'k' or 'K' or 'z';
    }

    public static char GetPrefix(MultibaseEncoding encoding)
        => encoding switch
        {
            MultibaseEncoding.Base32Lower => 'b',
            MultibaseEncoding.Base32Upper => 'B',
            MultibaseEncoding.Base36Lower => 'k',
            MultibaseEncoding.Base36Upper => 'K',
            MultibaseEncoding.Base58Btc => 'z',
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unsupported multibase encoding.")
        };

    private static string EncodeWithoutPrefix(ReadOnlySpan<byte> bytes, MultibaseEncoding encoding)
        => encoding switch
        {
            MultibaseEncoding.Base32Lower => EncodeBase32(bytes, Base32LowerAlphabet),
            MultibaseEncoding.Base32Upper => EncodeBase32(bytes, Base32UpperAlphabet),
            MultibaseEncoding.Base36Lower => EncodePositionalBase(bytes, Base36LowerAlphabet, '0'),
            MultibaseEncoding.Base36Upper => EncodePositionalBase(bytes, Base36UpperAlphabet, '0'),
            MultibaseEncoding.Base58Btc => EncodePositionalBase(bytes, Base58BtcAlphabet, '1'),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unsupported multibase encoding.")
        };

    private static byte[] DecodeWithoutPrefix(ReadOnlySpan<char> payload, MultibaseEncoding encoding)
        => encoding switch
        {
            MultibaseEncoding.Base32Lower => DecodeBase32(payload),
            MultibaseEncoding.Base32Upper => DecodeBase32(payload),
            MultibaseEncoding.Base36Lower => DecodePositionalBase(payload, Base36LowerIndex, 36, '0'),
            MultibaseEncoding.Base36Upper => DecodePositionalBase(payload, Base36UpperIndex, 36, '0'),
            MultibaseEncoding.Base58Btc => DecodePositionalBase(payload, Base58BtcIndex, 58, '1'),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unsupported multibase encoding.")
        };

    private static string EncodeBase32(ReadOnlySpan<byte> bytes, string alphabet)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var output = new StringBuilder(((bytes.Length * 8) + 4) / 5);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var current in bytes)
        {
            buffer = (buffer << 8) | current;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                output.Append(alphabet[(buffer >> bitsInBuffer) & 0x1F]);
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        if (bitsInBuffer > 0)
        {
            output.Append(alphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return output.ToString();
    }

    private static byte[] DecodeBase32(ReadOnlySpan<char> payload)
    {
        if (payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        var output = new List<byte>((payload.Length * 5) / 8);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var current in payload)
        {
            if (current == '=')
            {
                throw new CidFormatException("Base32 padding is not allowed for CID strings.");
            }

            var value = current switch
            {
                >= 'a' and <= 'z' => current - 'a',
                >= 'A' and <= 'Z' => current - 'A',
                >= '2' and <= '7' => current - '2' + 26,
                _ => -1
            };

            if (value < 0)
            {
                throw new CidFormatException("Invalid base32 character.");
            }

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;

            while (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((buffer >> bitsInBuffer) & 0xFF));
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        if (bitsInBuffer > 0 && (buffer & ((1 << bitsInBuffer) - 1)) != 0)
        {
            throw new CidFormatException("Invalid non-zero trailing bits in base32 payload.");
        }

        return output.ToArray();
    }

    private static string EncodePositionalBase(ReadOnlySpan<byte> bytes, string alphabet, char zeroSymbol)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var leadingZeroes = 0;
        while (leadingZeroes < bytes.Length && bytes[leadingZeroes] == 0)
        {
            leadingZeroes++;
        }

        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var output = new StringBuilder(bytes.Length * 2);
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, alphabet.Length, out var remainder);
            output.Append(alphabet[(int)remainder]);
        }

        for (var index = 0; index < leadingZeroes; index++)
        {
            output.Append(zeroSymbol);
        }

        var chars = output.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static byte[] DecodePositionalBase(
        ReadOnlySpan<char> payload,
        IReadOnlyDictionary<char, int> alphabetIndex,
        int radix,
        char zeroSymbol)
    {
        if (payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        var leadingZeroes = 0;
        while (leadingZeroes < payload.Length && payload[leadingZeroes] == zeroSymbol)
        {
            leadingZeroes++;
        }

        var value = BigInteger.Zero;
        foreach (var current in payload)
        {
            if (!alphabetIndex.TryGetValue(current, out var digit))
            {
                throw new CidFormatException("Invalid character for multibase payload.");
            }

            value = (value * radix) + digit;
        }

        var decoded = value.IsZero ? Array.Empty<byte>() : value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (leadingZeroes == 0)
        {
            return decoded;
        }

        var result = new byte[leadingZeroes + decoded.Length];
        decoded.CopyTo(result.AsSpan(leadingZeroes));
        return result;
    }

    private static IReadOnlyDictionary<char, int> BuildAlphabetIndex(string alphabet)
    {
        var index = new Dictionary<char, int>(alphabet.Length);
        for (var i = 0; i < alphabet.Length; i++)
        {
            index[alphabet[i]] = i;
        }

        return index;
    }
}
