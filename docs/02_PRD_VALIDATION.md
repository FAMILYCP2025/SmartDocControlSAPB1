# PRD Validation — Smart Document Control for SAP Business One

**PRD Version:** 1.0  
**Validation Date:** 2026-05-10  
**Implementation Version:** 0.1.0-alpha

Each row maps a PRD requirement to the Decision Log entry that resolves it, the current implementation status, and the tests that cover it.

---

## Legend

| Status | Meaning |
|---|---|
| IMPLEMENTED | Code exists and tested |
| PARTIAL | Code exists but not fully integrated |
| PORT ONLY | Interface/contract defined; concrete implementation pending |
| NOT YET | Planned for a future version per the milestone map |
| ENFORCED | Validated at startup; cannot proceed without it |

---

## §4 — Alcance funcional MVP (Document Types)

| PRD Requirement | Decision | Status | Tests |
|---|---|---|---|
| Oferta de venta — Quotations (ObjType 23) | — | NOT YET | — |
| Orden de venta — Orders (ObjType 17) | — | NOT YET | — |
| Oferta de compra — PurchaseQuotations (ObjType 540000006) | — | NOT YET | — |
| Orden de compra — PurchaseOrders (ObjType 22) | — | NOT YET | — |
| Limit máximo de documentos por ejecución | — | PARTIAL | `DocumentRuleTests` — `MaxDocumentsPerRun` field validated |
| Prevención de ejecución paralela | D02 | PORT ONLY | — |
| Procesamiento secuencial | — | NOT YET | — |

---

## §7 — Flujo funcional del proceso

| Step | PRD Requirement | Decision | Status | Tests |
|---|---|---|---|---|
| 1 | Task Scheduler ejecuta a las 08:00 | — | CONFIGURED | — |
| 2 | Generar RunId único | — | IMPLEMENTED | `Program.cs:39` (Guid.NewGuid 8-char) |
| 3 | Validar no-ejecución-activa | D02 | PORT ONLY | — |
| 4 | Login en Service Layer | — | IMPLEMENTED | `ServiceLayerClientTests`, `StartupValidatorTests` |
| 5 | Leer reglas activas desde UDT | — | PORT ONLY | — |
| 6 | Leer exclusiones activas | — | PORT ONLY | — |
| 7 | Buscar documentos abiertos candidatos | D03 | PORT ONLY | — |
| 8 | Evaluar cada documento contra motor de reglas | — | IMPLEMENTED | `DocumentCloseEvaluatorTests`, `DocumentRuleEvaluateTests` |
| 9 | Simulación → registrar como SIMULATED | — | IMPLEMENTED | `DocumentCloseEvaluatorTests` |
| 10 | Cierre real → POST /Close vía Service Layer | — | NOT YET | — |
| 11 | Log individual por documento | D07 | PORT ONLY | — |
| 12 | Actualizar resumen de ejecución | — | PORT ONLY | — |
| 13 | Logout Service Layer | — | IMPLEMENTED | `ServiceLayerClientTests` |

---

## §8 — Arquitectura técnica

| PRD Requirement | Status | Notes |
|---|---|---|
| Domain layer | IMPLEMENTED | Entities, enums, value objects — no external dependencies |
| Application layer | IMPLEMENTED | Ports, services, models, exceptions |
| Infrastructure layer | PARTIAL | ServiceLayerClient, StartupValidator, FileLogger, options — repositories pending |
| Runner layer | IMPLEMENTED | Program.cs, CliOptions, ConfigurationLoader, ConsoleOutputFormatter |
| Tests project | IMPLEMENTED | 114 tests, 0 warnings |
| No DI API (CLAUDE.md) | ENFORCED | No DI API references anywhere |
| No SQL direct (CLAUDE.md) | ENFORCED | Zero SQL in codebase; all SAP access via Service Layer |

---

## §10 — Reglas de negocio MVP

| PRD Requirement | Decision | Status | Tests |
|---|---|---|---|
| Documento abierto como prerequisite | — | IMPLEMENTED | `DocumentTests.CanBeClosed` |
| Regla activa para tipo de documento | — | IMPLEMENTED | `DocumentRuleTests` — `IsActive` guard |
| Superar días de holgura (GraceDays) | — | IMPLEMENTED | `DocumentRuleEvaluateTests` — grace period boundary |
| No estar excluido | — | IMPLEMENTED | `DocumentCloseEvaluatorTests` — Skipped paths |
| Fecha base: DocDate / TaxDate / DocDueDate / UpdateDate | — | PARTIAL | `Document.BaseDate` field exists; selector enum pending |
| Modo simulación por regla (U_Simulation) | — | IMPLEMENTED | `DocumentCloseEvaluatorTests` — SimulationMode |
| Cierre real: POST /Close vía Service Layer | — | NOT YET | — |
| Límite por ejecución (MaxDocumentsPerRun) | — | PARTIAL | Field in `DocumentRule`; enforcement loop pending |

---

## §11 — Estados de resultado

| Estado PRD | CloseDecision / ExecutionStatus | Status | Tests |
|---|---|---|---|
| CLOSED | `ExecutionStatus.Closed` | IMPLEMENTED | `ExecutionResultTests` |
| SIMULATED | `ExecutionStatus.Simulated` | IMPLEMENTED | `DocumentCloseEvaluatorTests` |
| SKIPPED_NOT_EXPIRED | `CloseDecision.SkipGracePeriod` | IMPLEMENTED | `DocumentRuleEvaluateTests` |
| SKIPPED_EXCLUDED | `ExecutionStatus.Skipped` | IMPLEMENTED | `DocumentCloseEvaluatorTests` |
| SKIPPED_HAS_TARGET | `CloseDecision.SkipHasTarget` | IMPLEMENTED | `DocumentRuleEvaluateTests` |
| SKIPPED_RECENT_ACTIVITY | `CloseDecision.SkipRecentActivity` | IMPLEMENTED | `DocumentRuleEvaluateTests` |
| SKIPPED_REQUIRES_APPROVAL | `CloseDecision.SkipApprovalRequired` | IMPLEMENTED (D04 reserved) | `DocumentRuleEvaluateTests` |
| ERROR | `ExecutionStatus.Error` | IMPLEMENTED | `ExecutionResultTests` |

---

## §12 — Diseño de UDT

| PRD Requirement | Decision | Status | Notes |
|---|---|---|---|
| @JCA_DLC_RULE definida y validada | D06 | DESIGNED | Spec: `docs/07_SCHEMA_DESIGN.md §6`; provisioning pending |
| @JCA_DLC_EXC definida y validada | D06 | DESIGNED | Spec: `docs/07_SCHEMA_DESIGN.md §8` |
| @JCA_DLC_LOG definida y validada | D06 | DESIGNED | Spec: `docs/07_SCHEMA_DESIGN.md §9` |
| @JCA_DLC_RUN definida y validada | D06 | DESIGNED | Spec: `docs/07_SCHEMA_DESIGN.md §10` |
| @JCA_DLC_PAY (reglas por pago) | D01 | NOT YET | PRO feature; reference script only |
| StartupValidator verifica UDTs al arranque | D06 | IMPLEMENTED | `StartupValidatorTests` — `UDT-001` |

---

## §13 — Seguridad

| PRD Requirement | Decision | Status | Code |
|---|---|---|---|
| Usuario técnico SAP exclusivo | — | ENFORCED | `SapOptions.Username` in config, never default |
| No usar usuarios personales | — | ENFORCED | Config enforced by convention |
| Password fuera del código fuente | — | ENFORCED | `SapOptions.PasswordEnvironmentVariable`; `SAP-005`, `SAP-006` validation |
| Password desde variable de entorno | — | IMPLEMENTED | `ServiceLayerClient.LoginAsync` — `Environment.GetEnvironmentVariable` |
| HTTPS obligatorio | — | ENFORCED | `SAP-002` validation in `StartupValidator` |
| No registrar credenciales en logs | — | ENFORCED | Password never passed to logger anywhere |
| Separar configuración por ambiente | D05 | IMPLEMENTED | `appsettings.{ENV}.json` overlay stack |
| IgnoreSslErrors prohibido en PRD | D05 | ENFORCED | `SEC-001` in `StartupValidator` |
| DefaultSimulation=true en PRD por defecto | D10 | IMPLEMENTED | `SIM-001` warning if PRD + simulation=false |

---

## §14 — Logging y auditoría

| PRD Requirement | Decision | Status | Code |
|---|---|---|---|
| Log técnico local en `C:\SmartDocControl\Logs\` | — | IMPLEMENTED | `FileLogger`, `LoggingOptions.LogPath` |
| Log funcional en @JCA_DLC_LOG | — | PORT ONLY | `ILogRepository` |
| Resumen en @JCA_DLC_RUN | — | PORT ONLY | `ILockManager` (partial overlap) |
| Fallback a PendingFunctionalLogPath si log SAP falla | D07 | PORT ONLY | `ILogRepository` contract; `LoggingOptions.PendingFunctionalLogPath` |
| Política JSON: raw en errores, resumen en éxito | D-H (Schema) | PARTIAL | Raw JSON only in `FileLogger`, never in SAP LOG UDT fields |
| DebugMode: logging extendido configurable | — | IMPLEMENTED | `LoggingOptions.DebugMode` |

---

## §15 — Manejo de errores

| PRD Requirement | Decision | Status | Tests |
|---|---|---|---|
| Timeout Service Layer | D09 | IMPLEMENTED | `ServiceLayerClientResilienceTests` |
| Sesión expirada (401 re-login) | D09 | IMPLEMENTED | `ServiceLayerClientResilienceTests` |
| Errores HTTP transitorios (408/429/500/502/503/504) | D09 | IMPLEMENTED | `ServiceLayerClientResilienceTests` |
| Documento ya cerrado | — | PARTIAL | `Document.CanBeClosed()` guard; HTTP-level not yet |
| Permiso insuficiente (403) | D09 | IMPLEMENTED | `SapFunctionalException` (no retry on 403) |
| Fallo al escribir log en UDT | D07 | PORT ONLY | `ILogRepository` fallback contract |
| Config incompleta → abortar con mensaje | — | IMPLEMENTED | `StartupValidator` — SAP-001..006, EXE-001, LOG-001..004 |
| Error individual no detiene todo el proceso | — | PORT ONLY | Architecture supports it; processing loop pending |

---

## §16 — Idempotencia y doble ejecución

| PRD Requirement | Decision | Status | Notes |
|---|---|---|---|
| Archivo lock local | D02 | PORT ONLY | `ILockManager` contract |
| Registro RUNNING en @JCA_DLC_RUN | D02 | PORT ONLY | `ILockManager` contract |
| Detección de STALE runs | D02 | CONFIG ONLY | `ExecutionOptions.StaleRunThresholdHours = 4` |

---

## §17 — Parámetros de ejecución

| PRD Parameter | Status | Notes |
|---|---|---|
| `--environment` / `-e` | IMPLEMENTED | Mandatory; loads `appsettings.{ENV}.json` overlay |
| `--validate-only` / `--dry-run` | IMPLEMENTED | Alias pair; login + UDT check + exit |
| `--help` / `-h` | IMPLEMENTED | No `--environment` required |
| `--simulation` | NOT YET | Overrides `DefaultSimulation` at runtime |
| `--company` | NOT YET | Runtime CompanyDb override |
| `--documentType` | D08 | NOT YET | Filters rules by U_EntitySet |
| `--max` | NOT YET | Runtime MaxDocumentsPerRun override |

---

## §18 — Criterios de aceptación

| Criterio PRD | Status |
|---|---|
| Ejecuta desde consola | IMPLEMENTED |
| Ejecuta desde Task Scheduler | CONFIGURED (exit codes mapped; deployment guide in `docs/05`) |
| Login Service Layer | IMPLEMENTED + VALIDATED (TST real, 2026-05-08) |
| Lee UDT (reglas activas) | PORT ONLY |
| Simula cierre | IMPLEMENTED (logic); NOT INTEGRATED (full loop) |
| Cierra documento (POST /Close) | NOT YET |
| Registra log detalle y resumen | PORT ONLY |
| Maneja error individual sin detener proceso | PORT ONLY |
| Respeta MaxPerRun | PARTIAL (field exists; enforcement loop pending) |
| Previene doble ejecución | PORT ONLY |
| No expone credenciales | ENFORCED |
| Código mantenible (capas) | IMPLEMENTED |
