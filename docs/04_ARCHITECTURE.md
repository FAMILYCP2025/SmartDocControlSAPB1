# Architecture — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Solution Structure

```
SmartDocControlSAPB1/
├── src/
│   ├── SmartDocControl.Domain/           # Pure business rules
│   ├── SmartDocControl.Application/      # Use cases, ports, models
│   ├── SmartDocControl.Infrastructure/   # SAP Service Layer, logging, config
│   └── SmartDocControl.Runner/           # Console entry point
├── tests/
│   └── SmartDocControl.Tests/            # xUnit test project
├── docs/                                 # Architecture, decisions, runbooks
├── scripts/                              # SAP schema installer (planned)
└── PRD_Smart_Document_Control_SAP_B1.md
```

---

## 2. Layer Responsibilities

| Layer | Project | What lives here | What must NOT live here |
|---|---|---|---|
| Domain | `SmartDocControl.Domain` | Entities, enums, value objects, rule evaluation logic | External dependencies, I/O, configuration |
| Application | `SmartDocControl.Application` | Ports (interfaces), use-case services, models, exceptions | Concrete implementations, HTTP, filesystem |
| Infrastructure | `SmartDocControl.Infrastructure` | `ServiceLayerClient`, `StartupValidator`, `FileLogger`, DTOs, options classes | Business rules, presentation |
| Runner | `SmartDocControl.Runner` | `Program.cs`, `CliOptions`, `ConfigurationLoader`, `ConsoleOutputFormatter` | Business logic, SAP-specific code |
| Tests | `SmartDocControl.Tests` | xUnit tests for all layers | Production code |

---

## 3. Dependency Rules

```
Runner
  ├── references Infrastructure
  └── references Application

Infrastructure
  └── references Application
        └── references Domain

Domain
  └── references nothing
```

Dependency arrows always point inward. Domain has zero dependencies on any other project. Application depends only on Domain. Infrastructure depends on Application (implements ports). Runner composes everything.

---

## 4. Key Design Decisions

- **No DI container.** The Runner constructs all dependencies manually in `Program.cs`. No HostBuilder, no ServiceCollection, no Autofac.
- **No Polly.** Retry logic is implemented directly in `ServiceLayerClient.SendWithTransientRetryAsync` using exponential backoff.
- **No Serilog.** `FileLogger` is a minimal custom implementation (daily rotating files, structured lines).
- **No System.CommandLine.** CLI parsing is manual in `CliParseResult.Parse`.
- **`InternalsVisibleTo`** allows the test project to access `internal` Runner types without making them `public`.

---

## 5. Execution Flow

```
Task Scheduler (08:00 AM)
  └── SmartDocControl.Runner.exe --environment PRD
        │
        ├── 1. CliParseResult.Parse(args)
        ├── 2. ConfigurationLoader.Load(environment)
        ├── 3. Generate RunId (Guid 8-char hex)
        ├── 4. FileLogger.Init (non-fatal if fails)
        ├── 5. ConsoleOutputFormatter.PrintBanner
        │
        └── [--validate-only]
              ├── HttpClientHandler (IgnoreSslErrors conditional)
              ├── HttpClient (BaseAddress + Timeout)
              ├── ServiceLayerClient (CorrelationId = RunId)
              ├── StartupValidator.ValidateAsync()
              │     ├── Validate SapOptions
              │     ├── Validate ExecutionOptions
              │     ├── Validate SecurityRules (SEC-001)
              │     ├── Validate PasswordEnvVar (SAP-006)
              │     ├── Validate LoggingPaths (write probe)
              │     ├── [if no errors] LoginAsync
              │     ├── GetExistingUserTablesAsync (UDT-001)
              │     └── LogoutAsync
              └── PrintValidationReport → exit code
```

---

## 6. Configuration Stack

```
appsettings.json           (base defaults — committed)
  └── appsettings.{ENV}.json  (environment overlay — gitignored for TST/PRD)
        └── Environment variables  (overrides any key via MSFT config naming)
```

The `--environment` CLI argument determines which overlay file is loaded. It also sets `ExecutionOptions.Environment`, which the StartupValidator reads for security checks.

### appsettings sections

| Section | Class | Key fields |
|---|---|---|
| `Sap` | `SapOptions` | BaseUrl, CompanyDb, Username, PasswordEnvironmentVariable, IgnoreSslErrors, TimeoutSeconds |
| `Execution` | `ExecutionOptions` | Environment, DefaultSimulation, MaxRetries, RetryDelaySeconds, PreventParallelRuns, StaleRunThresholdHours |
| `Logging` | `LoggingOptions` | LogPath, PendingFunctionalLogPath, DebugMode, FileSizeLimitMb, RetainedFileCountLimit |

---

## 7. Exception Hierarchy

All exceptions are defined in `SmartDocControl.Application/Exceptions/`.

```
Exception
  └── SapAuthenticationException   # 401/403 from Service Layer — no retry
  └── SapTransientException        # 408/429/500-504, timeout, connection reset — retry exhausted
  └── SapFunctionalException       # 4xx non-auth business errors — no retry
  └── StartupValidationException   # Validation failed — abort startup
```

---

## 8. Startup Validation Codes

| Code | Severity | Condition |
|---|---|---|
| SAP-001 | Error | BaseUrl is empty |
| SAP-002 | Error | BaseUrl does not use HTTPS |
| SAP-003 | Error | CompanyDb is empty |
| SAP-004 | Error | Username is empty |
| SAP-005 | Error | PasswordEnvironmentVariable is empty |
| SAP-006 | Error | Password environment variable is not set or empty |
| EXE-001 | Error | Environment is empty |
| EXE-002 | Warning | Environment is not DEV/TST/PRD |
| SEC-001 | Error | IgnoreSslErrors=true in PRD |
| SIM-001 | Warning | PRD + DefaultSimulation=false |
| LOG-001 | Error | LogPath is empty |
| LOG-002 | Error | PendingFunctionalLogPath is empty |
| LOG-003 | Error | LogPath is not writable |
| LOG-004 | Error | PendingFunctionalLogPath is not writable |
| SAP-CONN-001 | Error | Service Layer login failed |
| UDT-001 | Error | Required UDT not found in SAP |
| UDT-CONN-001 | Error | Failed to query SAP user tables |
| LOGOUT-001 | Warning | SAP logout during validation failed (non-blocking) |

---

## 9. Exit Codes

| Code | Name | Meaning |
|---|---|---|
| 0 | Success | All operations completed without errors |
| 1 | UsageError | Invalid CLI arguments |
| 2 | ValidationFailed | StartupValidator found errors |
| 3 | FatalConfig | Configuration could not be loaded |
| 4 | UnhandledFatal | Unexpected exception |

Exit codes are defined in `SmartDocControl.Runner/ExitCodes.cs` and consumed by Task Scheduler to detect failures.

---

## 10. SAP Service Layer Client

`ServiceLayerClient` in `SmartDocControl.Infrastructure/ServiceLayer/` handles:

- **Session management:** Cookie-based (`B1SESSION` + `ROUTEID`). Thread-safe via `Interlocked.Exchange`.
- **Retry policy:** Exponential backoff on transient HTTP status codes (408/429/500/502/503/504) and connection-level errors. Max attempts = `MaxRetries + 1`.
- **401 re-login:** On any authenticated request returning 401, the client invalidates the session, re-logs in once, and retries. If the retry also returns 401, throws `SapAuthenticationException`.
- **Error classification:** 401/403 → `SapAuthenticationException`; transient exhausted → `SapTransientException`; other non-success → `SapFunctionalException`.
- **CorrelationId:** Propagated through all exceptions for cross-referencing with log entries.
