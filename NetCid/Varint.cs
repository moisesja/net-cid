namespace NetCid;

/// <summary>
/// Unsigned varint encoder and decoder used by multiformats.
/// </summary>
public static class Varint
{
    public const ulong MaxValue = 0x7FFF_FFFF_FFFF_FFFF;

    public static int GetEncodedLength(ulong value)
    {
        EnsureInRange(value);

        var length = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }

        return length;
    }

    public static byte[] Encode(ulong value)
    {
        EnsureInRange(value);

        var output = new byte[GetEncodedLength(value)];
        _ = Write(value, output);
        return output;
    }

    public static int Write(ulong value, Span<byte> destination)
    {
        EnsureInRange(value);

        var required = GetEncodedLength(value);
        if (destination.Length < required)
        {
            throw new ArgumentException("Destination buffer is too small for the encoded varint.", nameof(destination));
        }

        var index = 0;
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            destination[index++] = next;
        } while (value != 0);

        return index;
    }

    public static ulong Decode(ReadOnlySpan<byte> source, out int bytesRead)
    {
        if (!TryDecode(source, out var value, out bytesRead))
        {
            throw new CidFormatException("Invalid unsigned varint encoding.");
        }

        return value;
    }

    public static bool TryDecode(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;

        if (source.IsEmpty)
        {
            return false;
        }

        var shift = 0;
        for (var index = 0; index < 9; index++)
        {
            if (index >= source.Length)
            {
                value = 0;
                bytesRead = 0;
                return false;
            }

            var current = source[index];
            if (index == 8 && (current & 0x80) != 0)
            {
                value = 0;
                bytesRead = 0;
                return false;
            }

            var chunk = (ulong)(current & 0x7F);
            value |= chunk << shift;

            if ((current & 0x80) == 0)
            {
                if (index > 0 && current == 0)
                {
                    value = 0;
                    bytesRead = 0;
                    return false;
                }

                if (value > MaxValue)
                {
                    value = 0;
                    bytesRead = 0;
                    return false;
                }

                bytesRead = index + 1;
                return true;
            }

            shift += 7;
        }

        value = 0;
        bytesRead = 0;
        return false;
    }

    private static void EnsureInRange(ulong value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Unsigned varint value must be <= {MaxValue}.");
        }
    }
}
