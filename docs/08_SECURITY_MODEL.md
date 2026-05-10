# Security Model — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Threat Model

### T1 — Credential Leakage

**Description:** The SAP service account password is exposed in a file, log, or process argument.

**Attack vectors:**
- Password stored in `appsettings.PRD.json` and committed to git.
- Password printed to console output or written to a log file.
- Password visible in `ps aux` or Task Manager (process arguments).

**Controls implemented:**
- Password is never stored in any JSON config file. Only `PasswordEnvironmentVariable` (the variable name, not the value) appears in config.
- The environment variable is read at login time in `ServiceLayerClient.LoginAsync` and immediately discarded. It is never stored in any field.
- `FileLogger` never receives the password value. No log call passes it.
- Console banner never displays `CompanyDb` or any credential. DebugMode only adds additional SAP response metadata, never credentials.
- `appsettings.TST.json` and `appsettings.PRD.json` are in `.gitignore`. Pre-commit hooks should enforce this if the team uses them.

**Residual risk:** Environment variables are visible to any process running as the same user on the same machine. Use a dedicated Windows service account with minimal permissions.

---

### T2 — Man-in-the-Middle (MITM)

**Description:** An attacker intercepts HTTPS traffic between the runner and SAP Service Layer.

**Attack vectors:**
- SSL certificate validation is disabled (`IgnoreSslErrors=true`), allowing traffic interception via a rogue certificate.
- HTTP (not HTTPS) used instead of HTTPS.

**Controls implemented:**
- `SAP-002` validation in `StartupValidator`: aborts if `BaseUrl` does not start with `https://`.
- `SEC-001` validation in `StartupValidator`: aborts if `IgnoreSslErrors=true` and `Environment=PRD`.
- `IgnoreSslErrors` defaults to `false` in `appsettings.json`.

**Residual risk:** `IgnoreSslErrors=true` is permitted in DEV and TST. This is acceptable for non-production environments with self-signed certificates. PRD is enforced by code — not by convention.

---

### T3 — Log Exposure

**Description:** Sensitive data (credentials, document content, personal data) is written to local log files and accessed by unauthorized personnel.

**Attack vectors:**
- SAP login payload (containing password) written verbatim to log file.
- Full HTTP response bodies (containing BP personal data) stored in logs.
- Log files left world-readable.

**Controls implemented:**
- The `FileLogger` never receives the raw login payload. Only high-level events are logged ("Runner started", "Login successful").
- Decision D-H (Schema Design): raw JSON request/response is written to `FileLogger` only in `DebugMode=true`, and never to SAP UDT fields.
- `DebugMode=false` by default in `appsettings.json`.
- Log directory is `C:\SmartDocControl\Logs\` — restrict permissions to the service account only (Windows ACL).

**Residual risk:** In `DebugMode=true`, SAP response bodies may contain BP names, document numbers, and amounts. Only activate DebugMode for troubleshooting sessions; disable immediately after.

---

### T4 — Rogue Scheduler

**Description:** A second instance of the runner starts while another is still executing, causing double-close of documents.

**Attack vectors:**
- Task Scheduler triggers a second run before the first completes (e.g., if the first run hangs).
- An operator manually starts the process while the scheduled run is active.

**Controls implemented:**
- `ExecutionOptions.PreventParallelRuns = true` by default.
- `ILockManager` implements a dual lock: local filesystem lock file (`SmartDocControl.lock`) and `@JCA_DLC_RUN` RUNNING record (pending implementation).
- `StaleRunThresholdHours = 4`: a RUNNING record older than 4 hours is treated as STALE and the lock is cleared, preventing permanent lockout after a hung run.

**Residual risk:** Until `ILockManager` concrete implementation is shipped (planned for 0.2.0), parallel run prevention is architectural only — the lock file mechanism is not active.

---

### T5 — Environment Confusion

**Description:** The runner is accidentally executed against the wrong environment (e.g., PRD config loaded in TST, or DEV config in PRD).

**Attack vectors:**
- `--environment PRD` passed but `appsettings.PRD.json` points to TST SAP server (copy-paste error in config).
- Task Scheduler task uses `--environment TST` but the server has PRD credentials configured.
- Banner not checked before a real run proceeds.

**Controls implemented:**
- `--environment` is mandatory — no default environment exists.
- Console banner prints `Environment` and SAP host:port explicitly before any operation.
- `CompanyDb` is visible in the banner **only when `DebugMode=true`** — this allows environment verification in troubleshooting sessions without exposing it by default.
- `ExecutionOptions.Environment` is set by the CLI argument and always wins over the JSON file value — the JSON cannot silently override it.
- `appsettings.TST.json` and `appsettings.PRD.json` are separate files — cross-contamination requires deliberate action.

**Residual risk:** A human can still point the wrong `appsettings.PRD.json` at the wrong SAP server. No automated cross-check between the file's `BaseUrl` and the declared environment exists.

---

### T6 — Stale Run Blocking Production

**Description:** A `@JCA_DLC_RUN` record stuck in RUNNING status blocks all future executions indefinitely.

**Attack vectors:**
- Process killed mid-run (power failure, force-kill from Task Manager).
- RUNNING record written but FINISHED/ERROR never written due to unhandled exception.

**Controls implemented:**
- `StaleRunThresholdHours = 4` (configurable): any RUNNING record older than this threshold is treated as STALE.
- STALE records are cleared at startup and a new run is allowed to proceed.
- `FileLogger` records the stale detection event.

**Residual risk:** If the SAP server is unreachable when startup validation runs, the stale check (which reads `@JCA_DLC_RUN`) cannot execute. The local lock file provides a fallback guard in this scenario.

---

## 2. PRD Security Guardrails

These are hard blocks — the application refuses to proceed if violated:

| Code | Condition | Behavior |
|---|---|---|
| SAP-002 | BaseUrl does not use HTTPS | Exit with `ValidationFailed` (exit code 2) |
| SAP-005 | PasswordEnvironmentVariable is empty | Exit with `ValidationFailed` |
| SAP-006 | Password env variable not set or empty | Exit with `ValidationFailed` |
| SEC-001 | `IgnoreSslErrors=true` in PRD | Exit with `ValidationFailed` |
| EXE-001 | Environment not specified | Exit with `UsageError` (exit code 1) |

---

## 3. Environment Variable Policy

| Variable | Scope | Description |
|---|---|---|
| `SAP_AUTOCLOSE_PASSWORD` | Machine or User | SAP service account password. Never in any file. |

Rules:
- Set at Machine scope for Task Scheduler runs (system account cannot read User scope variables set by a different user).
- Rotate on SAP password change — update the environment variable, verify with `--validate-only`, confirm Task Scheduler still works.
- Do not use `SETX` in scripts committed to git — it would expose the value in git history.

---

## 4. SAP User Permissions (Minimum Required)

The SAP service account (`svc_autoclose` or equivalent) requires only:

| Permission | Reason |
|---|---|
| Login to Service Layer | Authentication |
| Read `@JCA_DLC_RULE`, `@JCA_DLC_EXC` | Load rules and exclusions |
| Read Quotations, Orders, PurchaseQuotations, PurchaseOrders | Document discovery |
| POST `/Close` on Quotations, Orders, PurchaseQuotations, PurchaseOrders | Document closure |
| Read/Write `@JCA_DLC_LOG`, `@JCA_DLC_RUN` | Functional audit trail |
| Read `UserTablesMD` (metadata) | StartupValidator UDT check |

Do not grant: administrative access, user management, financial configuration, or any object type outside the four MVP document types.

---

## 5. Secrets Rotation Checklist

When rotating the SAP service account password:

1. Generate new password in SAP (do not reuse).
2. Update `SAP_AUTOCLOSE_PASSWORD` environment variable on the deployment server (Machine scope).
3. Run `SmartDocControl.Runner.exe --environment PRD --validate-only` to confirm login works.
4. If the Task Scheduler task is set to run under a specific user: re-enter the password in the Task Scheduler task properties (Windows caches it).
5. Confirm the next scheduled run succeeds via exit code 0 in Task Scheduler history.
