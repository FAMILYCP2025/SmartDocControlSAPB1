namespace SmartDocControl.Runner;

internal sealed class CliOptions
{
    public string Environment { get; init; } = string.Empty;
    public bool ValidateOnly { get; init; }
    public bool ShowHelp { get; init; }
}

internal sealed class CliParseResult
{
    public CliOptions? Options { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Error is null && Options is not null;

    public static CliParseResult Parse(string[] args)
    {
        string? environment = null;
        bool validateOnly = false;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--validate-only":
                case "--dry-run":
                    validateOnly = true;
                    break;

                case "--environment":
                case "-e":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                        return Fail("Missing value for --environment.");
                    environment = args[++i];
                    break;

                default:
                    return Fail($"Unknown argument: '{args[i]}'.");
            }
        }

        if (showHelp)
            return new CliParseResult
            {
                Options = new CliOptions
                {
                    Environment = environment ?? string.Empty,
                    ValidateOnly = validateOnly,
                    ShowHelp = true
                }
            };

        if (string.IsNullOrWhiteSpace(environment))
            return Fail("Missing required argument: --environment.");

        return new CliParseResult
        {
            Options = new CliOptions
            {
                Environment = environment,
                ValidateOnly = validateOnly
            }
        };
    }

    private static CliParseResult Fail(string error) => new() { Error = error };
}
