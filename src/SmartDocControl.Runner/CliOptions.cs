namespace SmartDocControl.Runner;

internal sealed class CliOptions
{
    public string Environment { get; init; } = string.Empty;
    public bool ValidateOnly { get; init; }
    public bool InstallSchema { get; init; }
    public bool DryRun { get; init; }
    public bool Force { get; init; }
    public bool TraceMetadata { get; init; }
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
        bool installSchema = false;
        bool dryRun = false;
        bool force = false;
        bool traceMetadata = false;
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
                    validateOnly = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--install-schema":
                    installSchema = true;
                    break;

                case "--force":
                    force = true;
                    break;

                case "--trace-metadata":
                    traceMetadata = true;
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
                    InstallSchema = installSchema,
                    DryRun = dryRun,
                    Force = force,
                    TraceMetadata = traceMetadata,
                    ShowHelp = true
                }
            };

        // Conflicting modes
        if (validateOnly && installSchema)
            return Fail("Conflicting modes: --validate-only and --install-schema cannot be combined.");

        // Backward compat: --dry-run alone (no --install-schema) means --validate-only
        if (dryRun && !installSchema && !validateOnly)
            validateOnly = true;

        if (string.IsNullOrWhiteSpace(environment))
            return Fail("Missing required argument: --environment.");

        return new CliParseResult
        {
            Options = new CliOptions
            {
                Environment = environment,
                ValidateOnly = validateOnly,
                InstallSchema = installSchema,
                DryRun = dryRun,
                Force = force,
                TraceMetadata = traceMetadata
            }
        };
    }

    private static CliParseResult Fail(string error) => new() { Error = error };
}
