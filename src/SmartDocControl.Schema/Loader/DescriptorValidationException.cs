namespace SmartDocControl.Schema.Loader;

public sealed class DescriptorValidationException : Exception
{
    public string DescriptorName { get; }

    public DescriptorValidationException(string descriptorName, string message)
        : base($"[{descriptorName}] {message}")
    {
        DescriptorName = descriptorName;
    }
}
