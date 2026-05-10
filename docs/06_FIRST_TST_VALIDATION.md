# Primera Validación Real — SAP Service Layer TST

**Objetivo:** Ejecutar `--validate-only` contra el SAP Service Layer real de TST.  
**Alcance:** Login + verificación de UDTs. **No cierra documentos. No lee documentos abiertos.**

---

## Prerrequisitos

- .NET 8 SDK instalado en la máquina de ejecución.
- Acceso de red al Service Layer de TST (puerto 50000).
- Credenciales de SAP provistos por el administrador del sistema.

---

## Paso 1 — Crear la variable de entorno con el password

El password **nunca se escribe en ningún archivo**. Solo vive en la variable de entorno del proceso.

**PowerShell (sesión actual):**
```powershell
$env:SAP_AUTOCLOSE_PASSWORD = "tu-password-aqui"
```

**PowerShell (permanente — usuario actual):**
```powershell
[System.Environment]::SetEnvironmentVariable(
    "SAP_AUTOCLOSE_PASSWORD",
    "tu-password-aqui",
    "User"
)
```

**CMD:**
```cmd
set SAP_AUTOCLOSE_PASSWORD=tu-password-aqui
```

> Verificar: `$env:SAP_AUTOCLOSE_PASSWORD` (PowerShell) o `echo %SAP_AUTOCLOSE_PASSWORD%` (CMD).

---

## Paso 2 — Crear `appsettings.TST.json` local

Este archivo **no está en Git** (está en `.gitignore`). Créalo copiando la plantilla:

```powershell
Copy-Item src/SmartDocControl.Runner/appsettings.TST.example.json `
          src/SmartDocControl.Runner/appsettings.TST.json
```

Luego edítalo con los valores reales del entorno TST:

```json
{
  "Sap": {
    "BaseUrl": "https://<SAP_SERVICE_LAYER_BASE_URL>/b1s/v1/",
    "CompanyDb": "<COMPANY_DB>",
    "Username": "<SERVICE_LAYER_USER>",
    "IgnoreSslErrors": true,
    "TimeoutSeconds": 60
  },
  "Execution": {
    "DefaultSimulation": true,
    "MaxRetries": 2,
    "RetryDelaySeconds": 2,
    "StaleRunThresholdHours": 4
  },
  "Logging": {
    "LogPath": "C:\\SmartDocControl\\Logs\\",
    "PendingFunctionalLogPath": "C:\\SmartDocControl\\PendingLogs\\",
    "DebugMode": false
  }
}
```

> `IgnoreSslErrors: true` es aceptable en TST. Está **prohibido** en PRD (validado por StartupValidator, código `SEC-001`).  
> El campo `PasswordEnvironmentVariable` no se pone aquí — el valor por defecto `SAP_AUTOCLOSE_PASSWORD` ya está en `appsettings.json`.

---

## Paso 3 — Ejecutar validación con `dotnet run`

Desde la raíz del repositorio:

```powershell
dotnet run --project src/SmartDocControl.Runner -- --environment TST --validate-only
```

O con el alias:

```powershell
dotnet run --project src/SmartDocControl.Runner -- --environment TST --dry-run
```

---

## Paso 4 — Publicar y ejecutar la DLL

**Publicar:**
```powershell
dotnet publish src/SmartDocControl.Runner `
    --configuration Release `
    --output ./publish/TST
```

El archivo `appsettings.TST.json` local se copia automáticamente al output si existe en el directorio del proyecto.

**Ejecutar la DLL publicada:**
```powershell
dotnet publish/TST/SmartDocControl.Runner.dll --environment TST --validate-only
```

---

## Salida esperada — Validación exitosa

```
Smart Document Control — SAP Business One
  Environment : TST
  SAP         : https://<SAP_SERVICE_LAYER_BASE_URL>:50000
  Run ID      : A1B2C3D4

Running startup validation...

Validation: OK
  Validated at: 2026-05-08 10:30:00 UTC
```

El proceso termina con **exit code 0**.

Los logs técnicos quedan en `C:\SmartDocControl\Logs\smartdoc_YYYYMMDD.log`.

---

## Diagnóstico — Falló SSL / certificado no confiable

**Síntoma:** Error de conexión antes del login, o excepción SSL.

**Causa:** El certificado del Service Layer de TST es autofirmado.

**Solución:** Asegurarse de que `appsettings.TST.json` tiene `"IgnoreSslErrors": true`.

**Verificar en la salida:**
```
[ERR] SEC-001: D05: IgnoreSslErrors=true is forbidden in PRD environment.
```
Este error aparece **solo** si `Environment = PRD`. En TST no es un error.

---

## Diagnóstico — Falló el login (401 / credenciales)

**Síntoma:**
```
[ERR] SAP-CONN-001: SAP Service Layer login failed: SAP login failed with HTTP 401.
```

**Checklist:**
1. ¿Está `SAP_AUTOCLOSE_PASSWORD` correctamente establecida? (`$env:SAP_AUTOCLOSE_PASSWORD`)
2. ¿El `Username` en `appsettings.TST.json` es correcto?
3. ¿El `CompanyDb` en `appsettings.TST.json` es correcto?
4. ¿La `BaseUrl` apunta al host:puerto correcto y termina en `/b1s/v1/`?
5. ¿El usuario tiene acceso al Service Layer? (validar con Postman usando las mismas credenciales)

---

## Diagnóstico — Faltan UDTs (`UDT-001`)

**Síntoma:**
```
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_RULE' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_EXC' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_LOG' not found in SAP.
[ERR] UDT-001: D06: Required UDT '@JCA_DLC_RUN' not found in SAP.
```

**Causa:** Las User Defined Tables del módulo no han sido creadas en la empresa SAP de TST.

**Acción:** Crear las UDTs antes de ejecutar en modo producción. Esto es un prerequisito de instalación del módulo (fuera del scope de esta validación inicial de conectividad).

> Si el objetivo es solo validar login y conectividad, los errores UDT-001 son esperados y no indican problema de red ni de credenciales.

---

## Diagnóstico — No se encuentra `appsettings.TST.json`

**Síntoma:** La salida usa valores genéricos de `appsettings.json` (URL base `sap-server`) o error de conexión.

**Causa:** El archivo `appsettings.TST.json` no existe en el directorio de salida.

**Solución:**
1. Verificar que el archivo existe en `src/SmartDocControl.Runner/appsettings.TST.json`.
2. Si se usa `dotnet run`: el SDK lo copia automáticamente al output.
3. Si se usa la DLL publicada: re-ejecutar `dotnet publish` después de crear el archivo.

---

## Confirmación de scope — Esta prueba NO cierra documentos

Esta validación ejecuta **únicamente**:
1. `POST /Login` — autenticación SAP
2. `GET /UserTablesMD?$filter=...` — verificación de UDTs
3. `POST /Logout` — cierre de sesión

**No se ejecuta ninguna operación de cierre de documentos.**  
No se llama a ningún endpoint de tipo `PATCH /{EntitySet}({DocEntry})/Close`.  
No se leen documentos abiertos.  
No se modifica ningún dato en SAP.

El flag `DefaultSimulation: true` en `appsettings.TST.json` garantiza adicionalmente que cualquier operación futura que se habilite en el runner operará en modo simulación por defecto.
