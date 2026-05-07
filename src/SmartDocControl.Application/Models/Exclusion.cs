namespace SmartDocControl.Application.Models;

public sealed class Exclusion
{
    public string ExclusionType { get; }
    public string ExclusionValue { get; }
    public string? ObjType { get; }
    public string? Reason { get; }

    public Exclusion(
        string exclusionType,
        string exclusionValue,
        string? objType = null,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(exclusionType))
            throw new ArgumentException("ExclusionType is required.", nameof(exclusionType));
        if (string.IsNullOrWhiteSpace(exclusionValue))
            throw new ArgumentException("ExclusionValue is required.", nameof(exclusionValue));

        ExclusionType = exclusionType;
        ExclusionValue = exclusionValue;
        ObjType = objType;
        Reason = reason;
    }
}
