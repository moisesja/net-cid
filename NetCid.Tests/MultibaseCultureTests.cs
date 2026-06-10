using System.Globalization;

namespace NetCid.Tests;

/// <summary>
/// Pins culture independence of the multibase parse/encode paths (#44). SimpleBase builds its
/// case-insensitive base32/base36 alphabets with culture-sensitive char.ToUpper, so under a
/// dotless-i culture (tr-TR, az-Latn-AZ) the first alphabet touch used to throw
/// IndexOutOfRangeException out of Try* and then rethrow from the faulted Lazy forever.
/// Multibase now warms every SimpleBase alphabet it uses under the invariant culture in its
/// static constructor and normalizes residual decoder exceptions to CidFormatException.
/// In-suite, the warm-up has usually run before these tests; the true first-touch-under-tr-TR
/// scenario is process-global and is additionally covered by the invariant warm-up itself
/// (the static constructor pins the culture it warms under, regardless of ambient culture).
/// </summary>
public sealed class MultibaseCultureTests
{
    private const string KnownCidBase32 = "bafkreidon73zkcrwdb5iafqtijxildoonbwnpv7dyd6ef3qdgads2jc4su";
    private const string KnownCidBytesHex = "015512206e6ff7950a36187a801613426e858dce686cd7d7e3c0fc42ee0330072d245c95";
    private const string KnownCidBase36Lower = "k2cwue9rqdypmt3thjky14z1tk9fi9f0o5w7b3ofitdewlcf87lismqs";
    private const string KnownCidBase36Upper = "K2CWUE9RQDYPMT3THJKY14Z1TK9FI9F0O5W7B3OFITDEWLCF87LISMQS";

    private static void RunWithCulture(string cultureName, Action assertions)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            assertions();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void TryDecode_ReturnsTrueForValidInputUnderDotlessICulture(string cultureName)
        => RunWithCulture(cultureName, () =>
        {
            Assert.True(Multibase.TryDecode(KnownCidBase32, out var bytes, out var encoding));
            Assert.Equal(MultibaseEncoding.Base32Lower, encoding);
            Assert.Equal(KnownCidBytesHex, Convert.ToHexString(bytes).ToLowerInvariant());

            Assert.True(Cid.TryParse(KnownCidBase32, out var cid));
            Assert.Equal(KnownCidBase32, cid!.ToString());
        });

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void Decode_Base36BothCasesWorkUnderDotlessICulture(string cultureName)
        => RunWithCulture(cultureName, () =>
        {
            var lower = Multibase.Decode(KnownCidBase36Lower, out var lowerEncoding);
            Assert.Equal(MultibaseEncoding.Base36Lower, lowerEncoding);

            var upper = Multibase.Decode(KnownCidBase36Upper, out var upperEncoding);
            Assert.Equal(MultibaseEncoding.Base36Upper, upperEncoding);

            Assert.Equal(lower, upper);
        });

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void Decode_UppercaseBase32WithLetterIWorksUnderDotlessICulture(string cultureName)
        => RunWithCulture(cultureName, () =>
        {
            // The decode path lowercases an uppercase base32 payload before handing it to
            // SimpleBase. Culture-sensitive folding would turn 'I' into dotless 'ı' (U+0131)
            // under tr-TR and the payload would be rejected; the invariant fold must keep
            // 'I' → 'i'. The known vector contains several 'I'/'i' symbols.
            var upperForm = "B" + KnownCidBase32[1..].ToUpperInvariant();
            Assert.Contains('I', upperForm);

            var decoded = Multibase.Decode(upperForm, out var encoding);

            Assert.Equal(MultibaseEncoding.Base32Upper, encoding);
            Assert.Equal(KnownCidBytesHex, Convert.ToHexString(decoded).ToLowerInvariant());
        });

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void EncodeDecode_RoundTripsUnderDotlessICulture(string cultureName)
        => RunWithCulture(cultureName, () =>
        {
            var payload = new byte[] { 0x01, 0x55, 0x12, 0x20, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC };
            foreach (var encoding in new[]
            {
                MultibaseEncoding.Base32Lower,
                MultibaseEncoding.Base32Upper,
                MultibaseEncoding.Base36Lower,
                MultibaseEncoding.Base36Upper,
            })
            {
                var encoded = Multibase.Encode(payload, encoding, includePrefix: true);
                var decoded = Multibase.Decode(encoded, out var detected);

                Assert.Equal(encoding, detected);
                Assert.Equal(payload, decoded);
            }
        });

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void TryDecode_RejectsInvalidInputWithoutThrowingUnderDotlessICulture(string cultureName)
        => RunWithCulture(cultureName, () =>
        {
            // Hostile inputs must reject cleanly (false / CidFormatException), never leak a
            // SimpleBase exception, under any ambient culture.
            Assert.False(Multibase.TryDecode("babc!", out _, out _));
            Assert.False(Multibase.TryDecode("k2cwue9rqdypmt3thj\u212Ay14z1tk9fi9f0o5w7b3ofitdewlcf87lismqs", out _, out _));
            Assert.False(Cid.TryParse("bafkrei\u0131nvalid", out _));
            Assert.Throws<CidFormatException>(() => Multibase.Decode("babc!"));
        });
}
