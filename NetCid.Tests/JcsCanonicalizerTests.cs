using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCid.Tests;

public sealed class JcsCanonicalizerTests
{
    private static string Canon(JsonNode? node)
        => Encoding.UTF8.GetString(JcsCanonicalizer.Canonicalize(node));

    private static string Canon(string json)
        => Encoding.UTF8.GetString(JcsCanonicalizer.Canonicalize(JsonNode.Parse(json)));

    private static string CanonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Encoding.UTF8.GetString(JcsCanonicalizer.Canonicalize(doc.RootElement));
    }

    [Fact]
    public void Null_Reference_Serializes_As_Null_Literal()
    {
        Assert.Equal("null", Canon((JsonNode?)null));
    }

    [Fact]
    public void Primitive_True_False_Null()
    {
        Assert.Equal("true", Canon(JsonValue.Create(true)));
        Assert.Equal("false", Canon(JsonValue.Create(false)));
        Assert.Equal("null", Canon("null"));
    }

    [Fact]
    public void Object_Keys_Sort_By_Code_Unit_Pure_Ascii()
    {
        Assert.Equal(
            "{\"a\":2,\"b\":1}",
            Canon("{\"b\":1,\"a\":2}"));
    }

    [Fact]
    public void Object_Keys_Sort_By_Code_Unit_Mixed_Case()
    {
        // 'A'(0x41) < 'a'(0x61) by UTF-16 code unit, so uppercase keys come first.
        Assert.Equal(
            "{\"A\":1,\"a\":2,\"b\":3}",
            Canon("{\"b\":3,\"a\":2,\"A\":1}"));
    }

    [Fact]
    public void Object_Keys_Sort_Is_Independent_At_Each_Nesting_Level()
    {
        Assert.Equal(
            "{\"a\":{\"x\":1,\"y\":2},\"b\":[3,2,1]}",
            Canon("{\"b\":[3,2,1],\"a\":{\"y\":2,\"x\":1}}"));
    }

    [Fact]
    public void Arrays_Preserve_Order()
    {
        // RFC 8785 §3.2.2.1: arrays keep insertion order.
        Assert.Equal("[3,1,2]", Canon("[3,1,2]"));
    }

    [Fact]
    public void Whitespace_Is_Stripped()
    {
        Assert.Equal(
            "{\"a\":1,\"b\":[1,2]}",
            Canon("  { \"b\" : [ 1 , 2 ] , \"a\" : 1 }  "));
    }

    [Fact]
    public void Integer_Numbers_Strip_To_Plain_Digits()
    {
        Assert.Equal("0", Canon("0"));
        Assert.Equal("1", Canon("1"));
        Assert.Equal("-1", Canon("-1"));
        Assert.Equal("9007199254740992", Canon("9007199254740992"));  // 2^53 — last exactly representable integer
    }

    [Fact]
    public void Negative_Zero_Normalises_To_Zero()
    {
        // RFC 8785 §3.2.2.3 references the ECMAScript ToString algorithm, which collapses -0 to "0".
        Assert.Equal("0", Canon("-0"));
    }

    [Fact]
    public void Integer_Literal_Above_2_To_53_Rounds_To_Nearest_Double()
    {
        // RFC 8785 §3.2.2.3 requires the JSON number be quantized to a double before
        // ECMA-262 ToString runs. 2^53 + 1 = 9007199254740993 has no exact double
        // representation; the nearest double is 2^53 = 9007199254740992.
        Assert.Equal("9007199254740992", Canon("9007199254740993"));
    }

    [Fact]
    public void Integer_Literal_And_Decimal_Form_Of_Same_Value_Canonicalise_Identically()
    {
        // Determinism guarantee: an encoder MUST produce identical bytes regardless of
        // how the source author wrote the literal. "9007199254740993" and
        // "9007199254740993.0" denote the same value (both round to 2^53 as a double).
        Assert.Equal(Canon("9007199254740993"), Canon("9007199254740993.0"));
    }

    [Fact]
    public void Large_Positive_Integer_Rounds_To_Nearest_Double_Per_Spec()
    {
        // ulong.MaxValue (2^64 - 1) is far beyond double precision; the nearest double
        // is 2^64 = 18446744073709551616, whose ECMA-262 canonical form is
        // "18446744073709552000" (shortest decimal that round-trips to 2^64).
        Assert.Equal("18446744073709552000", Canon("18446744073709551615"));
    }

    [Fact]
    public void String_With_Seven_Short_Escapes()
    {
        var node = JsonValue.Create("\"\\\b\f\n\r\t");
        Assert.Equal("\"\\\"\\\\\\b\\f\\n\\r\\t\"", Canon(node));
    }

    [Fact]
    public void String_With_Control_Bytes_Uses_LowercaseHex_u00XX()
    {
        // U+0001 → \u0001, U+001F → \u001f (lowercase hex per JCS §3.2.2.2).
        var node = JsonValue.Create("\u0001\u001F");
        Assert.Equal("\"\\u0001\\u001f\"", Canon(node));
    }

    [Fact]
    public void String_With_Solidus_Is_NOT_Escaped()
    {
        Assert.Equal("\"/path/to\"", Canon(JsonValue.Create("/path/to")));
    }

    [Fact]
    public void String_With_Characters_Above_U007F_Stays_Raw_Utf8()
    {
        // "é" is U+00E9, UTF-8 bytes C3 A9. JCS forbids \u00E9 escaping above U+007F.
        var bytes = JcsCanonicalizer.Canonicalize(JsonValue.Create("é"));
        Assert.Equal(new byte[] { 0x22, 0xC3, 0xA9, 0x22 }, bytes);
    }

    [Fact]
    public void String_With_Emoji_Surrogate_Pair_Becomes_4_Byte_Utf8()
    {
        // U+1F600 grinning face. UTF-8: F0 9F 98 80.
        var bytes = JcsCanonicalizer.Canonicalize(JsonValue.Create("\uD83D\uDE00"));
        Assert.Equal(new byte[] { 0x22, 0xF0, 0x9F, 0x98, 0x80, 0x22 }, bytes);
    }

    // --- Invalid UTF-16 / unpaired surrogates (issue #18): an unpaired surrogate has no UTF-8
    // representation. System.Text.Json would silently substitute U+FFFD, collapsing distinct
    // malformed inputs to identical bytes (a collision / signature-confusion vector). Every public
    // entry \u2014 string value or member name, JsonNode or JsonElement \u2014 must throw JcsFormatException
    // instead, while valid surrogate PAIRS and a legitimate U+FFFD pass through unchanged.

    [Fact]
    public void Unpaired_High_Surrogate_Throws()
    {
        // JsonNode (raw .NET string) path. Pre-fix this produced the silent bytes 22 EF BF BD 22
        // because SerializeToDocument rewrote U+D800 to U+FFFD upstream.
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonValue.Create("\uD800")));
    }

    [Fact]
    public void Unpaired_Low_Surrogate_Throws()
    {
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonValue.Create("\uDC00")));
    }

    [Fact]
    public void High_Surrogate_Followed_By_Non_Surrogate_Throws()
    {
        // A high surrogate must be followed by a low surrogate; a normal char after it is invalid.
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonValue.Create("\uD800x")));
    }

    [Fact]
    public void Unpaired_Surrogate_In_Object_Key_Throws()
    {
        // Raw JsonObject member name (the issue omits keys, but they are strings too \u2014 pre-fix this
        // silently produced {"<U+FFFD>":1}).
        var obj = new JsonObject { ["\uD800"] = 1 };
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(obj));
    }

    [Fact]
    public void Unpaired_Surrogate_Nested_In_Array_Throws()
    {
        var node = new JsonArray("ok", JsonValue.Create("\uDC00"));
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
    }

    [Fact]
    public void Unpaired_Surrogate_Via_JsonNode_Parse_Throws()
    {
        // Element-backed JsonValue (JsonNode.Parse). Pre-fix this threw a non-surrogate-specific
        // exception; it must be a JcsFormatException with the UTF-16 message.
        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonNode.Parse("\"\\uD800\"")));
        Assert.Contains("UTF-16", ex.Message);
    }

    [Fact]
    public void Unpaired_Surrogate_JsonElement_Value_Throws()
    {
        // JsonElement path: element.GetString() throws InvalidOperationException \u2014 must be converted.
        var ex = Assert.Throws<JcsFormatException>(() => CanonElement("\"\\uD800\""));
        Assert.Contains("UTF-16", ex.Message);
    }

    [Fact]
    public void Unpaired_Surrogate_JsonElement_Object_Key_Throws()
    {
        Assert.Throws<JcsFormatException>(() => CanonElement("{\"\\uD800\":1}"));
    }

    [Fact]
    public void Unpaired_Surrogate_IBufferWriter_Overload_Throws()
    {
        var sink = new ArrayBufferWriter<byte>();
        Assert.Throws<JcsFormatException>(
            () => JcsCanonicalizer.Canonicalize(JsonValue.Create("\uD800"), sink));
    }

    [Theory]
    [InlineData('\uD800')] // lone high surrogate
    [InlineData('\uDC00')] // lone low surrogate
    [InlineData('\uDFFF')]
    public void Unpaired_Surrogate_Char_Backed_JsonValue_Throws(char surrogate)
    {
        // A char-backed JsonValue serializes as a one-code-unit string; a single UTF-16 unit can
        // never be a valid pair, so any surrogate char is unpaired. Found by adversarial review:
        // pre-fix this bypassed the string-only check and silently produced 22 EF BF BD 22.
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonValue.Create(surrogate)));
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonValue.Create((char?)surrogate)));
        var obj = new JsonObject { ["v"] = JsonValue.Create(surrogate) };
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(obj));
    }

    [Fact]
    public void Valid_Char_Backed_JsonValue_Is_Unchanged()
    {
        // Non-surrogate chars must still canonicalize as their one-char string form.
        Assert.Equal(new byte[] { 0x22, 0x41, 0x22 }, JcsCanonicalizer.Canonicalize(JsonValue.Create('A')));
        Assert.Equal(new byte[] { 0x22, 0xC3, 0xA9, 0x22 }, JcsCanonicalizer.Canonicalize(JsonValue.Create('é')));
    }

    [Fact]
    public void Legitimate_Replacement_Char_U_FFFD_Still_Canonicalises()
    {
        // U+FFFD is a valid, assigned character (not a surrogate). It must NOT be rejected and must
        // serialize to its UTF-8 bytes EF BF BD \u2014 guarding against over-rejection.
        var expected = new byte[] { 0x22, 0xEF, 0xBF, 0xBD, 0x22 };
        Assert.Equal(expected, JcsCanonicalizer.Canonicalize(JsonValue.Create("\uFFFD")));
        using var doc = JsonDocument.Parse("\"\\uFFFD\"");
        Assert.Equal(expected, JcsCanonicalizer.Canonicalize(doc.RootElement));
    }

    [Fact]
    public void Valid_Surrogate_Pair_Via_JsonElement_Is_Unchanged()
    {
        // Regression for the JsonElement try/catch: a valid pair must still round-trip byte-for-byte.
        using var doc = JsonDocument.Parse("\"\\uD83D\\uDE00\"");
        Assert.Equal(
            new byte[] { 0x22, 0xF0, 0x9F, 0x98, 0x80, 0x22 },
            JcsCanonicalizer.Canonicalize(doc.RootElement));
    }

    [Fact]
    public void Audit_Entry_Example_Matches_Expected_Canonical_Form()
    {
        // Representative of net-wallet-mcp audit log entries (issue #9).
        var entry = new JsonObject
        {
            ["seq"] = 1,
            ["ts"] = "2026-05-21T18:00:00Z",
            ["op"] = "wallet.mint_identity",
            ["identity_id"] = "id_42",
            ["params_hash"] = "bafyparams",
            ["result_hash"] = "bafyresult",
            ["prev_cid"] = "genesis",
        };

        const string expected =
            "{\"identity_id\":\"id_42\",\"op\":\"wallet.mint_identity\","
            + "\"params_hash\":\"bafyparams\",\"prev_cid\":\"genesis\","
            + "\"result_hash\":\"bafyresult\",\"seq\":1,"
            + "\"ts\":\"2026-05-21T18:00:00Z\"}";

        Assert.Equal(expected, Canon(entry));
    }

    [Fact]
    public void Idempotence_Reparsing_Canonical_Form_Reproduces_Same_Bytes()
    {
        var node = JsonNode.Parse("{\"b\":[3,1,2],\"a\":{\"y\":\"é\",\"x\":1}}");
        var first = JcsCanonicalizer.Canonicalize(node);
        var second = JcsCanonicalizer.Canonicalize(JsonNode.Parse(Encoding.UTF8.GetString(first)));
        Assert.Equal(first, second);
    }

    [Fact]
    public void JsonElement_Overload_Matches_JsonNode_Overload()
    {
        const string raw = "{\"b\":2,\"a\":{\"y\":2,\"x\":1}}";
        var fromNode = JcsCanonicalizer.Canonicalize(JsonNode.Parse(raw));
        var fromElement = Encoding.UTF8.GetBytes(CanonElement(raw));
        Assert.Equal(fromNode, fromElement);
    }

    [Fact]
    public void IBufferWriter_Overload_Writes_Same_Bytes_As_Array_Overload()
    {
        var node = JsonNode.Parse("{\"b\":2,\"a\":1}");
        var expected = JcsCanonicalizer.Canonicalize(node);

        var writer = new ArrayBufferWriter<byte>();
        JcsCanonicalizer.Canonicalize(node, writer);

        Assert.Equal(expected, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void IBufferWriter_Overload_Rejects_Null_Destination()
    {
        Assert.Throws<ArgumentNullException>(
            () => JcsCanonicalizer.Canonicalize((JsonNode?)null, null!));
    }

    [Fact]
    public void NaN_Throws_JcsFormatException()
    {
        var node = JsonValue.Create(double.NaN);
        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
        Assert.Contains("NaN", ex.Message);
    }

    [Fact]
    public void Positive_Infinity_Throws_JcsFormatException()
    {
        var node = JsonValue.Create(double.PositiveInfinity);
        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
        Assert.Contains("infinity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Negative_Infinity_Throws_JcsFormatException()
    {
        var node = JsonValue.Create(double.NegativeInfinity);
        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
        Assert.Contains("infinity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<double> UnrepresentableDoubles => new()
    {
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
    };

    [Theory]
    [MemberData(nameof(UnrepresentableDoubles))]
    public void WrappedClrObject_NaN_Infinity_Throws_JcsFormatException(double value)
    {
        // ValidateNode cannot see inside a JsonValue wrapping a raw CLR object, so the NaN/∞
        // surfaces from JsonSerializer.SerializeToDocument as an ArgumentException; it must be
        // translated to JcsFormatException like every other unrepresentable-number path —
        // the XML docs promise JcsFormatException unconditionally. Fails closed either way;
        // only the exception type was wrong. See issue #47.
        JsonNode node = JsonValue.Create(new Dictionary<string, object?> { ["amount"] = value })!;

        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
        Assert.Contains("NaN or infinity", ex.Message);
    }

    [Theory]
    [MemberData(nameof(UnrepresentableDoubles))]
    public void WrappedClrObject_NaN_Infinity_Throws_JcsFormatException_OnWriterOverload(double value)
    {
        JsonNode node = JsonValue.Create(new Dictionary<string, object?> { ["amount"] = value })!;
        var writer = new ArrayBufferWriter<byte>();

        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node, writer));
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void WrappedClrObject_NaN_Throws_JcsFormatException_FromCidFromCanonicalJson()
    {
        JsonNode node = JsonValue.Create(new Dictionary<string, object?> { ["amount"] = double.NaN })!;

        Assert.Throws<JcsFormatException>(() => Cid.FromCanonicalJson(node));
    }

    [Fact]
    public void WrappedClrObject_NestedNaN_Throws_JcsFormatException()
    {
        // The NaN can hide arbitrarily deep in the wrapped object graph; SerializeToDocument
        // still trips on it, and the translation must hold. See issue #47.
        JsonNode node = JsonValue.Create(new Dictionary<string, object?>
        {
            ["outer"] = new object?[] { 1.0, new Dictionary<string, object?> { ["inner"] = float.NaN } },
        })!;

        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node));
    }

    [Fact]
    public void WrappedClrObject_WithoutNaN_StillCanonicalizes()
    {
        // The widened catch must not over-reject: a clean wrapped CLR object keeps working.
        JsonNode node = JsonValue.Create(new Dictionary<string, object?> { ["amount"] = 1.5 })!;

        var bytes = JcsCanonicalizer.Canonicalize(node);

        Assert.Equal("{\"amount\":1.5}", Encoding.UTF8.GetString(bytes));
    }

    // RFC 8785 §3.2.2.3 vector table — every row in issue #13's vector table must round-trip.
    [Theory]
    [InlineData("0", "0")]
    [InlineData("-0", "0")]
    [InlineData("1", "1")]
    [InlineData("-1", "-1")]
    [InlineData("1.0", "1")]
    [InlineData("1.5", "1.5")]
    [InlineData("0.1", "0.1")]
    [InlineData("100", "100")]
    [InlineData("100000000000000000000", "100000000000000000000")]
    [InlineData("1e21", "1e+21")]
    [InlineData("1e-6", "0.000001")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("5e-324", "5e-324")]
    [InlineData("1.7976931348623157e308", "1.7976931348623157e+308")]
    [InlineData("9007199254740992", "9007199254740992")]
    [InlineData("333333333.3333332897", "333333333.3333333")]
    public void Number_Vector_Table_From_Issue_13(string input, string expected)
    {
        Assert.Equal(expected, Canon(input));
    }

    [Fact]
    public void LargeIntegerLiteral_Overflowing_Double_Throws_Infinity()
    {
        // A 400-digit literal cannot fit in any IEEE-754 double — parses to +∞.
        var literal = new string('9', 400);
        var ex = Assert.Throws<JcsFormatException>(() => Canon(literal));
        Assert.Contains("infinity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LargeIntegerLiteral_Within_Double_Range_Canonicalises_As_IEEE754()
    {
        // Issue #13 behaviour change: literals beyond ulong.MaxValue but finite as a
        // double now canonicalise per ECMA-262 instead of throwing "outside range".
        Assert.Equal("1e+21", Canon("1000000000000000000000"));
    }

    [Fact]
    public void Precision_Loss_Per_Spec()
    {
        // 9007199254740993 = 2^53 + 1 cannot be represented exactly as a double; written
        // with a decimal point, JSON forces the double path and we canonicalise the
        // actually-stored double (2^53 = 9007199254740992).
        Assert.Equal("9007199254740992", Canon("9007199254740993.0"));
    }

    [Fact]
    public void JsonValue_NegativeZero_Double_Normalises_To_Zero()
    {
        // ECMA-262 step 1 collapses both signed zeros to "0" via the IEEE 754 -0.0 == 0.0
        // identity, regardless of whether the node was built from a JSON literal or a
        // CLR -0.0.
        Assert.Equal("0", Canon(JsonValue.Create(-0.0)));
        Assert.Equal("0", Canon("-0.0"));
    }

    [Fact]
    public void Canonicalisation_Is_Culture_Independent()
    {
        // de-DE uses ',' as the decimal separator; any culture leak would emit "0,1".
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("0.1", Canon("0.1"));
            Assert.Equal("1.5", Canon("1.5"));
            Assert.Equal("1e+21", Canon("1e21"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Empty_Object_And_Empty_Array_Canonicalise()
    {
        Assert.Equal("{}", Canon("{}"));
        Assert.Equal("[]", Canon("[]"));
    }

    [Fact]
    public void Empty_String_Canonicalises()
    {
        Assert.Equal("\"\"", Canon(JsonValue.Create("")));
    }

    [Fact]
    public void Large_String_Above_Stack_Threshold_Uses_Pool_And_Still_Canonicalises()
    {
        // Force the ArrayPool path (string > 256 bytes UTF-8).
        var big = new string('a', 1024);
        var expected = "\"" + big + "\"";
        Assert.Equal(expected, Canon(JsonValue.Create(big)));
    }

    // --- Recursion-depth limit (issue #16): deep input must throw JcsFormatException,
    // never an uncatchable StackOverflowException. The internal limit is 64 (matches
    // System.Text.Json's default MaxDepth); it is private, so the literal is used here.

    // Wrap a value in `depth` nested single-element arrays. Built programmatically so the
    // test exercises the canonicalizer's own guards directly, independent of any parse-time
    // MaxDepth behaviour. The innermost value then sits at nesting depth `depth`.
    private static JsonNode NestArrays(int depth)
    {
        JsonNode node = JsonValue.Create(1);
        for (var i = 0; i < depth; i++)
        {
            node = new JsonArray { node };
        }

        return node;
    }

    [Fact]
    public void JsonNode_At_Depth_Limit_Canonicalises()
    {
        var expected = new string('[', 64) + "1" + new string(']', 64);
        Assert.Equal(expected, Canon(NestArrays(64)));
    }

    [Fact]
    public void JsonNode_Just_Over_Limit_Throws_JcsFormatException()
    {
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(NestArrays(65)));
    }

    [Fact]
    public void JsonNode_Far_Over_Limit_Throws_Without_StackOverflow()
    {
        // 100_000 deep: if the guard worked only after recursing the whole tree this would
        // crash the test host. It must throw after ~65 frames instead.
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(NestArrays(100_000)));
    }

    [Fact]
    public void JsonElement_At_Depth_Limit_Canonicalises()
    {
        var atLimit = new string('[', 64) + "1" + new string(']', 64);
        using var doc = JsonDocument.Parse(atLimit, new JsonDocumentOptions { MaxDepth = 256 });
        Assert.Equal(atLimit, Encoding.UTF8.GetString(JcsCanonicalizer.Canonicalize(doc.RootElement)));
    }

    [Fact]
    public void JsonElement_Over_Limit_Throws_JcsFormatException()
    {
        // Just-over-limit boundary: verifies the depth-64 policy gate on the JsonElement path.
        // (Depth 70 would NOT overflow the stack on its own — see the far-over test below for
        // the actual no-crash proof.) Parse with headroom so the document itself builds; our
        // guard (limit 64) is what rejects it.
        var tooDeep = new string('[', 70) + "1" + new string(']', 70);
        using var doc = JsonDocument.Parse(tooDeep, new JsonDocumentOptions { MaxDepth = 256 });
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(doc.RootElement));
    }

    [Fact]
    public void JsonElement_Far_Over_Limit_Throws_Without_StackOverflow()
    {
        // The JsonElement path (WriteElement -> WriteArray -> WriteElement) has no
        // SerializeToDocument boundary and no parse-time guard, so before the fix it recursed
        // unbounded and overflowed the stack in the low thousands of frames. 100_000 deep is
        // well past that threshold: the depth-64 guard must throw JcsFormatException after ~65
        // frames instead of terminating the process with an uncatchable StackOverflowException.
        // Parse with no practical depth limit so the document builds and our guard is the gate.
        var tooDeep = new string('[', 100_000) + "1" + new string(']', 100_000);
        using var doc = JsonDocument.Parse(tooDeep, new JsonDocumentOptions { MaxDepth = int.MaxValue });
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(doc.RootElement));
    }

    // --- Canonical-output-byte cap (issue #16, optional guard): bound the size of the
    // produced output as defense-in-depth on untrusted JSON. "[1,2,3,4,5]" canonicalizes to
    // exactly 11 bytes, which makes the byte boundaries easy to assert against an explicit limit.

    [Fact]
    public void Default_Output_Limit_Is_One_MiB()
    {
        Assert.Equal(1_048_576, JcsCanonicalizer.DefaultMaxOutputByteLength);
    }

    [Fact]
    public void Output_At_Explicit_Limit_Canonicalises()
    {
        var node = JsonNode.Parse("[1,2,3,4,5]"); // 11 canonical bytes
        Assert.Equal("[1,2,3,4,5]", Encoding.UTF8.GetString(JcsCanonicalizer.Canonicalize(node, maxOutputBytes: 11)));
    }

    [Fact]
    public void Output_One_Byte_Over_Limit_Throws_JcsFormatException()
    {
        var node = JsonNode.Parse("[1,2,3,4,5]"); // needs 11 bytes; allow only 10
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node, maxOutputBytes: 10));
    }

    [Fact]
    public void JsonElement_Output_Over_Limit_Throws_JcsFormatException()
    {
        using var doc = JsonDocument.Parse("[1,2,3,4,5]");
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(doc.RootElement, maxOutputBytes: 5));
    }

    [Fact]
    public void IBufferWriter_Output_Over_Limit_Throws_Without_Exceeding_Limit()
    {
        var node = JsonNode.Parse("[1,2,3,4,5]");
        var sink = new ArrayBufferWriter<byte>();
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(node, sink, maxOutputBytes: 5));
        // The crossing byte is rejected before it is committed, so the caller's writer never
        // receives more than the limit.
        Assert.True(sink.WrittenCount <= 5, $"committed {sink.WrittenCount} bytes, expected <= 5");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Output_Limit_Below_One_Throws_ArgumentOutOfRange(int limit)
    {
        var node = JsonNode.Parse("1");
        Assert.Throws<ArgumentOutOfRangeException>(() => JcsCanonicalizer.Canonicalize(node, limit));
        using var doc = JsonDocument.Parse("1");
        Assert.Throws<ArgumentOutOfRangeException>(() => JcsCanonicalizer.Canonicalize(doc.RootElement, limit));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => JcsCanonicalizer.Canonicalize(node, new ArrayBufferWriter<byte>(), limit));
    }

    // A flat (depth-1) array whose canonical form exceeds the 1 MiB default cap, so ONLY the
    // output cap — never the depth guard — can reject it. 200_000 * "123456" ≈ 1.4 MiB.
    private static string OverDefaultCapArrayJson()
    {
        var sb = new StringBuilder(1_500_000);
        sb.Append('[');
        for (var i = 0; i < 200_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("123456");
        }

        sb.Append(']');
        return sb.ToString();
    }

    [Fact]
    public void Default_Cap_Triggers_On_Output_Over_One_MiB()
    {
        // The default-ON cap is the shipped breaking change, so pin it directly: a regression
        // that delegated with int.MaxValue (disabling the default) would otherwise pass the suite.
        var json = OverDefaultCapArrayJson();

        var ex = Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonNode.Parse(json)));
        Assert.Contains(
            JcsCanonicalizer.DefaultMaxOutputByteLength.ToString(CultureInfo.InvariantCulture), ex.Message);

        using var doc = JsonDocument.Parse(json);
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(doc.RootElement));

        var sink = new ArrayBufferWriter<byte>();
        Assert.Throws<JcsFormatException>(() => JcsCanonicalizer.Canonicalize(JsonNode.Parse(json), sink));
        Assert.True(sink.WrittenCount <= JcsCanonicalizer.DefaultMaxOutputByteLength);
    }

    [Fact]
    public void Output_Above_Default_Succeeds_When_Limit_Raised()
    {
        // The documented escape hatch: an explicit raised cap lets known-safe large output
        // through. This also exercises the overflow-safe `count > maxBytes - _written`
        // arithmetic in LimitedBufferWriter with a large _written and maxBytes = int.MaxValue.
        var node = JsonNode.Parse(OverDefaultCapArrayJson());
        var bytes = JcsCanonicalizer.Canonicalize(node, maxOutputBytes: int.MaxValue);

        Assert.True(bytes.Length > JcsCanonicalizer.DefaultMaxOutputByteLength);
        Assert.Equal((byte)'[', bytes[0]);
        Assert.Equal((byte)']', bytes[^1]);
    }

    // --- Duplicate object member names (issue #17): RFC 8785 builds on I-JSON (RFC 7493 §2.3),
    // which forbids duplicate names. JsonDocument preserves them, so both public surfaces must
    // reject them with JcsFormatException rather than emit ambiguous, non-canonical output.

    [Fact]
    public void Duplicate_Keys_Throw_JcsFormatException()
    {
        // JsonElement overload: JsonDocument.Parse keeps both "a" members.
        var ex = Assert.Throws<JcsFormatException>(() => CanonElement("{\"a\":1,\"a\":2}"));
        Assert.Contains("Duplicate object member name 'a'", ex.Message);
    }

    [Fact]
    public void Nested_Duplicate_Keys_Throw_JcsFormatException()
    {
        var ex = Assert.Throws<JcsFormatException>(() => CanonElement("{\"x\":{\"a\":1,\"a\":2}}"));
        Assert.Contains("Duplicate object member name 'a'", ex.Message);
    }

    [Fact]
    public void Duplicate_Keys_In_Array_Element_Throw_JcsFormatException()
    {
        // Exercises the WriteArray -> WriteElement -> WriteObject recursion path.
        Assert.Throws<JcsFormatException>(() => CanonElement("[{\"a\":1,\"a\":2}]"));
    }

    [Fact]
    public void Triple_Duplicate_Keys_Throw_JcsFormatException()
    {
        // Three identical names: confirms the adjacent-pair scan handles runs longer than two.
        Assert.Throws<JcsFormatException>(() => CanonElement("{\"a\":1,\"a\":2,\"a\":3}"));
    }

    [Fact]
    public void Distinct_Keys_With_Shared_Prefix_Do_Not_Throw()
    {
        // Guards against an over-eager check: "a" and "ab" sort adjacently but are not equal.
        Assert.Equal("{\"a\":1,\"ab\":2}", CanonElement("{\"ab\":2,\"a\":1}"));
        Assert.Equal("{\"a\":1,\"ab\":2}", Canon("{\"ab\":2,\"a\":1}"));
    }

    [Fact]
    public void JsonNode_Overload_Duplicate_Keys_Throw_JcsFormatException()
    {
        // JsonNode.Parse is lazy; the pre-walk enumerates the object, which makes the backing
        // JsonObject throw ArgumentException on duplicates. Rather than translate that, the overload
        // falls through to WriteObject, so the message names the offending key just like the
        // JsonElement path.
        var ex = Assert.Throws<JcsFormatException>(() => Canon("{\"a\":1,\"a\":2}"));
        Assert.Contains("Duplicate object member name 'a'", ex.Message);
    }

    [Fact]
    public void Both_Overloads_Report_Identical_Keyed_Duplicate_Message()
    {
        // WriteObject is the single source of the duplicate message, so the two surfaces agree
        // byte-for-byte (diagnostic consistency — PR #38 review).
        var fromElement = Assert.Throws<JcsFormatException>(() => CanonElement("{\"a\":1,\"a\":2}"));
        var fromNode = Assert.Throws<JcsFormatException>(() => Canon("{\"a\":1,\"a\":2}"));
        Assert.Equal(fromElement.Message, fromNode.Message);
    }

    [Fact]
    public void JsonNode_Overload_Nested_Duplicate_Keys_Throw_JcsFormatException()
    {
        Assert.Throws<JcsFormatException>(() => Canon("{\"x\":{\"a\":1,\"a\":2}}"));
    }

    [Fact]
    public void IBufferWriter_Overload_Duplicate_Keys_Throw_JcsFormatException()
    {
        var sink = new ArrayBufferWriter<byte>();
        Assert.Throws<JcsFormatException>(
            () => JcsCanonicalizer.Canonicalize(JsonNode.Parse("{\"a\":1,\"a\":2}"), sink));
    }
}
