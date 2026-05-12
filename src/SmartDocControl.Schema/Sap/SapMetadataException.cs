namespace SmartDocControl.Schema.Sap;

public sealed class SapMetadataException : Exception
{
    public string ObjectName { get; }
    public string? ErrorCode { get; }
    public int? HttpStatus { get; }

    public SapMetadataException(string objectName, int? httpStatus, string? errorCode, string message)
        : base(message)
    {
        ObjectName = objectName;
        HttpStatus = httpStatus;
        ErrorCode = errorCode;
    }
}
