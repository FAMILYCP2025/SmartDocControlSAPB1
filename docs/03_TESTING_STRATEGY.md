# Testing Strategy — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Principles

1. Tests verify behavior, not implementation details.
2. No mocking frameworks — custom `StubHttpMessageHandler` only.
3. Tests must be deterministic and fast (no real network calls, no real filesystem in unit tests).
4. Each test class maps to a single production class.
5. **No integration tests against PRD.** Ever.

---

## 2. Test Pyramid

```
                 ┌───────────────────────────┐
                 │    Manual / Exploratory   │  TST only
                 │  (--validate-only, Postman)│
                 └───────────────────────────┘
              ┌───────────────────────────────────┐
              │       Integration Tests           │  TST only (never PRD)
              │  (real Service Layer, real UDTs)  │
              └───────────────────────────────────┘
       ┌─────────────────────────────────────────────────┐
       │                  Unit Tests                     │  All layers
       │  (stubs, in-memory, no network, no filesystem)  │
       └─────────────────────────────────────────────────┘
```

The current implementation operates exclusively at the unit test level.

---

## 3. Test Framework

| Tool | Purpose |
|---|---|
| xUnit | Test runner |
| FluentAssertions | Assertion library |
| `StubHttpMessageHandler` | Custom HTTP stub (no Moq) |

---

## 4. Current Test Inventory

**Total: 114 tests, 0 failures, 0 warnings** (as of 0.1.0-alpha)

### 4.1 Domain Layer

| File | Count | What it covers |
|---|---|---|
| `DocumentTests.cs` | — | `Document` constructor guards, `CanBeClosed`, `MarkAsClosed` |
| `DocumentRuleTests.cs` | — | `DocumentRule` constructor validation, property mapping |
| `DocumentRuleEvaluateTests.cs` | — | `Evaluate` — all `CloseDecision` branches (grace period, inactive rule, has target, recent activity, approval reserved) |
| `RuleEvaluationResultTests.cs` | — | `RuleEvaluationResult` value object invariants |
| `ExecutionResultTests.cs` | — | `ExecutionResult` state transitions |

### 4.2 Application Layer

| File | Count | What it covers |
|---|---|---|
| `DocumentCloseEvaluatorTests.cs` | — | `Evaluate` — simulation path, pending path, skip path |
| `RunContextTests.cs` | — | `RunContext` construction, `SimulationMode` override |
| `DocumentProcessingResultTests.cs` | — | Result envelope invariants |
| `ExecutionSummaryTests.cs` | — | Aggregation of counts |

### 4.3 Infrastructure Layer

| File | Count | What it covers |
|---|---|---|
| `StartupValidatorTests.cs` | — | All validation codes (SAP-001..006, SEC-001, EXE-001..002, LOG-001..004, UDT-001, LOGOUT-001) |
| `ServiceLayerClientTests.cs` | — | Login, Logout, GetAsync, GetExistingUserTablesAsync — happy paths and functional errors |
| `ServiceLayerClientResilienceTests.cs` | — | Transient retry (408/429/500/503/504), 401 re-login, exhausted retries → `SapTransientException` |

### 4.4 Runner Layer

| File | Count | What it covers |
|---|---|---|
| `CliOptionsTests.cs` | 8 | `--environment`, `-e`, `--validate-only`, `--dry-run`, `--help`, unknown args, missing value |
| `ConfigurationLoaderTests.cs` | 5 | Base file only, overlay applied, missing overlay, environment override, missing base throws |

---

## 5. Stub Infrastructure

### `StubHttpMessageHandler`

Located in `tests/SmartDocControl.Tests/TestHelpers/StubHttpMessageHandler.cs`.

Supports:
- Per-URL response registration.
- Status code control.
- Response body (JSON string).
- Call count tracking per URL.
- Exception injection (network errors, timeouts).

Pattern:
```csharp
var handler = new StubHttpMessageHandler();
handler.RegisterResponse("Login", HttpStatusCode.OK, loginResponseJson);
handler.RegisterResponse("Logout", HttpStatusCode.NoContent);
var client = new HttpClient(handler) { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };
```

---

## 6. Test Conventions

### Naming
```
MethodOrScenario_StateUnderTest_ExpectedBehavior
```

Examples:
- `Evaluate_GracePeriodNotExceeded_ReturnsSkipGracePeriod`
- `LoginAsync_Returns401_ThrowsSapAuthenticationException`
- `Parse_MissingEnvironment_ReturnsError`

### Arrange-Act-Assert
All tests use the AAA layout. No nested `describe` blocks.

### Test data
No shared fixtures. Each test creates its own minimal data inline. Helper factory methods allowed within a test class — never static singletons.

---

## 7. Integration Test Policy

### Allowed environments

| Environment | Unit tests | Integration tests |
|---|---|---|
| DEV | Yes | Yes |
| TST | Yes | Yes |
| PRD | Yes | **NEVER** |

**No integration tests against PRD. This is a hard constraint.**

Rationale: PRD contains real documents. Even a `--validate-only` call is a real login event in the SAP audit log. Integration tests that create/modify/close documents are categorically forbidden against PRD.

### Running integration tests (TST)

Integration tests (once added) will require:
- `SAP_AUTOCLOSE_PASSWORD` environment variable set.
- `appsettings.TST.json` present with valid credentials.
- Explicit opt-in via test category/trait — not run in standard `dotnet test`.

```powershell
dotnet test --filter "Category=Integration"
```

Integration tests are never run in CI by default.

---

## 8. Coverage Targets

| Layer | Target | Rationale |
|---|---|---|
| Domain | 100% | Pure logic, no external dependencies |
| Application services | 90%+ | Core business decisions |
| Infrastructure (unit, with stubs) | 80%+ | HTTP interaction and retry paths |
| Runner | Key paths | CLI parsing + config loading |

Coverage is a heuristic, not a gate. A test that only exercises the happy path counts as coverage but provides no confidence on error paths. Prefer branch coverage over line coverage.

---

## 9. What is NOT tested

- `FileLogger` filesystem writes (I/O side effect — tested manually).
- `ConsoleOutputFormatter` console output (presentation concern).
- `ConfigurationLoader` with real filesystem paths in CI (covered by unit tests with temp directories).
- Task Scheduler integration (manual validation only).
