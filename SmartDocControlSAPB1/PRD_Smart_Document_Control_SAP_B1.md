# PRD – Smart Document Control for SAP Business One

**Producto:** Smart Document Control for SAP Business One  
**Versión del PRD:** 1.0  
**Tipo de solución:** Worker/Console App .NET 8 ejecutado desde Windows Task Scheduler  
**ERP objetivo:** SAP Business One v10 sobre HANA  
**Integración:** SAP Business One Service Layer  
**Ambientes:** DEV / TST / PRD  
**Autor funcional:** Consultor SAP Business One externo  

---

## 1. Resumen ejecutivo

Smart Document Control for SAP Business One es una solución de automatización orientada al control del ciclo de vida de documentos comerciales abiertos en SAP Business One.

Su objetivo inicial es identificar y cerrar de forma controlada documentos vencidos o antiguos, utilizando reglas configurables en tablas de usuario SAP B1. El cierre se realizará mediante Service Layer y quedará auditado mediante logs funcionales dentro de SAP y logs técnicos locales.

La primera versión debe operar como una aplicación .NET 8 tipo Worker/Console ejecutada diariamente a las 08:00 AM mediante Windows Task Scheduler. El sistema debe soportar modo simulación para validar candidatos antes de realizar cierres reales.

La visión del producto no es solo cerrar documentos, sino evolucionar hacia un motor configurable de reglas para saneamiento documental, control operativo y automatización SAP B1.

---

## 2. Problema de negocio

En muchos clientes SAP Business One quedan documentos comerciales abiertos durante semanas o meses sin gestión activa. Esto genera problemas como:

- Reportes de venta pendiente poco confiables.
- Pedidos abiertos que distorsionan seguimiento comercial.
- Ofertas antiguas que siguen apareciendo como oportunidades vigentes.
- Órdenes de compra antiguas que contaminan análisis de abastecimiento.
- Dificultad para distinguir documentos realmente activos de documentos olvidados.
- Dependencia del cierre manual por parte de usuarios.
- Falta de trazabilidad sobre qué documentos se cerraron, cuándo y bajo qué criterio.

---

## 3. Objetivos del producto

### 3.1 Objetivo general

Construir una solución robusta, mantenible y parametrizable para cerrar automáticamente documentos comerciales abiertos en SAP Business One, aplicando reglas de negocio configurables y dejando trazabilidad completa.

### 3.2 Objetivos específicos

- Automatizar la revisión diaria de documentos abiertos.
- Aplicar reglas de cierre por tipo de documento.
- Permitir modo simulación antes del cierre real.
- Registrar resultados por documento en una UDT de log.
- Registrar resumen de ejecución por corrida.
- Evitar que un error individual detenga todo el proceso.
- Operar inicialmente desde Windows Task Scheduler.
- Diseñar la arquitectura para evolución futura a versión PRO.

---

## 4. Alcance funcional MVP

La versión MVP debe incluir los siguientes documentos:

| Documento | Tabla SAP | EntitySet Service Layer | ObjType |
|---|---|---|---|
| Oferta de venta | OQUT | Quotations | 23 |
| Orden de venta | ORDR | Orders | 17 |
| Oferta de compra | OPQT | PurchaseQuotations | 540000006 |
| Orden de compra | OPOR | PurchaseOrders | 22 |

Funcionalidades MVP:

- Lectura de reglas desde UDT.
- Identificación de documentos abiertos.
- Cálculo de días según fecha base configurada.
- Modo simulación.
- Cierre real vía Service Layer cuando corresponda.
- Log funcional en SAP.
- Log técnico local.
- Límite máximo de documentos por ejecución.
- Prevención de doble ejecución simultánea.
- Procesamiento secuencial, no paralelo.

---

## 5. Fuera de alcance MVP

No forman parte del MVP inicial:

- Portal web de administración.
- Dashboard Power BI.
- Notificaciones automáticas por Teams, correo o WhatsApp.
- Aprobaciones previas por workflow.
- Motor avanzado por vendedor, sucursal o grupo de socio.
- Multiempresa avanzada.
- Instalador formal.
- SaaS centralizado.
- Reapertura automática de documentos.
- Modificación directa en base de datos SAP.
- Uso de DI API.

Estas funciones podrán considerarse en versión PRO o roadmap futuro.

---

## 6. Principios de diseño

La solución debe respetar estos principios:

1. Primero simulación, luego cierre real.
2. Nunca modificar documentos directamente por SQL.
3. Todo cierre debe ejecutarse vía Service Layer.
4. Todo documento evaluado debe tener trazabilidad.
5. Un error individual no debe detener la ejecución completa.
6. Las reglas deben vivir en SAP B1 mediante UDT.
7. Las credenciales no deben quedar hardcodeadas.
8. El diseño debe permitir extender nuevos documentos y reglas.
9. El sistema debe poder operar en DEV, TST y PRD.
10. El MVP debe ser simple, pero no improvisado.

---

## 7. Flujo funcional del proceso

1. Windows Task Scheduler ejecuta la aplicación a las 08:00 AM.
2. La aplicación genera un RunId único.
3. Se valida que no exista otra ejecución activa.
4. Se inicia sesión en Service Layer.
5. Se leen reglas activas desde UDT.
6. Se leen exclusiones activas.
7. Se buscan documentos abiertos candidatos.
8. Se evalúa cada documento contra el motor de reglas.
9. Si está en simulación, se registra como SIMULATED.
10. Si corresponde cierre real, se ejecuta POST /Close vía Service Layer.
11. Se registra log individual por documento.
12. Se actualiza resumen de ejecución.
13. Se cierra sesión Service Layer.
14. Finaliza el proceso.

---

## 8. Arquitectura técnica

### 8.1 Tipo de aplicación

Aplicación .NET 8 tipo Worker/Console ejecutada en Windows. La ejecución diaria se controla mediante Windows Task Scheduler.

### 8.2 Capas recomendadas

```text
src/
  SmartDocControl.Domain/
  SmartDocControl.Application/
  SmartDocControl.Infrastructure/
  SmartDocControl.Runner/

tests/
  SmartDocControl.Tests/
```

### 8.3 Responsabilidad por capa

| Capa | Responsabilidad |
|---|---|
| Domain | Entidades y reglas puras del negocio |
| Application | Casos de uso, orquestación y motor de reglas |
| Infrastructure | Service Layer, UDT, logs, configuración externa |
| Runner | Entrada de consola, parámetros y ejecución programada |
| Tests | Pruebas unitarias del motor de reglas y servicios críticos |

---

## 9. Componentes principales

| Componente | Responsabilidad |
|---|---|
| ExecutionService | Orquestar la ejecución completa |
| ServiceLayerClient | Login, logout, GET, POST, manejo de sesión |
| ConfigurationRepository | Leer reglas desde UDT |
| DocumentRepository | Buscar documentos abiertos |
| RuleEngine | Decidir si un documento se cierra, se simula u omite |
| DocumentProcessor | Procesar documentos secuencialmente |
| LogRepository | Registrar logs en UDT |
| LockManager | Evitar doble ejecución |
| SummaryService | Consolidar métricas de ejecución |

---

## 10. Reglas de negocio MVP

### 10.1 Regla base

Un documento será candidato a cierre si:

- Está abierto.
- Tiene una regla activa para su tipo de documento.
- Supera los días de holgura configurados.
- No está excluido.
- No excede restricciones configuradas.

### 10.2 Fecha base

La regla debe permitir definir qué fecha se usa para calcular antigüedad:

- DocDate
- TaxDate
- DocDueDate
- UpdateDate

### 10.3 Modo simulación

Si la regla está en simulación, el sistema no debe cerrar el documento. Debe registrar el resultado como SIMULATED.

### 10.4 Cierre real

Si la regla no está en simulación y el documento cumple las condiciones, debe ejecutarse:

```http
POST /b1s/v1/{EntitySet}({DocEntry})/Close
Body: {}
```

Ejemplo:

```http
POST /b1s/v1/Quotations(123)/Close
Body: {}
```

### 10.5 Límite por ejecución

Cada regla debe permitir definir un máximo de documentos por ejecución para evitar cierres masivos accidentales.

---

## 11. Estados de resultado

El sistema debe usar estados estandarizados:

| Estado | Descripción |
|---|---|
| CLOSED | Documento cerrado correctamente |
| SIMULATED | Documento candidato, pero no cerrado por simulación |
| SKIPPED_NOT_EXPIRED | Documento aún no supera días de holgura |
| SKIPPED_EXCLUDED | Documento excluido por regla |
| SKIPPED_HAS_TARGET | Documento omitido por tener documento destino |
| SKIPPED_RECENT_ACTIVITY | Documento omitido por actualización reciente |
| SKIPPED_REQUIRES_APPROVAL | Documento requiere aprobación previa |
| ERROR | Error técnico o funcional durante proceso |

---

## 12. Diseño de UDT

### 12.1 UDT reglas generales

Nombre: `@JCA_DLC_RULE`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| Code | Alfanumérico | Código único de regla |
| Name | Alfanumérico | Nombre descriptivo |
| U_Active | Y/N | Indica si la regla está activa |
| U_Priority | Numérico | Prioridad de evaluación |
| U_ObjType | Alfanumérico | Tipo de objeto SAP |
| U_EntitySet | Alfanumérico | EntitySet Service Layer |
| U_BaseDate | Alfanumérico | Fecha base de cálculo |
| U_GraceDays | Numérico | Días de holgura |
| U_Simulation | Y/N | Ejecuta en modo simulación |
| U_MaxPerRun | Numérico | Máximo de documentos por ejecución |
| U_OnlyNoTarget | Y/N | Solo cerrar si no tiene destino |
| U_CheckLines | Y/N | Validar líneas abiertas |
| U_CheckUpdate | Y/N | Validar actualización reciente |
| U_InactiveDays | Numérico | Días mínimos sin actualización |
| U_MinTotal | Importe | Monto mínimo |
| U_MaxTotal | Importe | Monto máximo |
| U_ReqApproval | Y/N | Requiere aprobación |
| U_CloseComment | Texto | Comentario funcional |
| U_ValidFrom | Fecha | Vigente desde |
| U_ValidTo | Fecha | Vigente hasta |

### 12.2 UDT reglas por condición de pago

Nombre: `@JCA_DLC_PAY`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| Code | Alfanumérico | Código único |
| Name | Alfanumérico | Nombre |
| U_Active | Y/N | Activa |
| U_ObjType | Alfanumérico | Tipo documento |
| U_GroupNum | Numérico | Código condición pago |
| U_PymntGroup | Texto | Nombre condición |
| U_GraceDays | Numérico | Días de holgura |
| U_BaseDate | Alfanumérico | Fecha base |
| U_Priority | Numérico | Prioridad |

### 12.3 UDT exclusiones

Nombre: `@JCA_DLC_EXC`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| Code | Alfanumérico | Código único |
| Name | Alfanumérico | Nombre |
| U_Active | Y/N | Activa |
| U_ObjType | Alfanumérico | Tipo documento o ALL |
| U_ExcType | Alfanumérico | CardCode, GroupCode, SlpCode, BPLId |
| U_ExcValue | Alfanumérico | Valor excluido |
| U_Reason | Texto | Motivo de exclusión |

### 12.4 UDT log detalle

Nombre: `@JCA_DLC_LOG`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| Code | Alfanumérico | ID único |
| Name | Alfanumérico | Resumen |
| U_RunId | Alfanumérico | ID ejecución |
| U_DateTime | Fecha/Hora | Fecha y hora |
| U_Env | Alfanumérico | Ambiente |
| U_CompanyDB | Alfanumérico | Sociedad |
| U_ObjType | Alfanumérico | Tipo documento |
| U_EntitySet | Alfanumérico | EntitySet |
| U_DocEntry | Numérico | DocEntry |
| U_DocNum | Numérico | DocNum |
| U_CardCode | Alfanumérico | Socio de negocio |
| U_CardName | Texto | Nombre socio |
| U_DocDate | Fecha | Fecha documento |
| U_DueDate | Fecha | Fecha vencimiento |
| U_BaseDate | Fecha | Fecha usada |
| U_DaysCalc | Numérico | Días calculados |
| U_RuleCode | Alfanumérico | Regla aplicada |
| U_Result | Alfanumérico | Resultado |
| U_Message | Texto | Mensaje |
| U_HttpStatus | Numérico | HTTP status |
| U_Attempts | Numérico | Intentos |
| U_Request | Memo | Request, solo en errores o debug |
| U_Response | Memo | Response, solo en errores o debug |

### 12.5 UDT resumen de ejecución

Nombre: `@JCA_DLC_RUN`

| Campo | Tipo sugerido | Descripción |
|---|---|---|
| Code | Alfanumérico | RunId |
| Name | Alfanumérico | Descripción |
| U_StartedAt | Fecha/Hora | Inicio |
| U_FinishedAt | Fecha/Hora | Fin |
| U_Status | Alfanumérico | RUNNING, FINISHED, ERROR |
| U_CompanyDB | Alfanumérico | Sociedad |
| U_Env | Alfanumérico | Ambiente |
| U_TotalCandidates | Numérico | Candidatos |
| U_TotalClosed | Numérico | Cerrados |
| U_TotalSimulated | Numérico | Simulados |
| U_TotalSkipped | Numérico | Omitidos |
| U_TotalErrors | Numérico | Errores |
| U_DurationSec | Numérico | Duración |

---

## 13. Seguridad

La solución debe cumplir:

- Usar usuario técnico SAP exclusivo.
- No usar usuarios personales.
- No guardar contraseña en código fuente.
- Leer password desde variable de entorno o Windows Credential Manager.
- Usar HTTPS contra Service Layer.
- No registrar credenciales en logs.
- Separar configuración por ambiente.
- Restringir permisos del directorio de instalación.
- Restringir permisos SAP del usuario técnico al mínimo necesario.

Ejemplo de configuración:

```json
{
  "Sap": {
    "BaseUrl": "https://sap-server:50000/b1s/v1",
    "CompanyDB": "SBODEMOCL",
    "UserName": "svc_autoclose",
    "PasswordEnvironmentVariable": "SAP_AUTOCLOSE_PASSWORD"
  },
  "Execution": {
    "Environment": "TST",
    "DefaultSimulation": true,
    "MaxRetries": 3,
    "TimeoutSeconds": 60,
    "PreventParallelRuns": true
  }
}
```

---

## 14. Logging y auditoría

### 14.1 Log técnico local

Debe generarse en:

```text
C:\SmartDocControl\Logs\
```

Debe incluir:

- Inicio y fin de ejecución.
- Login y logout Service Layer.
- Documentos candidatos.
- Errores técnicos.
- Tiempo total.
- Resumen final.

### 14.2 Log funcional SAP

Debe registrarse en `@JCA_DLC_LOG` y `@JCA_DLC_RUN`.

### 14.3 Política de JSON

- En errores: guardar request y response completo.
- En éxitos: guardar mensaje resumido.
- En debug: permitir activar logging extendido por configuración.

---

## 15. Manejo de errores

El sistema debe soportar:

- Timeout Service Layer.
- Sesión expirada.
- Error HTTP.
- Documento ya cerrado.
- Documento bloqueado.
- Permiso insuficiente.
- Error al escribir log en UDT.
- Error de configuración incompleta.

Regla crítica:

> Un error en un documento no debe detener el procesamiento del resto.

---

## 16. Idempotencia y doble ejecución

La solución debe prevenir múltiples ejecuciones simultáneas mediante:

- Archivo lock local, o
- Registro RUNNING en `@JCA_DLC_RUN`.

Si existe una ejecución activa, el proceso debe abortar de forma controlada y registrar el evento.

---

## 17. Parámetros de ejecución

El Runner debe permitir parámetros opcionales:

```bash
SmartDocControl.Runner.exe --simulation true
SmartDocControl.Runner.exe --company SBODEMOCL
SmartDocControl.Runner.exe --documentType Quotations
SmartDocControl.Runner.exe --max 50
```

---

## 18. Criterios de aceptación

El desarrollo será aceptado si cumple:

| Criterio | Resultado esperado |
|---|---|
| Ejecuta desde consola | La app puede ejecutarse manualmente |
| Ejecuta desde Task Scheduler | Corre diariamente a las 08:00 AM |
| Login Service Layer | Conecta correctamente a SAP |
| Lee UDT | Obtiene reglas activas |
| Simula cierre | Registra candidatos sin cerrar |
| Cierra documento | Ejecuta POST /Close correctamente |
| Registra log | Crea log detalle y resumen |
| Maneja error individual | Continúa con otros documentos |
| Respeta MaxPerRun | No supera límite configurado |
| Previene doble ejecución | No permite corridas simultáneas |
| No expone credenciales | Password fuera del código |
| Código mantenible | Proyecto separado por capas |

---

## 19. Plan de implementación

### Fase 1: Diseño técnico

- Validar PRD.
- Confirmar UDT.
- Confirmar reglas MVP.
- Confirmar ambiente TST.

### Fase 2: Desarrollo base

- Crear solución .NET 8.
- Crear capas.
- Implementar ServiceLayerClient.
- Implementar lectura de UDT.

### Fase 3: Motor de reglas

- Implementar RuleEngine.
- Implementar estados.
- Implementar simulación.

### Fase 4: Cierre documental

- Implementar POST /Close.
- Implementar procesamiento secuencial.
- Implementar logs.

### Fase 5: QA en TST

- Ejecutar solo en simulación.
- Revisar candidatos.
- Ajustar reglas.
- Validar con usuario clave.

### Fase 6: Activación controlada en PRD

- Activar con MaxPerRun bajo.
- Monitorear primera semana.
- Ajustar exclusiones.

---

## 20. Roadmap versión PRO

Funciones futuras:

- Reglas por condición de pago.
- Reglas por vendedor/comprador.
- Reglas por grupo de socio de negocio.
- Reglas por sucursal.
- Reglas por monto.
- Aprobación previa.
- Notificación por correo o Teams.
- Dashboard Power BI.
- Portal web de configuración.
- Instalador.
- Licenciamiento.
- SaaS híbrido con agente local.

---

## 21. Prompt operativo para Claude Code / Antigravity

Usar este prompt junto con el PRD adjunto:

```text
Antes de escribir código, lee completamente el archivo PRD_Smart_Document_Control_SAP_B1.

Tu tarea es implementar exactamente el alcance definido en el PRD.

Reglas obligatorias:
1. No agregues funcionalidades fuera del MVP sin consultarme.
2. No elimines funcionalidades definidas en criterios de aceptación.
3. No uses DI API.
4. No modifiques documentos por SQL directo.
5. Todo cierre documental debe hacerse vía Service Layer.
6. Debes mantener Clean Architecture.
7. Debes crear código compilable en .NET 8.
8. Debes documentar decisiones técnicas.
9. Si existe ambigüedad, detente y pregunta antes de asumir.
10. Después de cada fase, compara lo construido contra el PRD.

Primero genera:
- Resumen de entendimiento del PRD.
- Lista de tareas técnicas.
- Estructura de carpetas.
- Plan de implementación por commits.

No generes código hasta que confirme el plan.
```

---

## 22. Control de cambios

| Versión | Fecha | Descripción |
|---|---|---|
| 1.0 | 2026-05-04 | Versión inicial del PRD para desarrollo MVP |

---

## 23. Decisión recomendada

Para el primer desarrollo, se recomienda usar:

- Antigravity como entorno principal.
- Claude Code como agente de implementación.
- PRD como documento obligatorio de referencia.
- Desarrollo por fases, no todo de una vez.

El primer objetivo no debe ser cerrar documentos en productivo. El primer objetivo debe ser lograr una simulación confiable, trazable y revisable en TST.
