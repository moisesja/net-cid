namespace NetCid;

/// <summary>
/// Represents parsing and decoding errors for CID inputs.
/// </summary>
public sealed class CidFormatException : FormatException
{
    public CidFormatException(string message)
        : base(message)
    {
    }

    public CidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
