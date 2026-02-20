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
            [DagJson] = "dag-json"
        });

    private static readonly IReadOnlyDictionary<string, ulong> CodesByName = new ReadOnlyDictionary<string, ulong>(
        NamesByCode.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal));

    public static IEnumerable<KeyValuePair<ulong, string>> Entries => NamesByCode;

    public static bool TryGetName(ulong code, out string? name) => NamesByCode.TryGetValue(code, out name);

    public static bool TryGetCode(string name, out ulong code) => CodesByName.TryGetValue(name, out code);
}
