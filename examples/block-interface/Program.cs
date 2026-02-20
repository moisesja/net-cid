using System.Text.Json;
using NetCid;

var value = new Dictionary<string, string> { ["hello"] = "world" };

var block = SimpleBlock.Encode(value, Multicodec.DagJson);
Console.WriteLine($"Example block CID: {block.Cid}");

var block2 = SimpleBlock.Decode<Dictionary<string, string>>(block.Bytes, Multicodec.DagJson);
Console.WriteLine($"Example block CID equal to decoded binary block: {block.Cid == block2.Cid}");

var block3 = SimpleBlock.Create<Dictionary<string, string>>(block.Bytes, block.Cid, Multicodec.DagJson);
Console.WriteLine($"Example block CID equal to block created from CID + bytes: {block.Cid == block3.Cid}");

internal sealed record EncodedBlock<T>(T Value, byte[] Bytes, Cid Cid);

internal static class SimpleBlock
{
    public static EncodedBlock<T> Encode<T>(T value, ulong codec)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var cid = Cid.FromContent(bytes, codec, MultihashCode.Sha2_256);
        return new EncodedBlock<T>(value, bytes, cid);
    }

    public static EncodedBlock<T> Decode<T>(byte[] bytes, ulong codec)
    {
        var value = JsonSerializer.Deserialize<T>(bytes)
            ?? throw new InvalidOperationException("Decoded block value was null.");

        var cid = Cid.FromContent(bytes, codec, MultihashCode.Sha2_256);
        return new EncodedBlock<T>(value, bytes, cid);
    }

    public static EncodedBlock<T> Create<T>(byte[] bytes, Cid expectedCid, ulong codec)
    {
        var block = Decode<T>(bytes, codec);
        if (block.Cid != expectedCid)
        {
            throw new InvalidOperationException("CID verification failed for the provided block bytes.");
        }

        return new EncodedBlock<T>(block.Value, block.Bytes, expectedCid);
    }
}
