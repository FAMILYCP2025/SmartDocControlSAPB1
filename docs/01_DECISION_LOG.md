# Decision Log — Smart Document Control for SAP Business One

**Versión PRD:** 1.0  
**Fecha de decisiones:** 2026-05-04  
**Estado:** Aprobado — válido para todo el ciclo MVP

Estas decisiones resuelven las ambigüedades técnicas y funcionales detectadas durante el análisis del PRD antes de comenzar el desarrollo. Son parte oficial del diseño y deben consultarse antes de implementar cualquier componente.

---

## D01 — UDT @JCA_DLC_PAY: solo referencia PRO

**Contexto:** La sección 12.2 del PRD define la UDT `@JCA_DLC_PAY` para reglas por condición de pago. La sección 20 (Roadmap PRO) lista esta funcionalidad como futura.

**Decisión:** Crear la UDT `@JCA_DLC_PAY` únicamente como preparación documental (script de referencia en `scripts/udt_JCA_DLC_PAY_PRO.http`). No implementar lógica activa de condición de pago en el MVP. El motor de reglas MVP utiliza exclusivamente `@JCA_DLC_RULE`.

**Impacto en código:** La UDT no se valida en startup. No existe ningún repositorio ni servicio que la lea en MVP.

---

## D02 — Lock dual: archivo local + registro RUNNING en UDT

**Contexto:** La sección 16 del PRD ofrece dos mecanismos alternativos para prevenir ejecuciones simultáneas: archivo lock local o registro `RUNNING` en `@JCA_DLC_RUN`.

**Decisión:** Implementar ambos mecanismos en conjunto:

1. **Archivo lock local** (`SmartDocControl.lock`): guardia rápida al arranque, sin depender de SAP.
2. **Registro RUNNING en `@JCA_DLC_RUN`**: auditoría funcional visible desde SAP B1.

**Detección de ejecuciones colgadas (STALE):** Si existe un registro con `U_Status = RUNNING` cuya antigüedad supera el valor configurado en `Execution:StaleRunThresholdHours`, el sistema lo marcará como `STALE` y permitirá una nueva ejecución. El archivo lock también se eliminará si supera ese umbral.

**Impacto en código:** `LockManager` combina ambos mecanismos. El umbral STALE es configurable en `appsettings.json`.

---

## D03 — U_OnlyNoTarget: validación por líneas vía Service Layer

**Contexto:** El campo `U_OnlyNoTarget = Y` en una regla indica que solo se debe cerrar el documento si no tiene documento destino. No existe un campo de cabecera estándar en SAP B1 que indique esto de forma directa.

**Decisión:** Validar documento destino mediante expansión de líneas en el GET de Service Layer:

```
GET /b1s/v1/{EntitySet}({DocEntry})?$expand=DocumentLines&$select=DocumentLines/TargetType,DocumentLines/TargetEntry
```

Si alguna línea contiene `TargetType` o `TargetEntry` con valor distinto de nulo o vacío, el documento se marca como `SKIPPED_HAS_TARGET`.

**Tolerancia:** El nombre exacto del campo (`TargetType`, `TargetEntry`) debe verificarse contra el ambiente TST antes de activar la regla en producción. La lógica debe ser defensiva ante campos ausentes en la respuesta.

**Impacto en código:** `RuleEngine` realiza este check solo si `U_OnlyNoTarget = Y`. La consulta de líneas se incorpora al `DocumentRepository` condicionada por la regla.

---

## D04 — U_ReqApproval: estado reservado para versión PRO

**Contexto:** El campo `U_ReqApproval = Y` implica detectar si un documento tiene un workflow de aprobación activo en SAP B1, lo que involucra tablas internas (`OWDD`/`WDD1`) de acceso complejo vía Service Layer.

**Decisión:** No implementar validación real de workflow de aprobación en MVP. El estado `SKIPPED_REQUIRES_APPROVAL` queda reservado en el enum pero su lógica no está activa.

**Comportamiento en MVP:** Si una regla tiene `U_ReqApproval = Y`, el sistema omite el documento y registra en log que la validación de aprobación avanzada está pendiente para versión PRO.

**Impacto en código:** `RuleEngine` detecta `U_ReqApproval = Y` y retorna `SKIPPED_REQUIRES_APPROVAL` sin realizar ninguna llamada adicional a Service Layer.

---

## D05 — IgnoreSslErrors: configurable, prohibido en PRD

**Contexto:** Los servidores SAP B1 en entornos de desarrollo y prueba suelen usar certificados SSL auto-firmados, lo que requiere deshabilitar la validación TLS en el `HttpClient`.

**Decisión:** Agregar el campo `Sap:IgnoreSslErrors` (booleano) en `appsettings.json`, con valor por defecto `false`.

**Regla de seguridad obligatoria:** Si `Execution:Environment = PRD` e `IgnoreSslErrors = true`, la aplicación debe **abortar la ejecución** al inicio con un mensaje de error claro. No está permitido ignorar SSL en producción bajo ninguna circunstancia.

| Ambiente | IgnoreSslErrors permitido |
|----------|--------------------------|
| DEV      | Sí                       |
| TST      | Sí                       |
| PRD      | No — aborta ejecución    |

**Impacto en código:** `StartupValidator` verifica esta combinación antes de intentar el login a Service Layer.

---

## D06 — La aplicación no crea UDTs; valida existencia al arranque

**Contexto:** Las UDTs deben ser creadas por el consultor SAP B1 usando los scripts de referencia en `scripts/`. La aplicación no tiene permisos ni responsabilidad de crearlas.

**Decisión:** Al iniciar, la aplicación valida la existencia de las UDTs críticas mediante GET a Service Layer. Si alguna falta, aborta con mensaje descriptivo indicando cuál UDT no fue encontrada.

**UDTs que se validan en startup (MVP):**

| UDT | Clasificación | Acción si falta |
|-----|--------------|-----------------|
| `@JCA_DLC_RULE` | CRÍTICA | Abortar |
| `@JCA_DLC_EXC` | CRÍTICA | Abortar |
| `@JCA_DLC_LOG` | CRÍTICA | Abortar |
| `@JCA_DLC_RUN` | CRÍTICA | Abortar |
| `@JCA_DLC_PAY` | PRO — no validar | Ignorar |

**Scripts de referencia:** `scripts/udt_JCA_DLC_*.json` contienen las llamadas Service Layer necesarias para crear cada UDT con sus campos.

**Impacto en código:** `StartupValidator` realiza `GET /b1s/v1/UserTablesMD?$filter=TableName eq 'JCA_DLC_*'` para cada UDT crítica.

---

## D07 — Fallo en log funcional tras cierre exitoso: PendingLogs

**Contexto:** Si un documento se cierra correctamente en SAP pero la escritura en `@JCA_DLC_LOG` falla, el cierre ya es irreversible. Marcar el documento como ERROR sería incorrecto funcionalmente.

**Decisión:** Aplicar la siguiente secuencia de recuperación:

1. Reintentar la escritura en `@JCA_DLC_LOG` según `Execution:MaxRetries`.
2. Si todos los reintentos fallan: escribir entrada crítica en el log técnico local.
3. Guardar el payload del log como archivo JSON en `Logging:PendingFunctionalLogPath` (`C:\SmartDocControl\PendingLogs\`), con nombre `{RunId}_{DocEntry}_{timestamp}.json`.
4. Continuar con el siguiente documento.

**El cierre NO se marca como ERROR.** El cierre ocurrió en SAP correctamente. El fallo es exclusivamente de trazabilidad.

**Impacto en código:** `LogRepository` implementa esta lógica de fallback. `PendingFunctionalLogPath` es configurable en `appsettings.json`.

---

## D08 — --documentType filtra reglas UDT, no las reemplaza

**Contexto:** El parámetro CLI `--documentType` permite limitar la ejecución a un tipo de documento específico. Era ambiguo si este parámetro actuaba sobre las reglas existentes en UDT o si las sobreescribía.

**Decisión:** El parámetro `--documentType` **filtra** las reglas activas en `@JCA_DLC_RULE` por el campo `U_EntitySet`. No inventa reglas ni sobreescribe configuración.

**Ejemplo:**
```
SmartDocControl.Runner.exe --documentType Quotations
```
Solo procesa las reglas de `@JCA_DLC_RULE` donde `U_EntitySet = 'Quotations'` y `U_Active = 'Y'`.

**Si no existen reglas activas** para el `EntitySet` indicado, la aplicación aborta con mensaje claro: `No active rules found for EntitySet 'Quotations'.`

**Impacto en código:** `ConfigurationRepository` aplica el filtro antes de retornar las reglas. `ExecutionService` verifica que la lista no esté vacía.

---

## D09 — MaxRetries: errores transitorios; 401 hace re-login una vez

**Contexto:** El campo `Execution:MaxRetries` no especificaba a qué tipo de errores aplicaba ni cómo tratar la expiración de sesión (HTTP 401).

**Decisión:** `MaxRetries` aplica únicamente a **errores transitorios** de Service Layer. No aplica a errores funcionales de negocio.

**Errores que activan reintento:**

| Código HTTP | Motivo |
|-------------|--------|
| 408 | Request Timeout |
| 429 | Too Many Requests |
| 500 | Internal Server Error |
| 502 | Bad Gateway |
| 503 | Service Unavailable |
| 504 | Gateway Timeout |
| — | Timeout de conexión |
| — | Connection reset |

**Errores que NO se reintentan:**

| Código HTTP | Motivo |
|-------------|--------|
| 400 | Bad Request — error en datos enviados |
| 403 | Forbidden — permiso insuficiente |
| 404 | Not Found — recurso inexistente |
| Cualquier error de negocio SAP en cuerpo de respuesta |

**Tratamiento especial de 401 (sesión expirada):**
1. Intentar re-login **una sola vez**.
2. Si el re-login es exitoso, reintentar la operación original.
3. Si el re-login falla, abortar el documento actual con estado `ERROR` y continuar con el siguiente.

**Impacto en código:** `ServiceLayerClient` implementa la política de retry. La lista de códigos transitorios es configurable vía `Execution:RetryableHttpStatusCodes` en `appsettings.json`.

---

## Control de cambios

| Versión | Fecha | Descripción |
|---------|-------|-------------|
| 1.0 | 2026-05-04 | Versión inicial — 9 decisiones aprobadas previo al desarrollo MVP |
