using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCid;

/// <summary>
/// JSON Canonicalization Scheme (JCS) per
/// <see href="https://www.rfc-editor.org/rfc/rfc8785">RFC 8785</see>.
/// Produces a deterministic byte-for-byte stable UTF-8 serialization of a JSON value
/// so that two writers of the same logical value produce identical bytes — and therefore
/// identical CIDs, hashes, or signatures.
/// </summary>
/// <remarks>
/// <para>
/// Supports objects, arrays, strings, IEEE-754 double-precision numbers (per ECMA-262
/// §6.1.6.1.20, as RFC 8785 §3.2.2.3 requires), <c>true</c>, <c>false</c>, and <c>null</c>.
/// <c>NaN</c> and <c>±infinity</c> throw <see cref="JcsFormatException"/>.
/// </para>
/// <para>
/// Strings (and object member names) must be well-formed UTF-16. An unpaired surrogate has no
/// UTF-8 representation; rather than let <see cref="JsonSerializer"/> silently substitute U+FFFD
/// — which would collapse two distinct malformed inputs to identical bytes — it throws
/// <see cref="JcsFormatException"/>. Valid surrogate pairs and a legitimate U+FFFD are unaffected.
/// This guard covers parsed input (<see cref="JsonElement"/>, <see cref="JsonNode.Parse(string,JsonNodeOptions?,JsonDocumentOptions)"/>)
/// and <see cref="JsonValue"/> nodes built from <see langword="string"/> or <see langword="char"/>
/// primitives. A <see cref="JsonValue"/> wrapping an arbitrary CLR object (e.g.
/// <c>JsonValue.Create(someObject)</c>, a dictionary, or a collection) is expanded by
/// <see cref="JsonSerializer"/>, which substitutes U+FFFD <em>before</em> this validation can
/// inspect its members; when canonicalizing untrusted UTF-16, pass parsed JSON or primitive-built
/// nodes rather than a node wrapping a raw CLR object.
/// </para>
/// <para>
/// Object member names must be unique. RFC 8785 builds on I-JSON (RFC 7493 §2.3), which
/// forbids duplicate member names; duplicates — which <see cref="JsonDocument"/> preserves —
/// throw <see cref="JcsFormatException"/> rather than producing ambiguous, non-canonical
/// output that two parsers could read differently (a signature-confusion vector).
/// </para>
/// <para>
/// JSON nested deeper than 64 levels throws <see cref="JcsFormatException"/> rather than
/// overflowing the stack. Because JCS processes untrusted credential JSON, the unbounded
/// recursion that would otherwise occur is a denial-of-service vector: a
/// <see cref="StackOverflowException"/> cannot be caught in .NET and terminates the process.
/// </para>
/// <para>Thread-safe — all members are static and stateless.</para>
/// </remarks>
public static class JcsCanonicalizer
{
    /// <summary>
    /// Default maximum size, in bytes, of the canonical UTF-8 output. Canonicalization that
    /// would produce more than this throws <see cref="JcsFormatException"/>. This is a
    /// defense-in-depth bound for applications canonicalizing untrusted JSON, mirroring the
    /// input limits on the CID/Multibase parse paths (<c>1 MiB</c>). Callers processing
    /// known-safe, larger documents can raise it via the <c>maxOutputBytes</c> overloads.
    /// </summary>
    public const int DefaultMaxOutputByteLength = 1_048_576;

    private const int StackBufferThreshold = 256;

    /// <summary>
    /// Maximum JSON nesting depth accepted during canonicalization. Matches the default
    /// <see cref="JsonSerializerOptions.MaxDepth"/> of 64. Input nested deeper than this
    /// throws <see cref="JcsFormatException"/> instead of overflowing the stack.
    /// </summary>
    private const int MaxDepth = 64;

    // Make our depth guard the single source of truth: validation already rejects anything
    // past MaxDepth, so the serializer only ever sees depth ≤ MaxDepth. The +1 of headroom
    // guarantees a value at exactly MaxDepth is never rejected by System.Text.Json's own
    // default depth limit with a different (non-JcsFormatException) error. MaxDepth is the
    // only changed option, so the produced document — and thus the canonical bytes — is
    // identical to the default-options serialization.
    private static readonly JsonSerializerOptions SerializeOptions = new() { MaxDepth = MaxDepth + 1 };

    /// <summary>
    /// Canonicalize <paramref name="node"/> per RFC 8785. A <see langword="null"/>
    /// reference is treated as the JSON literal <c>null</c>.
    /// </summary>
    /// <returns>UTF-8 encoded canonical JSON bytes.</returns>
    /// <exception cref="JcsFormatException">
    /// The node contains a value JCS cannot represent deterministically — <c>NaN</c>
    /// or <c>±infinity</c> — has duplicate object member names, nests deeper than the
    /// supported limit of 64 levels, or
    /// canonicalizes to more than <see cref="DefaultMaxOutputByteLength"/> bytes.
    /// </exception>
    public static byte[] Canonicalize(JsonNode? node)
        => Canonicalize(node, DefaultMaxOutputByteLength);

    /// <summary>
    /// Canonicalize <paramref name="node"/> per RFC 8785, rejecting output larger than
    /// <paramref name="maxOutputBytes"/>.
    /// </summary>
    /// <returns>UTF-8 encoded canonical JSON bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputBytes"/> is less than 1.</exception>
    /// <exception cref="JcsFormatException">
    /// The node contains a value JCS cannot represent deterministically, has duplicate object member
    /// names, nests deeper than the supported limit of 64 levels, or canonicalizes to more than
    /// <paramref name="maxOutputBytes"/> bytes.
    /// </exception>
    public static byte[] Canonicalize(JsonNode? node, int maxOutputBytes)
    {
        var writer = new ArrayBufferWriter<byte>();
        Canonicalize(node, writer, maxOutputBytes);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Canonicalize a <see cref="JsonElement"/> per RFC 8785.
    /// </summary>
    /// <returns>UTF-8 encoded canonical JSON bytes.</returns>
    /// <exception cref="JcsFormatException">
    /// The element contains a value JCS cannot represent deterministically, has duplicate object member
    /// names, nests deeper than the supported limit of 64 levels, or canonicalizes to more than
    /// <see cref="DefaultMaxOutputByteLength"/> bytes.
    /// </exception>
    public static byte[] Canonicalize(JsonElement element)
        => Canonicalize(element, DefaultMaxOutputByteLength);

    /// <summary>
    /// Canonicalize a <see cref="JsonElement"/> per RFC 8785, rejecting output larger than
    /// <paramref name="maxOutputBytes"/>.
    /// </summary>
    /// <returns>UTF-8 encoded canonical JSON bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputBytes"/> is less than 1.</exception>
    /// <exception cref="JcsFormatException">
    /// The element contains a value JCS cannot represent deterministically, has duplicate object member
    /// names, nests deeper than the supported limit of 64 levels, or canonicalizes to more than
    /// <paramref name="maxOutputBytes"/> bytes.
    /// </exception>
    public static byte[] Canonicalize(JsonElement element, int maxOutputBytes)
    {
        ValidateOutputLimit(maxOutputBytes);

        var writer = new ArrayBufferWriter<byte>();
        try
        {
            WriteElement(element, new LimitedBufferWriter(writer, maxOutputBytes), 0);
        }
        catch (InvalidOperationException ex)
        {
            // A parsed string value or object member name holding an unpaired UTF-16 surrogate
            // makes JsonElement.GetString()/JsonProperty.Name throw InvalidOperationException.
            // Surface it as the uniform JcsFormatException; the local buffer is discarded, so no
            // partial — and no silently U+FFFD-substituted — output escapes. (Duplicate-member and
            // depth/output-limit errors throw JcsFormatException, not InvalidOperationException, so
            // they propagate untouched.)
            throw new JcsFormatException(UnpairedSurrogateMessage, ex);
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Canonicalize <paramref name="node"/> and write the bytes directly into
    /// <paramref name="destination"/>. Useful for hashing pipelines that do not want
    /// an intermediate <c>byte[]</c> allocation.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="JcsFormatException">
    /// The node contains a value JCS cannot represent deterministically, has duplicate object member
    /// names, nests deeper than the supported limit of 64 levels, or canonicalizes to more than
    /// <see cref="DefaultMaxOutputByteLength"/> bytes.
    /// </exception>
    public static void Canonicalize(JsonNode? node, IBufferWriter<byte> destination)
        => Canonicalize(node, destination, DefaultMaxOutputByteLength);

    /// <summary>
    /// Canonicalize <paramref name="node"/> into <paramref name="destination"/>, rejecting
    /// output larger than <paramref name="maxOutputBytes"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputBytes"/> is less than 1.</exception>
    /// <exception cref="JcsFormatException">
    /// The node contains a value JCS cannot represent deterministically, has duplicate object member
    /// names, nests deeper than the supported limit of 64 levels, or canonicalizes to more than
    /// <paramref name="maxOutputBytes"/> bytes.
    /// </exception>
    public static void Canonicalize(JsonNode? node, IBufferWriter<byte> destination, int maxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateOutputLimit(maxOutputBytes);

        // Enforce the output cap at the single choke point through which every byte is
        // committed; the wrapper throws before the byte that would cross the limit is counted.
        var writer = new LimitedBufferWriter(destination, maxOutputBytes);

        if (node is null)
        {
            WriteRaw(writer, "null"u8);
            return;
        }

        // JsonNode may wrap raw .NET doubles/floats holding NaN/±infinity, or strings and member
        // names containing unpaired UTF-16 surrogates. The default System.Text.Json serializer
        // would silently rewrite a surrogate to U+FFFD (collapsing distinct malformed inputs to
        // identical bytes) or throw a non-JCS exception; surface a JcsFormatException instead so
        // callers can handle it uniformly. This walk runs first, so its depth guard protects the
        // serialize step below from overflowing on a deeply nested node, and it inspects raw
        // strings before serialization can mangle them.
        try
        {
            ValidateNode(node, 0);
        }
        catch (InvalidOperationException ex)
        {
            // A parsed JsonNode holding an element-backed unpaired surrogate makes the value/key
            // accessors in the walk throw InvalidOperationException ("incomplete UTF-16"); convert
            // it to the same JcsFormatException the raw-string check produces.
            throw new JcsFormatException(UnpairedSurrogateMessage, ex);
        }
        catch (ArgumentException)
        {
            // A parsed JsonObject lazily builds its backing dictionary on first enumeration and
            // rejects duplicate member names with an ArgumentException — the only ArgumentException
            // source in the walk above (NaN/±infinity and unpaired surrogates throw JcsFormatException,
            // which is not an ArgumentException, so they propagate untouched). Don't translate it here:
            // fall through
            // to the serialize + WriteElement path below so WriteObject reports the duplicate with
            // its precise, key-naming message — the single source of truth the JsonElement overload
            // also uses. SerializeToDocument preserves duplicates, so the offending member is
            // guaranteed to reach (and trip) WriteObject (RFC 8785 / RFC 7493).
        }

        JsonDocument document;
        try
        {
            document = JsonSerializer.SerializeToDocument(node, SerializeOptions);
        }
        catch (JsonException ex)
        {
            throw new JcsFormatException(
                "JSON value cannot be serialized for canonicalization.", ex);
        }
        catch (ArgumentException ex)
        {
            // System.Text.Json's signal for NaN/±infinity hidden inside a JsonValue wrapping a
            // raw CLR object (e.g. a Dictionary/POCO), which ValidateNode cannot see into; it
            // must surface as JcsFormatException like every other unrepresentable-number path
            // (#47). Distinct from the duplicate-key ArgumentException around ValidateNode
            // above, which intentionally falls through.
            throw new JcsFormatException(
                "JSON value contains NaN or infinity, which cannot be canonicalized.", ex);
        }

        using (document)
        {
            WriteElement(document.RootElement, writer, 0);
        }
    }

    private static void ValidateOutputLimit(int maxOutputBytes)
    {
        if (maxOutputBytes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes), maxOutputBytes, "Output size limit must be at least 1.");
        }
    }

    private static void ThrowIfTooDeep(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new JcsFormatException(
                $"JSON nesting exceeds the canonicalization depth limit of {MaxDepth}.");
        }
    }

    private const string UnpairedSurrogateMessage =
        "JSON string contains invalid UTF-16 (unpaired surrogate); cannot canonicalize.";

    // RFC 8785 §3.2.2.2 serializes strings as UTF-8. An unpaired UTF-16 surrogate has no UTF-8
    // representation; System.Text.Json would silently substitute U+FFFD, so two distinct malformed
    // inputs could collapse to identical canonical bytes (a collision / signature-confusion vector).
    // Reject it instead. A valid surrogate PAIR (high followed by low) is left untouched, and a
    // legitimately-supplied U+FFFD is not a surrogate, so it passes through unchanged.
    private static void ThrowIfUnpairedSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    throw new JcsFormatException(UnpairedSurrogateMessage);
                }

                i++; // valid pair — skip the paired low surrogate
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                throw new JcsFormatException(UnpairedSurrogateMessage);
            }
        }
    }

    private static void ValidateNode(JsonNode node, int depth)
    {
        ThrowIfTooDeep(depth);

        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                {
                    ThrowIfUnpairedSurrogate(key);
                    if (child is not null)
                    {
                        ValidateNode(child, depth + 1);
                    }
                }
                break;

            case JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null)
                    {
                        ValidateNode(item, depth + 1);
                    }
                }
                break;

            case JsonValue val:
                if (val.TryGetValue<double>(out var d))
                {
                    if (double.IsNaN(d))
                    {
                        throw new JcsFormatException("JCS cannot represent NaN (RFC 8785 §3.2.2.3).");
                    }
                    if (double.IsPositiveInfinity(d))
                    {
                        throw new JcsFormatException("JCS cannot represent +infinity (RFC 8785 §3.2.2.3).");
                    }
                    if (double.IsNegativeInfinity(d))
                    {
                        throw new JcsFormatException("JCS cannot represent -infinity (RFC 8785 §3.2.2.3).");
                    }
                }
                else if (val.TryGetValue<float>(out var f))
                {
                    if (float.IsNaN(f))
                    {
                        throw new JcsFormatException("JCS cannot represent NaN (RFC 8785 §3.2.2.3).");
                    }
                    if (float.IsPositiveInfinity(f))
                    {
                        throw new JcsFormatException("JCS cannot represent +infinity (RFC 8785 §3.2.2.3).");
                    }
                    if (float.IsNegativeInfinity(f))
                    {
                        throw new JcsFormatException("JCS cannot represent -infinity (RFC 8785 §3.2.2.3).");
                    }
                }
                else if (val.TryGetValue<string>(out var s) && s is not null)
                {
                    ThrowIfUnpairedSurrogate(s);
                }
                else if (val.TryGetValue<char>(out var ch) && char.IsSurrogate(ch))
                {
                    // A char-backed JsonValue (e.g. JsonValue.Create('\uD800')) serializes as a
                    // one-code-unit JSON string. A single UTF-16 code unit can never form a
                    // surrogate PAIR, so any surrogate char is unpaired and has no UTF-8
                    // representation — reject it rather than let the serializer substitute U+FFFD.
                    // (TryGetValue<string>/<double>/<float> all return false for a char-backed
                    // value, so it reaches here; numbers and strings never match TryGetValue<char>.)
                    throw new JcsFormatException(UnpairedSurrogateMessage);
                }
                break;
        }
    }

    private static void WriteElement(JsonElement element, IBufferWriter<byte> writer, int depth)
    {
        ThrowIfTooDeep(depth);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, writer, depth);
                break;
            case JsonValueKind.Array:
                WriteArray(element, writer, depth);
                break;
            case JsonValueKind.String:
                WriteString(element.GetString()!, writer);
                break;
            case JsonValueKind.Number:
                WriteNumber(element, writer);
                break;
            case JsonValueKind.True:
                WriteRaw(writer, "true"u8);
                break;
            case JsonValueKind.False:
                WriteRaw(writer, "false"u8);
                break;
            case JsonValueKind.Null:
                WriteRaw(writer, "null"u8);
                break;
            case JsonValueKind.Undefined:
            default:
                throw new JcsFormatException(
                    $"JCS cannot canonicalize JSON value of kind '{element.ValueKind}'.");
        }
    }

    private static void WriteObject(JsonElement obj, IBufferWriter<byte> writer, int depth)
    {
        WriteByte(writer, (byte)'{');

        // RFC 8785 §3.2.3: object member names are sorted by UTF-16 code unit order,
        // which is exactly what string.CompareOrdinal compares.
        var properties = new List<JsonProperty>();
        foreach (var prop in obj.EnumerateObject())
        {
            properties.Add(prop);
        }

        properties.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));

        // RFC 8785 builds on I-JSON (RFC 7493 §2.3), which forbids duplicate member names.
        // JsonDocument.Parse preserves duplicates, so reject them rather than emit both: two
        // members with the same name are non-canonical and let different parsers disagree on the
        // authoritative value — a signature-confusion vector. After the ordinal sort above, any
        // duplicate names are adjacent, so a single linear scan finds them.
        for (var i = 1; i < properties.Count; i++)
        {
            if (string.Equals(properties[i].Name, properties[i - 1].Name, StringComparison.Ordinal))
            {
                throw new JcsFormatException(
                    $"Duplicate object member name '{properties[i].Name}' is not allowed (RFC 8785 / RFC 7493).");
            }
        }

        for (var i = 0; i < properties.Count; i++)
        {
            if (i > 0)
            {
                WriteByte(writer, (byte)',');
            }

            WriteString(properties[i].Name, writer);
            WriteByte(writer, (byte)':');
            WriteElement(properties[i].Value, writer, depth + 1);
        }

        WriteByte(writer, (byte)'}');
    }

    private static void WriteArray(JsonElement arr, IBufferWriter<byte> writer, int depth)
    {
        WriteByte(writer, (byte)'[');

        var first = true;
        foreach (var item in arr.EnumerateArray())
        {
            if (!first)
            {
                WriteByte(writer, (byte)',');
            }

            first = false;
            WriteElement(item, writer, depth + 1);
        }

        WriteByte(writer, (byte)']');
    }

    // RFC 8785 §3.2.2.3 requires every JSON number be quantized to an IEEE-754
    // double before ECMA-262 §6.1.6.1.20 (Number::toString) runs. Integer literals
    // with |x| ≤ 2^53 are exactly representable as doubles, so writing the integer
    // text directly produces the same bytes as the full helper without a float
    // round-trip. Above that boundary the integer text and the rounded double's
    // canonical form diverge — e.g. `9007199254740993` rounds to `9007199254740992`
    // — so we MUST fall through to the helper to keep two encoders byte-identical.
    private const long MaxSafeInteger = 9007199254740992L;
    private const ulong MaxSafeIntegerUnsigned = 9007199254740992UL;

    private static void WriteNumber(JsonElement num, IBufferWriter<byte> writer)
    {
        if (num.TryGetInt64(out var l) && l >= -MaxSafeInteger && l <= MaxSafeInteger)
        {
            WriteAscii(writer, l.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (num.TryGetUInt64(out var ul) && ul <= MaxSafeIntegerUnsigned)
        {
            WriteAscii(writer, ul.ToString(CultureInfo.InvariantCulture));
            return;
        }

        // Anything else — fractional, exponential, or an integer beyond ±2^53 — goes
        // through the IEEE-754 path. Integer literals so large they parse to ±∞ trip
        // the helper's defensive infinity check and throw with the documented message.
        var d = num.GetDouble();
        WriteAscii(writer, EcmaScriptNumber.ToCanonicalString(d));
    }

    private static void WriteString(string value, IBufferWriter<byte> writer)
    {
        WriteByte(writer, (byte)'"');

        // Encode the entire string to UTF-8 once, then walk the bytes. UTF-8 multibyte
        // continuation bytes all have the high bit set (>= 0x80), so the per-byte escape
        // table only acts on single-byte ASCII characters — multibyte sequences pass
        // through verbatim, which is exactly what JCS §3.2.2.2 requires.
        var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        byte[]? rented = null;
        Span<byte> utf8 = maxBytes <= StackBufferThreshold
            ? stackalloc byte[StackBufferThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            var written = Encoding.UTF8.GetBytes(value, utf8);
            WriteEscapedUtf8(writer, utf8[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        WriteByte(writer, (byte)'"');
    }

    private static void WriteEscapedUtf8(IBufferWriter<byte> writer, ReadOnlySpan<byte> utf8)
    {
        // Walk the UTF-8 bytes and escape only what RFC 8785 §3.2.2.2 strictly requires:
        // the seven short escapes (\" \\ \b \t \n \f \r) and \u00XX for control bytes
        // below U+0020. Solidus is NOT escaped. Bytes >= 0x80 are emitted as raw UTF-8.
        Span<byte> u = stackalloc byte[6] { (byte)'\\', (byte)'u', (byte)'0', (byte)'0', 0, 0 };

        var start = 0;
        for (var i = 0; i < utf8.Length; i++)
        {
            var b = utf8[i];

            ReadOnlySpan<byte> escape;
            switch (b)
            {
                case 0x22: escape = "\\\""u8; break;
                case 0x5C: escape = "\\\\"u8; break;
                case 0x08: escape = "\\b"u8; break;
                case 0x09: escape = "\\t"u8; break;
                case 0x0A: escape = "\\n"u8; break;
                case 0x0C: escape = "\\f"u8; break;
                case 0x0D: escape = "\\r"u8; break;
                default:
                    if (b < 0x20)
                    {
                        if (i > start)
                        {
                            WriteRaw(writer, utf8.Slice(start, i - start));
                        }

                        u[4] = HexNybble((b >> 4) & 0xF);
                        u[5] = HexNybble(b & 0xF);
                        WriteRaw(writer, u);
                        start = i + 1;
                    }
                    continue;
            }

            if (i > start)
            {
                WriteRaw(writer, utf8.Slice(start, i - start));
            }
            WriteRaw(writer, escape);
            start = i + 1;
        }

        if (start < utf8.Length)
        {
            WriteRaw(writer, utf8[start..]);
        }
    }

    private static byte HexNybble(int n) => (byte)(n < 10 ? '0' + n : 'a' + (n - 10));

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        var span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }

    private static void WriteRaw(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var span = writer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        writer.Advance(bytes.Length);
    }

    private static void WriteAscii(IBufferWriter<byte> writer, string ascii)
    {
        var span = writer.GetSpan(ascii.Length);
        for (var i = 0; i < ascii.Length; i++)
        {
            span[i] = (byte)ascii[i];
        }

        writer.Advance(ascii.Length);
    }

    /// <summary>
    /// Forwards buffer requests to an inner writer while enforcing a maximum committed-byte
    /// count. Because every byte the canonicalizer emits is committed through
    /// <see cref="Advance"/>, this single choke point bounds the total output. The throw
    /// happens <em>before</em> the crossing bytes are committed to the inner writer, so a
    /// caller-supplied destination never receives more than the limit.
    /// </summary>
    private sealed class LimitedBufferWriter(IBufferWriter<byte> inner, int maxBytes) : IBufferWriter<byte>
    {
        private int _written;

        public void Advance(int count)
        {
            // `maxBytes - _written` is the remaining budget (always >= 0, since we throw before
            // exceeding it). Comparing this way avoids any int overflow on `_written + count`.
            if (count > maxBytes - _written)
            {
                throw new JcsFormatException(
                    $"Canonical output exceeds the limit of {maxBytes} bytes.");
            }

            _written += count;
            inner.Advance(count);
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
    }
}
