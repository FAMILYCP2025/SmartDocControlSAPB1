# Deployment Strategy — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Runtime Requirements

| Requirement | Value |
|---|---|
| .NET runtime | .NET 8 (framework-dependent publish) |
| OS | Windows Server 2016+ or Windows 10+ |
| Architecture | x64 |
| Network | TCP access to SAP Service Layer (default port 50000) |
| Disk | ~50 MB for logs (configurable rolling limit) |
| Permissions | Write access to `C:\SmartDocControl\` |

The application is built as **framework-dependent** (`SelfContained=false`). The .NET 8 runtime must be installed on the target machine. For air-gapped servers, use the [.NET 8 offline runtime installer](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## 2. Build and Publish

### From development machine

```powershell
# Build (verify no errors before publishing)
dotnet build src/SmartDocControl.Runner --configuration Release

# Publish (framework-dependent, no self-contained)
dotnet publish src/SmartDocControl.Runner `
    --configuration Release `
    --output ./publish/TST
```

The publish output at `./publish/TST/` includes:
- `SmartDocControl.Runner.dll` — main assembly
- `SmartDocControl.Runner.exe` — Windows launcher
- `appsettings.json` — base configuration (committed, safe to deploy)
- `appsettings.DEV.json` — DEV overlay (committed, placeholder values)
- `appsettings.TST.json` — **if present locally**, copied automatically
- All dependency DLLs

### Important: gitignored config files

`appsettings.TST.json` and `appsettings.PRD.json` are excluded from git. They must be created locally from the example templates before publishing:

```powershell
Copy-Item src/SmartDocControl.Runner/appsettings.TST.example.json `
          src/SmartDocControl.Runner/appsettings.TST.json
# Then edit with real credentials
```

See `docs/06_FIRST_TST_VALIDATION.md` for the TST procedure.

---

## 3. Directory Layout on Target Server

```
C:\SmartDocControl\
├── App\                    # Published binaries
│   ├── SmartDocControl.Runner.exe
│   ├── SmartDocControl.Runner.dll
│   ├── appsettings.json
│   ├── appsettings.PRD.json   ← create from template, never in git
│   └── (other DLLs)
├── Logs\                   # Technical log files (daily rotation)
│   └── smartdoc_YYYYMMDD.log
└── PendingLogs\            # Pending functional logs (D07 fallback)
    └── {RunId}_{DocEntry}_{ts}.json
```

Permissions:
- The Windows service account running Task Scheduler must have **write access** to `C:\SmartDocControl\Logs\` and `C:\SmartDocControl\PendingLogs\`.
- The `App\` directory should be **read-only** for the service account to prevent accidental modification.

---

## 4. Environment Variable Setup

The SAP password is never stored in any file. Set the environment variable for the service account:

```powershell
# Set for the SYSTEM account or the specific service account:
[System.Environment]::SetEnvironmentVariable(
    "SAP_AUTOCLOSE_PASSWORD",
    "actual-password-here",
    "Machine"  # or "User" if running under a specific user account
)
```

Verify:
```powershell
[System.Environment]::GetEnvironmentVariable("SAP_AUTOCLOSE_PASSWORD", "Machine")
```

> The variable name can be changed via `Sap:PasswordEnvironmentVariable` in `appsettings.PRD.json` if required by the client's naming conventions.

---

## 5. Windows Task Scheduler Configuration

| Setting | Value |
|---|---|
| Action | Start a program |
| Program | `C:\SmartDocControl\App\SmartDocControl.Runner.exe` |
| Arguments | `--environment PRD` |
| Start in | `C:\SmartDocControl\App\` |
| Run As | Dedicated service account |
| Trigger | Daily at 08:00 AM |
| Run whether user is logged in | Yes |
| Run with highest privileges | As required by service account |

### Exit code monitoring

Task Scheduler can alert on non-zero exit codes. Map to the following exit codes:

| Exit Code | Name | Action |
|---|---|---|
| 0 | Success | Normal |
| 1 | UsageError | Fix Task Scheduler arguments — do not retry |
| 2 | ValidationFailed | Check startup validator output in logs |
| 3 | FatalConfig | Check `appsettings.PRD.json` and env variable |
| 4 | UnhandledFatal | Check logs; report bug |

---

## 6. First Deployment Checklist

- [ ] .NET 8 runtime installed on target server
- [ ] `C:\SmartDocControl\App\` directory created
- [ ] `C:\SmartDocControl\Logs\` directory created (or will be auto-created)
- [ ] `C:\SmartDocControl\PendingLogs\` directory created (or will be auto-created)
- [ ] `SAP_AUTOCLOSE_PASSWORD` environment variable set (Machine scope)
- [ ] `appsettings.PRD.json` created from `appsettings.PRD.example.json` with real credentials
- [ ] Published binaries copied to `C:\SmartDocControl\App\`
- [ ] `--validate-only` run manually to confirm SAP connectivity
- [ ] Task Scheduler task created and tested manually
- [ ] First real run with `MaxDocumentsPerRun` set to a low value (e.g., 5)

---

## 7. Update Procedure

1. Build and publish new version to a staging folder (e.g., `./publish/PRD-new`).
2. Disable the Task Scheduler task.
3. Verify no active run (`SmartDocControl.lock` absent or `@JCA_DLC_RUN` has no RUNNING record).
4. Replace binaries in `C:\SmartDocControl\App\`.
5. Do NOT replace `appsettings.PRD.json` (it is not in the publish output if gitignored).
6. Run `--validate-only` to confirm the new build works.
7. Re-enable the Task Scheduler task.

---

## 8. Rollback Procedure

Keep the previous publish output in a versioned folder:

```
C:\SmartDocControl\App-v0.1.0-alpha\   ← previous
C:\SmartDocControl\App\                 ← current
```

To roll back:
1. Disable Task Scheduler task.
2. Replace `App\` contents with contents of `App-v0.1.0-alpha\`.
3. Run `--validate-only` to confirm.
4. Re-enable Task Scheduler task.
