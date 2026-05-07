namespace SmartDocControl.Application.Models;

public sealed class RunContext
{
    // RunId funcional de ejecución
    public string CorrelationId { get; }
    public DateTime ExecutionDate { get; }
    public bool SimulationMode { get; }
    public int MaxDocumentsPerRun { get; }
    public string EnvironmentName { get; }

    public RunContext(
        string correlationId,
        DateTime executionDate,
        bool simulationMode,
        int maxDocumentsPerRun,
        string environmentName)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
        if (executionDate == default)
            throw new ArgumentException("ExecutionDate is required.", nameof(executionDate));
        if (string.IsNullOrWhiteSpace(environmentName))
            throw new ArgumentException("EnvironmentName is required.", nameof(environmentName));

        CorrelationId = correlationId;
        ExecutionDate = executionDate;
        SimulationMode = simulationMode;
        MaxDocumentsPerRun = maxDocumentsPerRun;
        EnvironmentName = environmentName;
    }
}
