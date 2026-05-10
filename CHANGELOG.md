# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [0.1.0-alpha] — 2026-05-10

### Added

- **Domain layer** — `Document`, `DocumentRule`, `DocumentRule.Evaluate`, `RuleEvaluationResult`, `ExecutionResult`, `CloseDecision`, `DocumentType`, `ExecutionStatus` enums and value objects.
- **Application layer** — `DocumentCloseEvaluator`, `RunContext`, `DocumentProcessingResult`, `ExecutionSummary`, `ValidationIssue`, `StartupValidationReport`. Ports: `IStartupValidator`, `IDocumentRepository`, `IConfigurationRepository`, `ILogRepository`, `ILockManager`. Exceptions: `SapAuthenticationException`, `SapTransientException`, `SapFunctionalException`, `StartupValidationException`.
- **Infrastructure layer** — `ServiceLayerClient` with cookie session management, transient retry with exponential backoff, 401 re-login, and structured error classification. `StartupValidator` with 18 validation codes (SAP-001..006, SEC-001, SIM-001, EXE-001..002, LOG-001..004, UDT-001, SAP-CONN-001, LOGOUT-001). `FileLogger` with daily rotation. Configuration classes: `SapOptions`, `ExecutionOptions`, `LoggingOptions`.
- **Runner** — `Program.cs` manual composition root. `CliOptions` / `CliParseResult` with `--environment` (mandatory), `--validate-only`, `--dry-run` alias, `--help`. `ConfigurationLoader` with layered `appsettings.json` + `appsettings.{ENV}.json` + environment variable overrides. `ConsoleOutputFormatter` with colored banner and validation report. `ExitCodes` named constants (0–4).
- **Tests** — 114 unit tests across Domain, Application, Infrastructure, and Runner layers. `StubHttpMessageHandler` for HTTP-level testing without mocking frameworks.
- **Configuration** — `appsettings.json` base, `appsettings.DEV.json`, `appsettings.TST.example.json`, `appsettings.PRD.example.json`. `appsettings.TST.json` and `appsettings.PRD.json` are gitignored.
- **Documentation** — `docs/01_DECISION_LOG.md` (11 decisions), `docs/02_PRD_VALIDATION.md`, `docs/03_TESTING_STRATEGY.md`, `docs/04_ARCHITECTURE.md`, `docs/05_DEPLOYMENT_STRATEGY.md`, `docs/06_FIRST_TST_VALIDATION.md`, `docs/07_SCHEMA_DESIGN.md` (5 UDTs, 63+ fields), `docs/08_SECURITY_MODEL.md`, `docs/09_RELEASE_STRATEGY.md`, `docs/10_OPERATIONAL_RUNBOOK.md`.

### Security

- Password is never stored in any configuration file. Read exclusively from a named environment variable at login time.
- `SEC-001`: `IgnoreSslErrors=true` blocked in PRD environment — aborts with exit code 2.
- `SAP-006`: Missing or empty password environment variable blocked at startup.
- `appsettings.TST.json` and `appsettings.PRD.json` added to `.gitignore` and untracked via `git rm --cached`.

### Validated

- SAP Service Layer connectivity in TST environment (real HTTP login + UDT metadata query) — 2026-05-08.

---

[Unreleased]: https://github.com/your-org/SmartDocControlSAPB1/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/your-org/SmartDocControlSAPB1/releases/tag/v0.1.0-alpha
