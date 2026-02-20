namespace NetCid;

/// <summary>
/// Common multihash function codes.
/// </summary>
public static class MultihashCode
{
    public const ulong Identity = 0x00;
    public const ulong Sha1 = 0x11;
    public const ulong Sha2_256 = 0x12;
    public const ulong Sha2_512 = 0x13;
    public const ulong Sha3_512 = 0x14;
    public const ulong Sha3_384 = 0x15;
    public const ulong Sha3_256 = 0x16;
    public const ulong Sha3_224 = 0x17;
    public const ulong Shake128 = 0x18;
    public const ulong Shake256 = 0x19;
    public const ulong Keccak224 = 0x1A;
    public const ulong Keccak256 = 0x1B;
    public const ulong Keccak384 = 0x1C;
    public const ulong Keccak512 = 0x1D;
    public const ulong Blake3 = 0x1E;
}
