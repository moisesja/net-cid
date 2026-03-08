using System.Collections.ObjectModel;

namespace NetCid;

/// <summary>
/// Common multicodec identifiers used with CIDs.
/// </summary>
public static class Multicodec
{
    public const ulong Raw = 0x55;
    public const ulong DagPb = 0x70;
    public const ulong DagCbor = 0x71;
    public const ulong Libp2pKey = 0x72;
    public const ulong GitRaw = 0x78;
    public const ulong TorrentInfo = 0x7B;
    public const ulong TorrentFile = 0x7C;
    public const ulong LeofcoinBlock = 0x81;
    public const ulong LeofcoinTx = 0x82;
    public const ulong EthBlock = 0x90;
    public const ulong EthBlockList = 0x91;
    public const ulong EthTxTrie = 0x92;
    public const ulong EthTx = 0x93;
    public const ulong EthTxReceiptTrie = 0x94;
    public const ulong EthTxReceipt = 0x95;
    public const ulong EthStateTrie = 0x96;
    public const ulong EthAccountSnapshot = 0x97;
    public const ulong EthStorageTrie = 0x98;
    public const ulong BitcoinBlock = 0xB0;
    public const ulong BitcoinTx = 0xB1;
    public const ulong ZcashBlock = 0xC0;
    public const ulong ZcashTx = 0xC1;
    public const ulong DagJson = 0x0129;

    // Key-type codecs (used by DID methods, Verifiable Credentials, etc.)
    public const ulong Secp256k1Pub = 0xE7;
    public const ulong Bls12381G1Pub = 0xEA;
    public const ulong Bls12381G2Pub = 0xEB;
    public const ulong X25519Pub = 0xEC;
    public const ulong Ed25519Pub = 0xED;
    public const ulong P256Pub = 0x1200;
    public const ulong P384Pub = 0x1201;

    private static readonly IReadOnlyDictionary<ulong, string> NamesByCode = new ReadOnlyDictionary<ulong, string>(
        new Dictionary<ulong, string>
        {
            [Raw] = "raw",
            [DagPb] = "dag-pb",
            [DagCbor] = "dag-cbor",
            [Libp2pKey] = "libp2p-key",
            [GitRaw] = "git-raw",
            [TorrentInfo] = "torrent-info",
            [TorrentFile] = "torrent-file",
            [LeofcoinBlock] = "leofcoin-block",
            [LeofcoinTx] = "leofcoin-tx",
            [EthBlock] = "eth-block",
            [EthBlockList] = "eth-block-list",
            [EthTxTrie] = "eth-tx-trie",
            [EthTx] = "eth-tx",
            [EthTxReceiptTrie] = "eth-tx-receipt-trie",
            [EthTxReceipt] = "eth-tx-receipt",
            [EthStateTrie] = "eth-state-trie",
            [EthAccountSnapshot] = "eth-account-snapshot",
            [EthStorageTrie] = "eth-storage-trie",
            [BitcoinBlock] = "bitcoin-block",
            [BitcoinTx] = "bitcoin-tx",
            [ZcashBlock] = "zcash-block",
            [ZcashTx] = "zcash-tx",
            [DagJson] = "dag-json",
            [Secp256k1Pub] = "secp256k1-pub",
            [Bls12381G1Pub] = "bls12_381-g1-pub",
            [Bls12381G2Pub] = "bls12_381-g2-pub",
            [X25519Pub] = "x25519-pub",
            [Ed25519Pub] = "ed25519-pub",
            [P256Pub] = "p256-pub",
            [P384Pub] = "p384-pub"
        });

    private static readonly IReadOnlyDictionary<string, ulong> CodesByName = new ReadOnlyDictionary<string, ulong>(
        NamesByCode.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal));

    public static IEnumerable<KeyValuePair<ulong, string>> Entries => NamesByCode;

    public static bool TryGetName(ulong code, out string? name) => NamesByCode.TryGetValue(code, out name);

    public static bool TryGetCode(string name, out ulong code) => CodesByName.TryGetValue(name, out code);

    /// <summary>
    /// Prefix raw bytes with the varint-encoded multicodec tag.
    /// </summary>
    public static byte[] Prefix(ulong codec, ReadOnlySpan<byte> rawBytes)
    {
        var prefixLength = Varint.GetEncodedLength(codec);
        var result = new byte[checked(prefixLength + rawBytes.Length)];
        Varint.Write(codec, result);
        rawBytes.CopyTo(result.AsSpan(prefixLength));
        return result;
    }

    /// <summary>
    /// Decode a multicodec-prefixed byte buffer, returning the codec and raw bytes.
    /// </summary>
    public static (ulong Codec, byte[] RawBytes) Decode(ReadOnlySpan<byte> prefixedBytes)
    {
        if (!TryDecode(prefixedBytes, out var codec, out var rawBytes))
        {
            throw new CidFormatException("Invalid multicodec-prefixed data.");
        }

        return (codec, rawBytes!);
    }

    /// <summary>
    /// Try to decode a multicodec-prefixed byte buffer.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> prefixedBytes, out ulong codec, out byte[]? rawBytes)
    {
        codec = 0;
        rawBytes = null;

        if (!Varint.TryDecode(prefixedBytes, out codec, out var bytesRead))
        {
            return false;
        }

        rawBytes = prefixedBytes.Slice(bytesRead).ToArray();
        return true;
    }
}
