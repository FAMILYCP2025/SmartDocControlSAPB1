# Schema Design — Smart Document Control SAP B1 (UDTs v1)

**Estado:** Diseño cerrado y aprobado. Pendiente: validación final contra PRD §11/§12 (D-I) y luego diseño del installer.
**Fecha cierre diseño:** 2026-05-08
**Versión schema target:** `1`
**PRD de referencia:** `PRD_Smart_Document_Control_SAP_B1.md` v1.0 (§11, §12)
**Decisiones de diseño aplicadas:** D-A, D-B, D-C, D-D, D-E, D-G, D-H, D-I + Ajustes #1–#7

---

## 1. Propósito y alcance

Este documento define el diseño físico definitivo de las User Defined Tables (UDTs) y User Defined Fields (UDFs) del módulo Smart Document Control para SAP Business One v10 sobre HANA.

**Alcance v1 (MVP):**
- `@JCA_DLC_SCHEMA_VERSION`
- `@JCA_DLC_RULE`
- `@JCA_DLC_EXC`
- `@JCA_DLC_LOG`
- `@JCA_DLC_RUN`

**Fuera de alcance v1 (diferido a PRO):**
- `@JCA_DLC_PAY` — reglas por condición de pago (PRD §12.2). Deferido por D01.
- Particionamiento, archivado automático, drift detection cross-DB.

**No es alcance de este documento:**
- Diseño del installer (próximo paso, ya aprobado el enfoque general).
- Scripts SQL HANA (no se usan: vamos por Service Layer JSON descriptors).

---

## 2. Convenciones transversales (decisiones)

### 2.1 Tipos SAP B1 permitidos

| Tipo | Uso |
|------|-----|
| `db_Alpha` | Strings, enums, ISO 8601 timestamps, códigos. Tamaño máximo SAP: 254. |
| `db_Numeric` | Enteros, conteos, contadores, ms, días. |
| `db_Float` | Importes (`MinTotal`, `MaxTotal`). Único caso. |
| `db_Date` | Fechas SAP nativas (DocDate, DueDate, BaseDate, ValidFrom/ValidTo). |

### 2.2 Tipos prohibidos por convención

| Tipo | Por qué |
|------|---------|
| `db_Memo` | Blob, no indexable, infla backups y replicaciones. **Nunca en LOG.** |
| Tipos HANA-only | Rompen portabilidad SQL Server. |
| `db_DateTime` | No existe en UDFs. Usar `db_Alpha 30` ISO 8601. |

### 2.3 Booleanos (D-B aprobado)

`db_Alpha 1`, valores `Y` / `N`, default explícito (`Y` o `N`).
- App valida que solo se acepten `Y`/`N`.
- B1 UI los muestra correctamente.

### 2.4 Timestamps (D-A aprobado)

`db_Alpha 30` con formato **ISO 8601 UTC**:
```
YYYY-MM-DDTHH:mm:ssZ
```
- Siempre UTC, sufijo `Z` obligatorio.
- Generación: `DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)`.
- App valida formato antes de POST a SAP.
- Costo: B1 UI muestra como texto, no como datetime. Aceptable para auditoría programática.

### 2.5 Fechas SAP nativas

`db_Date` se usa solo cuando el campo refleja semántica SAP de "fecha de documento" (DocDate, DueDate, BaseDate resuelto, ValidFrom/ValidTo). Para timestamps de auditoría SIEMPRE ISO 8601 string.

### 2.6 Naming

- Tablas: `JCA_DLC_<NAME>` mayúsculas. SAP añade `@` automáticamente.
- UDFs: PascalCase. SAP añade `U_` automáticamente.
- Códigos enum: PascalCase para Status (`Started`), UPPER_SNAKE para Result (`SKIPPED_HAS_TARGET`) — distinto por origen (Status es diseño nuestro; Result viene de PRD §11 literal).

### 2.7 Tamaños alfa (Ajuste #7 aplicado)

Política: tamaño = caso peor real conocido + margen mínimo. **Ningún campo a 254 sin justificación.**

| Patrón | Tamaño |
|--------|--------|
| Boolean Y/N | 1 |
| Env (DEV/TST/PRD/STAGE) | 5 |
| Status enum | 12 |
| Result enum (PRD §11, max "SKIPPED_REQUIRES_APPROVAL" = 25) | 25 |
| ObjType (incluye UDOs ≥1.25e9 como string) | 10 |
| ExcType / TriggeredBy / Mode | 12-15 |
| CardCode (match `OCRD`) | 15 |
| Version (números cortos) | 5 |
| AppVersion (semver "1.2.3-rc1") | 20 |
| Reason / CloseComment / Hostname / CardName / Description corta | 100 |
| Message (sanitizado, error funcional) | 200 |
| EntitySet (SL endpoints) | 50 |
| Code (SAP default UDT PK) | 50 |
| Name (SAP default UDT) | 100 |
| ISO 8601 UTC timestamp | 30 |
| GUID 32 hex | 32 |
| BaseDate enum | 15 |

### 2.8 Defaults

Solo cuando hay regla de negocio que justifica el default. Reglas activas (Active=Y), Status inicial (Started), Mode inicial (Simulation por D10), Counters (0), Attempts (1).

### 2.9 NOT NULL no existe en UDFs

SAP no permite enforce NOT NULL vía Service Layer. **La app debe validar campos obligatorios antes de POST.** Documentar columna "Obligatorio (app)" en cada tabla.

---

## 3. CorrelationId / RunId (Ajuste #3 aprobado)

**Formato único:**
```
Guid.NewGuid().ToString("N")
```
- 32 chars hexadecimales, sin guiones.
- Generado una sola vez por run, al inicio.
- **`RUN.Code` ≡ `RunId` ≡ `CorrelationId`.** Un solo concepto, un solo valor.
- `LOG.U_RunId` referencia ese mismo valor (FK lógico).
- Banner del Runner se mantiene legible mostrando los primeros 8 chars uppercase, pero el valor persistido siempre es completo.

**Cambio respecto al Runner actual:** `Program.cs` actualmente genera `[..8].ToUpperInvariant()` como `runId`. Cuando se conecte al installer/audit pipeline, debe pasar a generar el GUID completo. El display puede seguir mostrando los primeros 8 chars. **Cambio diferido al commit que conecta el ServiceLayerClient.CorrelationId con la persistencia LOG/RUN.**

---

## 4. Códigos de fila (PK SAP `Code`)

| Tabla | Formato | Tamaño | Generación |
|-------|---------|--------|------------|
| `JCA_DLC_SCHEMA_VERSION` | Literal `CURRENT` | 7 | Constante |
| `JCA_DLC_RULE` | Human-readable, ej. `RULE_QUO_120` | ≤30 (regla app) | Manual / config inicial |
| `JCA_DLC_EXC` | Human-readable, ej. `EXC_001` | ≤20 (regla app) | Manual / config inicial |
| `JCA_DLC_RUN` | 32-char GUID | 32 | `Guid.NewGuid().ToString("N")` |
| `JCA_DLC_LOG` | 32-char GUID | 32 | `Guid.NewGuid().ToString("N")` por fila |

**Resolución del Ajuste #6 — `LOG.Code` formato:**
El formato propuesto inicialmente `LOG_{RUNCODE}_{seq}` se **descarta** por:
1. `RUNCODE` es 32 chars → `LOG_<32>_<seq>` ≥ 38 chars: comprime el margen del campo `Code` (50 default).
2. La secuencia `seq` requiere lookup por run (`SELECT MAX(seq) WHERE RunId=...`) o contador en memoria. Lookup: race conditions con paralelismo futuro. Contador: no sobrevive a reinicios mid-run.
3. `Guid.NewGuid().ToString("N")` resuelve unicidad sin coordinación, sin lookup, sin sequence.

**Decisión:** `LOG.Code` y `RUN.Code` son ambos GUID-N de 32 chars. Patrón uniforme.

---

## 5. Enums oficiales

### 5.1 `RUN.U_Status` (Ajuste #5 aprobado)

| Valor | Significado | Quién lo escribe |
|-------|-------------|------------------|
| `Started` | Run en curso. Default al INSERT inicial. | App, al iniciar |
| `Completed` | Run terminó normalmente. | App, al final feliz |
| `Failed` | Run terminó con error no recuperable. | App, en catch global |
| `Aborted` | Run abortado por usuario o LockManager. | App, antes de salir |
| `Stale` | Run quedó en estado activo más de `StaleRunThresholdHours`. **Lo escribe el siguiente run** al detectar zombi. | App, al iniciar nuevo run |

**Divergencia explícita con PRD §12.5** (que listaba "RUNNING, FINISHED, ERROR"): se reemplaza por este enum de 5 valores. Mapeo conceptual:
- `RUNNING` → `Started`
- `FINISHED` → `Completed`
- `ERROR` → `Failed`
- + `Aborted` (nuevo, control)
- + `Stale` (nuevo, runs zombi sin Zombie semántico)

**Tamaño:** `db_Alpha 12` (max literal "Completed" = 9, margen 3).

**No usar `Zombie`** como valor persistido (Ajuste #5). El término operativo informal puede usarse en logs técnicos (FileLogger), no en SAP.

### 5.2 `LOG.U_Result` (PRD §11 literal)

Valores canónicos de PRD §11, sin renombrar:

| Valor | Tamaño | Significado |
|-------|--------|-------------|
| `CLOSED` | 6 | Documento cerrado en SAP |
| `SIMULATED` | 9 | Candidato confirmado, no cerrado por simulación |
| `SKIPPED_NOT_EXPIRED` | 19 | No supera grace days |
| `SKIPPED_EXCLUDED` | 16 | Match con `JCA_DLC_EXC` |
| `SKIPPED_HAS_TARGET` | 18 | Tiene documento sucesor (OnlyNoTarget=Y) |
| `SKIPPED_RECENT_ACTIVITY` | 23 | UpdateDate dentro de InactiveDays |
| `SKIPPED_REQUIRES_APPROVAL` | 25 | ReqApproval=Y (PRO) |
| `ERROR` | 5 | Error técnico/funcional al procesar este doc |

**Tamaño:** `db_Alpha 25` (max = "SKIPPED_REQUIRES_APPROVAL" exacto, sin margen — los valores son cerrados por PRD §11).

### 5.3 `RULE.U_BaseDate` (selector de fecha base)

Valores canónicos de PRD §10.2:
- `DocDate`
- `TaxDate`
- `DocDueDate`
- `UpdateDate`

**Tamaño:** `db_Alpha 15` (max "DocDueDate" = 10, margen 5).

**Nota crítica de naming:** el campo se llama `BaseDate` pero su semántica difiere por tabla:
- En `RULE`: **selector** ("qué fecha SAP usar"). Valor: enum.
- En `LOG`: **valor resuelto** ("la fecha que se usó"). Valor: `db_Date`.

### 5.4 `EXC.U_ExcType` (PRD §12.3 literal)

Valores: `CardCode` | `GroupCode` | `SlpCode` | `BPLId`.
**Tamaño:** `db_Alpha 15`.

**Nota:** PRD §12.3 NO incluye exclusiones por DocEntry/DocNum/Series. Si esto se necesita en MVP, requiere extender este enum y solicitar aprobación.

### 5.5 `RUN.U_Mode`

Valores: `Simulation` | `Real`.
**Tamaño:** `db_Alpha 12`.

**Semántica de simulación efectiva:**
```
EffectiveSimulation = (RUN.Mode == "Simulation") OR (RULE.Simulation == "Y")
```
Es decir: si **cualquiera** está en simulación → no se cierra. Cierre real requiere ambos en Real/N.

### 5.6 `RUN.U_TriggeredBy`

Valores: `Manual` | `Schedule` | `Service`.
**Tamaño:** `db_Alpha 12`.

---

## 6. UDT — `@JCA_DLC_SCHEMA_VERSION` (Ajuste #1 aprobado, MVP)

**Objetivo:** Una sola fila identifica la versión de schema instalada en la company DB.

**Volumen:** 1 fila, exactamente. Invariante app-side.

**Campos:**

| Campo | Tipo | Tamaño | Default | Obligatorio (app) | Notas |
|-------|------|--------|---------|-------------------|-------|
| Code | (SAP) | 50 | — | sí | Constante `CURRENT` |
| Name | (SAP) | 100 | — | sí | "Schema Version" |
| U_Version | db_Alpha | 5 | — | sí | "1", "2", … |
| U_AppliedAt | db_Alpha | 30 | — | sí | ISO 8601 UTC |

**Índices:** ninguno. PK basta.

**Riesgo R-SV-01:** SAP no enforce "single-row-where-Code='CURRENT'". App debe upsert idempotente (si existe → UPDATE, si no → INSERT).

**Diferido a PRO** (no en v1, evitar campos sin uso):
- `U_AppliedBy` (usuario SAP que ejecutó install)
- `U_Hostname` (máquina del installer)
- `U_AppVersion` (versión del Runner)

Estos se añaden cuando el installer-PRO los necesite. Versionado del schema permite migration aditiva.

---

## 7. UDT — `@JCA_DLC_RULE`

**Objetivo:** Reglas declarativas de cierre por tipo de documento. PRD §10, §12.1.

**Volumen esperado:** 5–50 filas/empresa. Sin riesgos performance.

**Campos** (alineados con PRD §12.1 + D-C + D-E):

| Campo | Tipo | Tamaño | Default | Obligatorio (app) | Notas |
|-------|------|--------|---------|-------------------|-------|
| Code | (SAP) | 50 | — | sí | Ej. `RULE_QUO_120` |
| Name | (SAP) | 100 | — | sí | |
| U_Active | db_Alpha | 1 | `Y` | sí | Y/N |
| U_Priority | db_Numeric | 4 | 100 | sí | Orden de evaluación. Menor = mayor prioridad. |
| U_ObjType | db_Alpha | 10 | — | sí | "23"/"17"/"22"/"540000006". Soporta UDOs. |
| U_EntitySet | db_Alpha | 50 | — | sí | "Quotations"/"Orders"/"PurchaseQuotations"/"PurchaseOrders" |
| U_BaseDate | db_Alpha | 15 | `DocDate` | sí | Selector enum (§5.3) |
| U_GraceDays | db_Numeric | 4 | — | sí | 0–9999. App valida ≥0. |
| U_Simulation | db_Alpha | 1 | `Y` | sí | Y/N. Per-rule simulation flag (PRD §10.3). |
| U_MaxDocumentsPerRun | db_Numeric | 6 | 100 | sí | 1–999999. `0` = ilimitado (D-C: renombrado de `MaxPerRun`). |
| U_OnlyNoTarget | db_Alpha | 1 | `Y` | sí | Y/N. Default `Y` por seguridad. |
| U_CheckLines | db_Alpha | 1 | `N` | sí | Y/N. Validar líneas abiertas. |
| U_CheckUpdate | db_Alpha | 1 | `N` | sí | Y/N. Validar UpdateDate reciente. |
| U_InactiveDays | db_Numeric | 4 | — | no | Días mínimos sin update (relevante si `U_CheckUpdate=Y`). |
| U_MinTotal | db_Float | — | — | no | Importe mínimo. |
| U_MaxTotal | db_Float | — | — | no | Importe máximo. |
| U_ReqApproval | db_Alpha | 1 | `N` | sí | Y/N. **PRO únicamente.** En MVP siempre `N`. (D-E mantener desde v1) |
| U_CloseComment | db_Alpha | 100 | — | no | Comentario funcional al cerrar. |
| U_ValidFrom | db_Date | — | — | no | Vigente desde. |
| U_ValidTo | db_Date | — | — | no | Vigente hasta. |

**Índices:**

| Índice | Único | Campos | Justificación |
|--------|-------|--------|---------------|
| `Active_ObjType_IDX` | no | `U_Active`, `U_ObjType` | Filtro principal al inicio de cada run. |

**Notas:**
- Total 18 UDFs + 2 SAP defaults. Crecimiento controlado.
- `U_MaxDocumentsPerRun` = `0` significa **ilimitado**, no "ninguno". Documentar en operación.
- `U_MinTotal`/`U_MaxTotal` son los únicos `db_Float` del schema (importes monetarios SAP).

**Diferido a PRO:**
- `U_RuleSet` (agrupación)
- `U_Schedule` (cron)
- `U_LastAppliedAt` (auditoría)

---

## 8. UDT — `@JCA_DLC_EXC`

**Objetivo:** Exclusiones que bloquean cierre aunque la regla aplique. PRD §12.3.

**Volumen esperado:** 0–500 filas/empresa.

**Campos:**

| Campo | Tipo | Tamaño | Default | Obligatorio (app) | Notas |
|-------|------|--------|---------|-------------------|-------|
| Code | (SAP) | 50 | — | sí | Ej. `EXC_001` |
| Name | (SAP) | 100 | — | sí | |
| U_Active | db_Alpha | 1 | `Y` | sí | |
| U_ObjType | db_Alpha | 10 | — | sí | "23"/"17"/"22"/"540000006"/`ALL` |
| U_ExcType | db_Alpha | 15 | — | sí | Enum §5.4 |
| U_ExcValue | db_Alpha | 30 | — | sí | Acomoda CardCode (15) y códigos numéricos. |
| U_Reason | db_Alpha | 100 | — | no | **No db_Memo.** |
| U_ValidFrom | db_Date | — | — | no | (D-E reservado v1, opcional) |
| U_ValidTo | db_Date | — | — | no | (D-E reservado v1, opcional) |

**Índices:**

| Índice | Único | Campos | Justificación |
|--------|-------|--------|---------------|
| `Active_ObjType_IDX` | no | `U_Active`, `U_ObjType` | Filtro base al inicio de run. |
| `ObjType_ExcType_ExcValue_IDX` | no | `U_ObjType`, `U_ExcType`, `U_ExcValue` | Lookup rápido por documento durante evaluación. |

**Notas:**
- `U_ObjType` admite literal `ALL` para exclusión multi-objeto.
- App valida `U_ExcType` contra enum cerrado §5.4 antes de POST (SAP no enforce).

---

## 9. UDT — `@JCA_DLC_LOG` ⚠️ TABLA CRÍTICA

**Objetivo:** Audit trail por documento evaluado. PRD §12.4.

**Volumen esperado:** 1k–10k filas/día/cliente. Multi-año multi-cliente: millones.

**Riesgo principal:** crecimiento ilimitado sin partitioning ni archivado nativo. Mitigación: §11 (retención).

**Campos** (alineados con PRD §12.4 + Ajuste #7 + D-H):

| Campo | Tipo | Tamaño | Default | Obligatorio (app) | Notas |
|-------|------|--------|---------|-------------------|-------|
| Code | (SAP) | 50 | — | sí | 32-char GUID por fila (Ajuste #6) |
| Name | (SAP) | 100 | — | sí | Resumen corto |
| U_RunId | db_Alpha | 32 | — | sí | FK lógico → `RUN.Code` |
| U_DateTime | db_Alpha | 30 | — | sí | ISO 8601 UTC |
| U_Env | db_Alpha | 5 | — | sí | "DEV"/"TST"/"PRD" |
| U_CompanyDB | db_Alpha | 50 | — | sí | Match `Sap.CompanyDb` |
| U_ObjType | db_Alpha | 10 | — | sí | |
| U_EntitySet | db_Alpha | 50 | — | sí | |
| U_DocEntry | db_Numeric | 11 | — | sí | int range, suficiente para SAP `DocEntry` |
| U_DocNum | db_Numeric | 11 | — | no | Número humano |
| U_CardCode | db_Alpha | 15 | — | no | Match `OCRD` |
| U_CardName | db_Alpha | 100 | — | no | App trunca a 100 |
| U_DocDate | db_Date | — | — | sí | |
| U_DueDate | db_Date | — | — | no | |
| U_BaseDate | db_Date | — | — | sí | Fecha resuelta usada en cálculo |
| U_DaysCalc | db_Numeric | 5 | — | no | Días desde `U_BaseDate` a `U_DateTime` |
| U_RuleCode | db_Alpha | 50 | — | no | FK lógico → `RULE.Code`. Vacío para skips pre-rule-match. |
| U_Result | db_Alpha | 25 | — | sí | Enum §5.2 (PRD §11) |
| U_Message | db_Alpha | 200 | — | no | Sanitizado (sin CRLF/tab) y truncado por app |
| U_HttpStatus | db_Numeric | 4 | — | no | 200/401/500/etc. |
| U_Attempts | db_Numeric | 4 | 1 | sí | Total intentos (1 = sin retry) |
| U_DurationMs | db_Numeric | 11 | — | no | (Ajuste #4) Tiempo de procesamiento del doc individual. |

**Total: 21 UDFs + 2 SAP defaults.**

**Índices (Ajuste #2 — solo 3 desde MVP):**

| Índice | Único | Campos | Justificación |
|--------|-------|--------|---------------|
| `RunId_IDX` | no | `U_RunId` | "Logs de un run específico" — query operativa principal. |
| `DateTime_IDX` | no | `U_DateTime` | Retención por fecha (`DELETE WHERE U_DateTime < ?`). |
| `DocLookup_IDX` | no | `U_ObjType`, `U_DocEntry` | "¿Procesamos este doc antes?" — útil para retry y deduplicación cross-run. |

**`Action_IDX` se difiere** hasta tener volumen real (Ajuste #2). Si dashboards de "solo failures" se vuelven necesarios y la tabla excede ~500k filas, se añade en migration v2.

**Divergencia explícita con PRD §12.4:**

| Campo PRD | Decisión | Razón |
|-----------|----------|-------|
| `U_Request` (Memo) | **Eliminado** | D-H: raw SAP request va a FileLogger. |
| `U_Response` (Memo) | **Eliminado** | D-H: raw SAP response va a FileLogger. |
| (no existe en PRD) `U_DurationMs` | **Añadido** | Ajuste #4. Métrica de performance per-doc. |

**Política de campos sensibles:**
- `U_Message` SIEMPRE sanitizado en app: strip CRLF + tab, truncado a 200 chars, sufijo `[truncated]` si aplica. Misma lógica que `SapFunctionalException`.
- Si ocurre error y se necesita full payload → `FileLogger` técnico (`smartdoc_YYYYMMDD.log`) con CorrelationId que permite cross-reference.

---

## 10. UDT — `@JCA_DLC_RUN`

**Objetivo:** Lifecycle por ejecución. PRD §12.5.

**Volumen esperado:** 1 fila/run. Diario × años × clientes = miles. Sin riesgos.

**Campos** (alineados con PRD §12.5 + Ajuste #4/#5 + extensiones documentadas):

| Campo | Tipo | Tamaño | Default | Obligatorio (app) | Notas |
|-------|------|--------|---------|-------------------|-------|
| Code | (SAP) | 50 | — | sí | 32-char GUID = RunId |
| Name | (SAP) | 100 | — | sí | |
| U_StartedAt | db_Alpha | 30 | — | sí | ISO 8601 UTC |
| U_FinishedAt | db_Alpha | 30 | — | no | ISO 8601 UTC. Vacío = run en curso o zombi. |
| U_Status | db_Alpha | 12 | `Started` | sí | Enum §5.1 (5 valores). |
| U_Mode | db_Alpha | 12 | `Simulation` | sí | Enum §5.5. Default `Simulation` por D10. |
| U_CompanyDB | db_Alpha | 50 | — | sí | |
| U_Env | db_Alpha | 5 | — | sí | |
| U_Hostname | db_Alpha | 50 | — | sí | `Environment.MachineName` |
| U_TriggeredBy | db_Alpha | 12 | `Manual` | sí | Enum §5.6 |
| U_AppVersion | db_Alpha | 20 | — | sí | (D-E) Semver del Runner |
| U_TotalCandidates | db_Numeric | 8 | 0 | sí | (PRD §12.5) |
| U_TotalClosed | db_Numeric | 8 | 0 | sí | |
| U_TotalSimulated | db_Numeric | 8 | 0 | sí | |
| U_TotalSkipped | db_Numeric | 8 | 0 | sí | |
| U_TotalErrors | db_Numeric | 8 | 0 | sí | |
| U_DurationSec | db_Numeric | 8 | — | no | (PRD §12.5) Solo al cierre del run. |
| U_LastError | db_Alpha | 200 | — | no | (D-E) Última excepción no manejada. Sanitizado. |

**Total: 17 UDFs + 2 SAP defaults.**

**Índices:**

| Índice | Único | Campos | Justificación |
|--------|-------|--------|---------------|
| `Status_IDX` | no | `U_Status` | Detectar runs activos/zombis al inicio (PRD §16). Sin este índice la query degrada con crecimiento. |

**Divergencia explícita con PRD §12.5:**

| Aspecto PRD | Decisión | Razón |
|-------------|----------|-------|
| `U_Status`: "RUNNING, FINISHED, ERROR" (3 valores) | **5 valores** §5.1 (Started/Completed/Failed/Aborted/Stale) | Ajuste #5. Cubre detección de zombis sin término "Zombie". |
| `U_StartedAt`/`U_FinishedAt` "Fecha/Hora" | **`db_Alpha 30` ISO 8601 UTC** | D-A. SAP UDFs no tienen datetime. |
| (PRD no lo lista) `U_Mode` | **Añadido** | Auditoría: "¿este run cerró real o solo simuló?" |
| (PRD no lo lista) `U_Hostname` | **Añadido** | Trazabilidad operativa multi-host. |
| (PRD no lo lista) `U_TriggeredBy` | **Añadido** | Distinguir Manual vs Schedule (PRD §17). |
| (PRD no lo lista) `U_AppVersion` | **Añadido** | (D-E) Correlacionar comportamiento con versión. |
| (PRD no lo lista) `U_LastError` | **Añadido** | (D-E) Diagnóstico rápido sin abrir LOG. |

---

## 11. Política de retención (D-G aprobado, MVP runbook)

**Política MVP:** runbook manual ejecutado por DBA cuarterly o cuando la tabla exceda 1M filas, lo que ocurra primero.

**Tablas con política de retención:**

| Tabla | Política | Comando guía |
|-------|----------|--------------|
| `JCA_DLC_LOG` | Conservar **180 días** de registros. Borrar > 180 días. | `DELETE FROM "@JCA_DLC_LOG" WHERE "U_DateTime" < ?` con timestamp ISO 8601 de hoy menos 180 días. |
| `JCA_DLC_RUN` | Conservar **365 días**. | `DELETE FROM "@JCA_DLC_RUN" WHERE "U_StartedAt" < ?` |
| `JCA_DLC_RULE` | Sin retención (config). | — |
| `JCA_DLC_EXC` | Sin retención (config). | — |
| `JCA_DLC_SCHEMA_VERSION` | Sin retención (1 fila). | — |

**Reglas operativas:**
1. **Solo DBA con permisos elevados puede ejecutar.** Documentado en runbook ops.
2. La app **nunca** elimina filas de LOG/RUN automáticamente en MVP.
3. Antes de cualquier `DELETE`, se hace backup de la tabla.
4. Borrado en lote `LIMIT 10000` para no bloquear el motor HANA.
5. **Nunca borrar runs con `U_Status='Started'`** (run posiblemente activo aunque parezca viejo).

**Diferido a PRO:**
- Comando `--purge-logs --older-than-days 180` ejecutable por el Runner con perfil de instalación.
- Archivado a tabla histórica antes del DELETE.
- Métricas de tamaño de LOG en cada run (alerta a 80% de threshold).

---

## 12. Inventario completo de índices v1

| UDT | Nombre índice | Único | Campos | Crítico para |
|-----|---------------|-------|--------|--------------|
| RULE | `Active_ObjType_IDX` | no | Active, ObjType | Filtro de reglas al iniciar run |
| EXC | `Active_ObjType_IDX` | no | Active, ObjType | Filtro de exclusiones |
| EXC | `ObjType_ExcType_ExcValue_IDX` | no | ObjType, ExcType, ExcValue | Lookup por documento |
| LOG | `RunId_IDX` | no | RunId | Logs de un run |
| LOG | `DateTime_IDX` | no | DateTime | Retención y rangos |
| LOG | `DocLookup_IDX` | no | ObjType, DocEntry | Dedup cross-run |
| RUN | `Status_IDX` | no | Status | Detección zombis |

**Total v1: 7 índices.**

**Índices descartados v1 (justificación):**

| Índice descartado | Tabla | Razón |
|-------------------|-------|-------|
| `Action_IDX` | LOG | Ajuste #2: esperar volumen real. Dashboards no son MVP. |
| `(Environment, StartedAt)_IDX` | RUN | Tabla pequeña, full-scan aceptable en MVP. |
| `EntitySet_IDX` | RULE | Volumen pequeño (<50 filas), full-scan aceptable. |
| `ValidFrom_IDX` / `ValidTo_IDX` | RULE/EXC | Campos PRO, sin queries en MVP. |

---

## 13. Resumen de divergencias respecto al PRD

Cambios deliberados que se apartan del PRD §11/§12:

| # | Divergencia | Origen | Impacto |
|---|-------------|--------|---------|
| 1 | `U_MaxPerRun` → `U_MaxDocumentsPerRun` | D-C | Renombrado nominal. Cero datos productivos. |
| 2 | Timestamps `Fecha/Hora` → `db_Alpha 30` ISO 8601 UTC | D-A | UDFs SAP no tienen datetime. |
| 3 | `LOG.U_Request` y `U_Response` (Memo) eliminados | D-H | Performance + tamaño. Raw payloads → FileLogger. |
| 4 | `RUN.U_Status` 3 valores → 5 valores | Ajuste #5 | Cubre detección de zombis sin "Zombie". |
| 5 | Añadido `RUN.U_Mode/Hostname/TriggeredBy/AppVersion/LastError` | D-E + observabilidad | Extensiones aditivas. |
| 6 | Añadido `LOG.U_DurationMs` | Ajuste #4 | Métrica per-doc. |
| 7 | `LOG.Code` formato `LOG_{run}_{seq}` → 32-char GUID | Ajuste #6 | Sin lookups, sin race conditions. |
| 8 | `CorrelationId` 8 chars híbrido → 32 chars siempre | Ajuste #3 | Estandarización GUID-N. |
| 9 | `@JCA_DLC_PAY` (PRD §12.2) **NO en v1** | D01 | Diferido a PRO. |
| 10 | `@JCA_DLC_SCHEMA_VERSION` (no estaba en PRD) **SÍ en v1** | Ajuste #1 | Evita versioning retroactivo. |

---

## 14. Riesgos schema (revisados con diseño cerrado)

| # | Riesgo | Severidad | Mitigación implementada |
|---|--------|-----------|-------------------------|
| R1 | Cambio de tipo de UDF en migration | Crítica | Política aditiva. Code review obligatorio. |
| R2 | Borrado accidental de tabla con datos | Crítica | Installer **nunca** borra. Solo crea. |
| R3 | Run zombi bloquea siguiente ejecución | Alta | Status `Stale` + threshold + Status_IDX. |
| R4 | Tabla LOG explosiva | Alta | Retention runbook §11 + 3 índices clave. |
| R5 | Conflicto `JCA_DLC_*` con UDTs pre-existentes | Media | Prefijo único, check pre-install. |
| R6 | App escribe `Result` fuera del enum §5.2 | Media | Validación app obligatoria (SAP no enforce). |
| R7 | Timestamp con timezone local en lugar de UTC | Media | Convención §2.4 + tests unitarios. |
| R8 | Single-row invariant SCHEMA_VERSION roto | Baja | Upsert idempotente en installer. |
| R9 | `U_Message` sin truncar → 400 al insertar | Media | Sanitizer + truncate en app antes de POST. |
| R10 | `Y/N` minúsculo (`y`/`n`) inconsistente | Baja | App siempre escribe uppercase. |
| R11 | Multi-cliente con UDOs distintos en `ObjType` | Media | `db_Alpha 10` acomoda hasta 10 dígitos. Validar por cliente. |
| R12 | Service Layer rate-limit en bulk install (~30 POSTs) | Media | Pausa 50–100ms entre operaciones de metadata (al diseñar installer). |

---

## 15. PRD validation checklist (D-I)

Validación literal contra PRD §11 y §12. **Marcar OK / DIVERGE_<doc-section>.**

### 15.1 Estados PRD §11 vs `LOG.U_Result`

| PRD §11 | Adoptado | Estado |
|---------|----------|--------|
| `CLOSED` | `CLOSED` | ✅ literal |
| `SIMULATED` | `SIMULATED` | ✅ literal |
| `SKIPPED_NOT_EXPIRED` | `SKIPPED_NOT_EXPIRED` | ✅ literal |
| `SKIPPED_EXCLUDED` | `SKIPPED_EXCLUDED` | ✅ literal |
| `SKIPPED_HAS_TARGET` | `SKIPPED_HAS_TARGET` | ✅ literal |
| `SKIPPED_RECENT_ACTIVITY` | `SKIPPED_RECENT_ACTIVITY` | ✅ literal |
| `SKIPPED_REQUIRES_APPROVAL` | `SKIPPED_REQUIRES_APPROVAL` | ✅ literal |
| `ERROR` | `ERROR` | ✅ literal |

### 15.2 `JCA_DLC_RULE` PRD §12.1 vs schema

| Campo PRD | Schema v1 | Estado |
|-----------|-----------|--------|
| `Code` | `Code` | ✅ |
| `Name` | `Name` | ✅ |
| `U_Active` Y/N | `U_Active` `db_Alpha 1` | ✅ |
| `U_Priority` Numérico | `U_Priority` `db_Numeric 4` | ✅ |
| `U_ObjType` Alfa | `U_ObjType` `db_Alpha 10` | ✅ |
| `U_EntitySet` Alfa | `U_EntitySet` `db_Alpha 50` | ✅ |
| `U_BaseDate` Alfa | `U_BaseDate` `db_Alpha 15` (enum §5.3) | ✅ |
| `U_GraceDays` Numérico | `U_GraceDays` `db_Numeric 4` | ✅ |
| `U_Simulation` Y/N | `U_Simulation` `db_Alpha 1` | ✅ |
| `U_MaxPerRun` Numérico | `U_MaxDocumentsPerRun` `db_Numeric 6` | ⚠️ DIVERGE_2.7 D-C (renombrado) |
| `U_OnlyNoTarget` Y/N | `U_OnlyNoTarget` `db_Alpha 1` | ✅ |
| `U_CheckLines` Y/N | `U_CheckLines` `db_Alpha 1` | ✅ |
| `U_CheckUpdate` Y/N | `U_CheckUpdate` `db_Alpha 1` | ✅ |
| `U_InactiveDays` Numérico | `U_InactiveDays` `db_Numeric 4` | ✅ |
| `U_MinTotal` Importe | `U_MinTotal` `db_Float` | ✅ |
| `U_MaxTotal` Importe | `U_MaxTotal` `db_Float` | ✅ |
| `U_ReqApproval` Y/N | `U_ReqApproval` `db_Alpha 1` | ✅ |
| `U_CloseComment` Texto | `U_CloseComment` `db_Alpha 100` | ✅ (texto = alpha) |
| `U_ValidFrom` Fecha | `U_ValidFrom` `db_Date` | ✅ |
| `U_ValidTo` Fecha | `U_ValidTo` `db_Date` | ✅ |

### 15.3 `JCA_DLC_EXC` PRD §12.3 vs schema

| Campo PRD | Schema v1 | Estado |
|-----------|-----------|--------|
| `Code` | `Code` | ✅ |
| `Name` | `Name` | ✅ |
| `U_Active` | `U_Active` | ✅ |
| `U_ObjType` | `U_ObjType` | ✅ (`ALL` literal aceptado) |
| `U_ExcType` (CardCode/GroupCode/SlpCode/BPLId) | `U_ExcType` `db_Alpha 15` | ✅ literal |
| `U_ExcValue` | `U_ExcValue` `db_Alpha 30` | ✅ |
| `U_Reason` Texto | `U_Reason` `db_Alpha 100` | ✅ (texto = alpha 100, no memo) |
| (no en PRD) `U_ValidFrom` | `U_ValidFrom` `db_Date` | ⚠️ DIVERGE_13#5 D-E (añadido reservado) |
| (no en PRD) `U_ValidTo` | `U_ValidTo` `db_Date` | ⚠️ DIVERGE_13#5 D-E (añadido reservado) |

### 15.4 `JCA_DLC_LOG` PRD §12.4 vs schema

| Campo PRD | Schema v1 | Estado |
|-----------|-----------|--------|
| `Code` | `Code` | ✅ |
| `Name` | `Name` | ✅ |
| `U_RunId` | `U_RunId` `db_Alpha 32` | ✅ |
| `U_DateTime` Fecha/Hora | `U_DateTime` `db_Alpha 30` ISO 8601 UTC | ⚠️ DIVERGE_2.4 D-A |
| `U_Env` | `U_Env` `db_Alpha 5` | ✅ |
| `U_CompanyDB` | `U_CompanyDB` `db_Alpha 50` | ✅ |
| `U_ObjType` | `U_ObjType` | ✅ |
| `U_EntitySet` | `U_EntitySet` | ✅ |
| `U_DocEntry` | `U_DocEntry` | ✅ |
| `U_DocNum` | `U_DocNum` | ✅ |
| `U_CardCode` | `U_CardCode` | ✅ |
| `U_CardName` Texto | `U_CardName` `db_Alpha 100` | ✅ |
| `U_DocDate` | `U_DocDate` | ✅ |
| `U_DueDate` | `U_DueDate` | ✅ |
| `U_BaseDate` | `U_BaseDate` `db_Date` (resuelto) | ✅ |
| `U_DaysCalc` | `U_DaysCalc` | ✅ |
| `U_RuleCode` | `U_RuleCode` | ✅ |
| `U_Result` | `U_Result` (enum §5.2) | ✅ |
| `U_Message` Texto | `U_Message` `db_Alpha 200` | ✅ (texto = alpha 200) |
| `U_HttpStatus` | `U_HttpStatus` | ✅ |
| `U_Attempts` | `U_Attempts` | ✅ |
| `U_Request` Memo | **eliminado** | ❌ DIVERGE_13#3 D-H |
| `U_Response` Memo | **eliminado** | ❌ DIVERGE_13#3 D-H |
| (no en PRD) `U_DurationMs` | `U_DurationMs` `db_Numeric 11` | ⚠️ DIVERGE_13#6 Ajuste #4 (añadido) |

### 15.5 `JCA_DLC_RUN` PRD §12.5 vs schema

| Campo PRD | Schema v1 | Estado |
|-----------|-----------|--------|
| `Code` | `Code` | ✅ |
| `Name` | `Name` | ✅ |
| `U_StartedAt` Fecha/Hora | `U_StartedAt` `db_Alpha 30` ISO 8601 | ⚠️ DIVERGE_2.4 D-A |
| `U_FinishedAt` Fecha/Hora | `U_FinishedAt` `db_Alpha 30` ISO 8601 | ⚠️ DIVERGE_2.4 D-A |
| `U_Status` (RUNNING/FINISHED/ERROR) | `U_Status` (Started/Completed/Failed/Aborted/Stale) | ⚠️ DIVERGE_5.1 Ajuste #5 |
| `U_CompanyDB` | `U_CompanyDB` | ✅ |
| `U_Env` | `U_Env` | ✅ |
| `U_TotalCandidates` | `U_TotalCandidates` | ✅ |
| `U_TotalClosed` | `U_TotalClosed` | ✅ |
| `U_TotalSimulated` | `U_TotalSimulated` | ✅ |
| `U_TotalSkipped` | `U_TotalSkipped` | ✅ |
| `U_TotalErrors` | `U_TotalErrors` | ✅ |
| `U_DurationSec` | `U_DurationSec` | ✅ |
| (no en PRD) `U_Mode` | `U_Mode` | ⚠️ DIVERGE_13#5 D-E (añadido) |
| (no en PRD) `U_Hostname` | `U_Hostname` | ⚠️ DIVERGE_13#5 (añadido) |
| (no en PRD) `U_TriggeredBy` | `U_TriggeredBy` | ⚠️ DIVERGE_13#5 (añadido) |
| (no en PRD) `U_AppVersion` | `U_AppVersion` | ⚠️ DIVERGE_13#5 D-E (añadido) |
| (no en PRD) `U_LastError` | `U_LastError` | ⚠️ DIVERGE_13#5 D-E (añadido) |

### 15.6 Decisión final pendiente

✅ Cero `❌ DIVERGE` no aprobados (los dos `❌` corresponden a D-H ya aprobado).
⚠️ Las `DIVERGE_*` listadas son extensiones aditivas o renombramientos; todas tienen aprobación explícita en este documento.
✅ Cero campos PRD silenciosamente omitidos (`U_Request`/`U_Response` están explícitamente eliminados con justificación).

**Pendiente única decisión final del usuario:** confirmar que esta tabla de validación captura fielmente el PRD y aprobar pasar al diseño del installer.

---

## 16. Próximos pasos

1. **Revisión del usuario** del checklist §15 contra el PRD físico.
2. Si OK → **diseño del installer** (`docs/08_INSTALLER_DESIGN.md`):
   - Comando `--install-schema` en Runner.
   - Variables de entorno (`SAP_INSTALL_PASSWORD` separada).
   - Idempotencia check-then-create (Service Layer GET antes de POST).
   - JSON descriptors per UDT.
   - Pause inter-POST para evitar rate-limit (R12).
   - Upsert de `JCA_DLC_SCHEMA_VERSION` al final.
3. Cuando el installer esté diseñado → **escribir descriptors `.json` en `scripts/install/schema/v1/`**.
4. Cuando los descriptors estén escritos → **implementar el installer en código**.

**Política de no-regresión:** ningún campo de este schema v1 se modifica/borra sin abrir un nuevo `docs/NN_SCHEMA_VN.md` y migration aditiva.
