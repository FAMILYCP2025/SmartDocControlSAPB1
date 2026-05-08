using SmartDocControl.Infrastructure.Configuration;

namespace SmartDocControl.Runner;

internal sealed record AppConfiguration(
    SapOptions Sap,
    ExecutionOptions Execution,
    LoggingOptions Logging);
