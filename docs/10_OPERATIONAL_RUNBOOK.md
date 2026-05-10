# Operational Runbook — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Daily Operations

### Normal run (Task Scheduler)

The runner executes daily at 08:00 AM via Windows Task Scheduler. No manual intervention is required unless:

- The Task Scheduler history shows a non-zero exit code.
- The `C:\SmartDocControl\Logs\` directory is not updated.
- Business users report unexpected document state changes.

### Verify last run result

```powershell
# Check exit code from most recent Task Scheduler run
# In Task Scheduler UI: Task History → Last Run Result

# Check last log entry
Get-Content "C:\SmartDocControl\Logs\smartdoc_$(Get-Date -Format 'yyyyMMdd').log" -Tail 30
```

---

## 2. Manual Execution

### Validate-only (connectivity check)

```powershell
cd C:\SmartDocControl\App
.\SmartDocControl.Runner.exe --environment PRD --validate-only
```

Expected output:
```
Smart Document Control — SAP Business One
  Environment : PRD
  SAP         : https://sap-server:50000
  Run ID      : A1B2C3D4

Running startup validation...

Validation: OK
  Validated at: 2026-05-10 08:00:00 UTC
```

Exit code 0 = healthy.

### Simulation run (when document processing is implemented)

```powershell
.\SmartDocControl.Runner.exe --environment PRD
```

With `DefaultSimulation=true` in `appsettings.PRD.json`, this processes documents without closing them.

---

## 3. Known Failure Modes

### FM-001 — SAP-006: Password variable not set

**Symptom:**
```
[ERR] SAP-006: Password environment variable 'SAP_AUTOCLOSE_PASSWORD' is not set or is empty.
Validation: FAILED
```
**Exit code:** 2

**Cause:** The `SAP_AUTOCLOSE_PASSWORD` environment variable is not set in the scope the process runs under. Task Scheduler processes run as SYSTEM or a service account — User-scope variables are not visible to them.

**Fix:**
```powershell
[System.Environment]::SetEnvironmentVariable("SAP_AUTOCLOSE_PASSWORD", "password", "Machine")
```
Then restart the Task Scheduler task (environment variables are read at process start).

---

### FM-002 — SAP-CONN-001: Login failed (401)

**Symptom:**
```
[ERR] SAP-CONN-001: SAP Service Layer login failed: SAP login failed with HTTP 401.
Validation: FAILED
```
**Exit code:** 2

**Checklist:**
1. Is `SAP_AUTOCLOSE_PASSWORD` correct? Test with Postman using the same credentials.
2. Is `Username` in `appsettings.PRD.json` the exact SAP user name (case-sensitive)?
3. Is `CompanyDb` in `appsettings.PRD.json` the correct company database name?
4. Is the SAP user account locked? Check in SAP B1 administration.
5. Has the password expired? Check in SAP B1.

---

### FM-003 — SAP-002: HTTP instead of HTTPS

**Symptom:**
```
[ERR] SAP-002: SapOptions.BaseUrl must use HTTPS scheme (got: 'http://...').
Validation: FAILED
```
**Fix:** Update `appsettings.PRD.json` — change `http://` to `https://` in `BaseUrl`.

---

### FM-004 — SEC-001: IgnoreSslErrors in PRD

**Symptom:**
```
[ERR] SEC-001: D05: IgnoreSslErrors=true is forbidden in PRD environment.
Validation: FAILED
```
**Fix:** Set `"IgnoreSslErrors": false` in `appsettings.PRD.json`. If the SAP server has a self-signed certificate, it must be added to the Windows certificate store instead.

---

### FM-005 — UDT-001: Required UDTs not found

**Symptom:**
```
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_RULE' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_EXC' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_LOG' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_RUN' not found in SAP.
Validation: FAILED
```
**Exit code:** 2

**Cause:** The User Defined Tables have not been provisioned in the SAP company database.

**Fix:** Run the schema installer (available from 0.2.0):
```powershell
.\SmartDocControl.Runner.exe --environment PRD --install-schema
```

---

### FM-006 — LOG-003/LOG-004: Log path not writable

**Symptom:**
```
[ERR] LOG-003: LogPath 'C:\SmartDocControl\Logs\' is not writable: Access to the path is denied.
Validation: FAILED
```
**Fix:** Grant write permissions to the service account on `C:\SmartDocControl\`.

```powershell
icacls "C:\SmartDocControl" /grant "DOMAIN\svc_smartdoc:(OI)(CI)F"
```

---

### FM-007 — FatalConfig (exit code 3): appsettings.PRD.json not found

**Symptom:**
```
[FATAL] Failed to load configuration: The configuration file 'appsettings.json' was not found...
```
Or: runner uses base defaults pointing to `sap-server` placeholder.

**Cause:** The published binary directory does not contain `appsettings.PRD.json`.

**Fix:**
1. Verify `appsettings.PRD.json` exists in `C:\SmartDocControl\App\`.
2. If missing: create from template and copy manually. The file is not included in the published output if it did not exist at publish time.

---

### FM-008 — Stale run blocking execution (OP-001)

**Symptom:** Runner aborts immediately with a message about an existing active run.

**Cause:** A previous run was killed mid-execution. `SmartDocControl.lock` exists, or `@JCA_DLC_RUN` has a RUNNING record older than `StaleRunThresholdHours`.

**Automatic resolution:** The runner automatically clears stale runs on startup if the record age exceeds `StaleRunThresholdHours` (default: 4 hours).

**Manual resolution if automatic fails:**
```powershell
Remove-Item "C:\SmartDocControl\App\SmartDocControl.lock" -ErrorAction SilentlyContinue
```
Then manually update the `@JCA_DLC_RUN` record in SAP B1 to `Status = STALE`.

---

### FM-009 — Disk full: log directory

**Symptom:** Exit code 4 (UnhandledFatal), or `[WARNING] RUNNING WITHOUT TECHNICAL FILE LOGGER` in console.

**Cause:** `C:\SmartDocControl\Logs\` is full.

**Fix:**
1. Check disk space: `Get-PSDrive C`
2. Delete old log files manually, or adjust `RetainedFileCountLimit` in `appsettings.PRD.json`.
3. Implement log archiving or increase disk allocation.

> Retention policy for log files is TBD after production metrics are collected.

---

## 4. Log File Reference

Log files are written daily to `C:\SmartDocControl\Logs\smartdoc_YYYYMMDD.log`.

Format:
```
2026-05-10 08:00:01Z [INF] [A1B2C3D4] Runner started. Environment=PRD, ValidateOnly=False
2026-05-10 08:00:02Z [INF] [A1B2C3D4] Startup validation passed.
2026-05-10 08:05:13Z [INF] [A1B2C3D4] Execution completed. Closed=0 Simulated=47 Errors=0
```

Fields: `timestamp [LEVEL] [RunId] message`

Levels: `[INF]` `[WRN]` `[ERR]`

---

## 5. PendingLogs Reference

Files in `C:\SmartDocControl\PendingLogs\` are created when a document closes successfully in SAP but the functional log write to `@JCA_DLC_LOG` fails (D07 fallback).

File name: `{RunId}_{DocEntry}_{timestamp}.json`

These files represent closed documents with missing audit trail. They should be:
1. Reviewed to confirm the document was actually closed in SAP.
2. Manually reconciled in `@JCA_DLC_LOG` if audit completeness is required.
3. Deleted once reconciled.

> Monitor this folder regularly. Accumulation of `.json` files indicates a persistent SAP log write failure.

---

## 6. Health Check Procedure (Monthly)

1. Run `--validate-only` against PRD and confirm exit code 0.
2. Review `C:\SmartDocControl\Logs\` for recurring WRN or ERR patterns.
3. Check `C:\SmartDocControl\PendingLogs\` for unreconciled files.
4. Verify disk space on the deployment server.
5. Confirm SAP service account is not locked and password has not expired.
6. Review Task Scheduler history for the past 30 days — confirm no unexplained failures.
