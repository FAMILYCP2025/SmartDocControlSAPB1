namespace SmartDocControl.Schema.Sap;

public sealed class SapObjectAlreadyExistsException : Exception
{
    public string ObjectName { get; }
    public string? ErrorCode { get; }

    public SapObjectAlreadyExistsException(string objectName, string? errorCode, string message)
        : base(message)
    {
        ObjectName = objectName;
        ErrorCode = errorCode;
    }
}
