# Integración pendiente con Community, Sensor, Alert y Audit

Baseline: rama `steve-fase3`, commit `764ac02`. Los cuatro servicios todavía no
existen en esta rama. Sus rutas siguen atendidas por el monolito. Este documento
describe los contratos actuales, no una API alternativa. Angular usa solamente
`/api` a través de Gateway; no necesita conocer destinos internos.

## Entrega necesaria de cada integrante

Entregar URL interna con esquema/puerto y DNS de Compose/K3s, imagen y variables
documentadas, persistencia propia, procedimiento de migración no destructivo,
contratos compatibles, pruebas de contrato y health. Confirmar validación JWT y
consulta de identidad vigente de Auth; las cabeceras `X-User-*` no son identidad.
No compartir credenciales ni acceder a bases de otros servicios.

| Servicio futuro | Rutas actuales que debe atender | Referencia ejecutable |
|---|---|---|
| Community | `/api/communities` y `/{id}`, `/{id}/status` | [Controller](../../backend/src/ClimateAlert.Api/Controllers/CommunitiesController.cs) |
| Sensor | `/api/sensors`, `/{id}`, `/{id}/status`, `/{id}/readings`, `/{id}/readings/latest`; `/api/sensor-readings`, `/latest`; `/api/communities/{id}/sensors` | [Sensors](../../backend/src/ClimateAlert.Api/Controllers/SensorsController.cs), [Readings](../../backend/src/ClimateAlert.Api/Controllers/SensorReadingsController.cs) |
| Alert | `/api/alerts`, `/{id}`, `/{id}/acknowledge`, `/{id}/resolve`; `/api/alert-rules`, `/{id}`, `/{id}/status`; `/api/events`, `/{id}`, `/statistics`; `/api/communities/{id}/alerts` | [Alerts](../../backend/src/ClimateAlert.Api/Controllers/AlertsController.cs), [Rules](../../backend/src/ClimateAlert.Api/Controllers/AlertRulesController.cs), [Events](../../backend/src/ClimateAlert.Api/Controllers/EventsController.cs) |
| Audit | GET `/api/audit-actions`; recepción interna de auditoría Auth | [Audit](../../backend/src/ClimateAlert.Api/Controllers/AuditActionsController.cs), [Ingesta](../../backend/src/ClimateAlert.Api/Controllers/AuthIntegrationController.cs) |

## Reglas comunes de JSON y autorización

JSON usa nombres **camelCase**, UUID en cadenas, fechas ISO 8601 con offset y enums
como cadenas con el nombre exacto del enum. Mantener nombres, nullabilidad y
semántica; no convertir UUID en enteros ni números decimales en textos.

La paginación se solicita con `page` (desde 1) y `pageSize`; la respuesta es:

```json
{"data":[],"pageIndex":1,"pageSize":20,"totalPages":0,"totalCount":0,"hasPrevious":false,"hasNext":false}
```

`totalCount` debe corresponder a los mismos filtros en todas las páginas y no a la
página actual. Report consulta páginas de 50, hasta 2,000 registros por fuente por
defecto, detecta páginas incompletas y rechaza excesos. Las consultas no constituyen
un snapshot transaccional entre servicios. Para grandes volúmenes se necesita un
contrato de snapshot/cursor acordado posteriormente, sin cambiar silenciosamente
la semántica actual.

Mantener 400 para validación, 401 para sesión inválida/inactiva, 403 para permiso
insuficiente, 404 para ausentes, 409 para conflicto y ProblemDetails sin secretos.
Las lecturas Community/Sensor/Alert/Rule son públicas actualmente. Escrituras
requieren `OperateSystem` (Administrator/Operator); Events requiere `ConsultEvents`
(también ConsultationUser); Audit requiere Administrator. El servicio propietario
debe autorizar nuevamente, aunque Gateway haya validado criptográficamente el JWT.

## Community

GET colección sin query devuelve **array**, con cualquier query devuelve paginado.
Conservar ambos formatos. Filtros: `search`, `isActive`, `municipality`, `department`,
`page`, `pageSize`. GET `/{id}` devuelve CommunityResponse. POST devuelve 201 y
Location; PUT `/{id}` y PATCH `/{id}/status` devuelven 200; DELETE `/{id}` devuelve
204 o 409 si hay dependencias. PATCH recibe `{"isActive":true}`.

CommunityResponse contiene `id`, `name`, `location`, `description`, `isActive`,
`createdAt`, `municipality`, `department`, `country`, `latitude`, `longitude`,
`sensorCount`. POST/PUT reciben los mismos datos administrativos, sin id/fecha ni
sensorCount. En PUT isActive es nullable; no reemplazar omisión por false.

Tipos exactos/defaults: [CommunityModels.cs](../../backend/src/ClimateAlert.Application/Features/Communities/CommunityModels.cs).
`sensorCount` implica integración con Sensor por HTTP/proyección; nunca consultar
su SQL. El integrante debe definir cómo mantiene ese dato y su consistencia.

## Sensor y lecturas

GET sensores conserva array sin query y paginado con query. Filtros: `communityId`,
`type`, `variable`, `isActive`, `code`, `search`, `page`, `pageSize`.
GET comunidad/sensores devuelve array. POST 201; PUT `/{id}` y PATCH status 200.
PATCH recibe `{"isActive":true}`; conservar el reinicio de monitoreo al activar
un sensor que ya está activo, y su auditoría.

SensorResponse: `id`, `communityId`, `code`, `name`, `measurementType`, `origin`,
`status`, `location`, `deviceCode`, `lastCommunicationAt`, `createdAt`, `type`,
`unit`, `installationDate` (fecha YYYY-MM-DD), `description`, `communityName`,
`isActive`. No confundir `measurementType` del cuerpo con `variable` del filtro.
POST/PUT y nullabilidad: [SensorModels.cs](../../backend/src/ClimateAlert.Application/Features/Sensors/SensorModels.cs).

GET `/api/sensor-readings`: paginado con `communityId`, `sensorId`, `variable`,
`dateFrom`, `dateTo`, `page`, `pageSize`. GET `/api/sensor-readings/latest` devuelve
una lectura más reciente por sensor, también paginada, con los mismos filtros
salvo fechas. Desempate por measuredAt/Id, no una consulta por cada sensor.

HistoryReadingResponse: `id`, `sensorId`, `communityId`, `sensorName`, `sensorCode`,
`communityName`, `variable`, `value`, `unit`, `measuredAt`, `receivedAt`, `origin`,
`sensorStatusAtMeasurement` nullable. Preservar estado histórico, sin inferirlo del
estado actual. SensorReadingResponse omite los nombres/communityId enriquecidos.

GET `/api/sensors/{id}/readings`: paginado SensorReadingResponse, query `page`,
`pageSize` o alias `limit`. GET `.../readings/latest`: un SensorReadingResponse.
POST `/api/sensor-readings` recibe `{"sensorId":"UUID","value":24.5}` (value
nullable según reglas actuales), devuelve 201 SensorReadingResponse.
[Modelos exactos](../../backend/src/ClimateAlert.Application/Features/SensorReadings/SensorReadingModels.cs).
Conservar validación de comunidad/sensor, origen simulado y auditoría; resolver
dependencias mediante contratos con los otros propietarios.

## Alert, reglas y eventos

GET alertas: paginado y contadores adicionales `preventiveCount`, `highCount`,
`criticalCount`. Filtros `page`, `pageSize`, `communityId`, `variable`, `level`,
`sensorId`, `phenomenon`, `status`, `dateFrom`, `dateTo`. GET comunidad/alertas
devuelve array. PATCH acknowledge/resolve no necesita cuerpo y devuelve 200 AlertResponse.

AlertResponse conserva `id`, `communityId`, `ruleId`, `supportingReadingId`, `eventId`,
`level`, `phenomenon`, `status`, `message`, `detectedAt`, `updatedAt`, `closedAt`,
`sensorId`, `variable`, `detectedValue`, `activationPoint`, `unit`, `communityName`,
`sensorName`, `sensorCode`, `ruleName`, `ruleCode`, `statusLabel`, `minValue`,
`maxValue`, `usesRange`, `comparisonOperator`, `acknowledgedAt`, `acknowledgedById`,
`acknowledgedByName`, `closedById`, `closedByName`.
[Tipos y nullabilidad](../../backend/src/ClimateAlert.Application/Features/Alerts/AlertModels.cs).

GET reglas devuelve array; GET id un objeto; POST 201, PUT y PATCH status 200.
PATCH recibe isActive. AlertRuleResponse: `id`, `communityId`, `sensorId`, `code`,
`name`, `phenomenon`, `variable`, `dangerLevel`, `lowerLimit`, `upperLimit`,
`validFrom`, `validUntil`, `isActive`, `createdAt`, `comparisonOperator`,
`activationPoint`, `unit`, `minValue`, `maxValue`, `message`, `usesRange`.
Los cuerpos POST/PUT **no son iguales**: conservar campos requeridos, aliases y
semántica en [AlertRuleModels.cs](../../backend/src/ClimateAlert.Application/Features/AlertRules/AlertRuleModels.cs).

GET eventos: paginado con `from`, `to` (no dateFrom/dateTo), `communityId`,
`phenomenon`, `level`, `status`, `page`, `pageSize`. GET statistics admite esos
filtros sin paginación y devuelve `{total,active,closed,byPhenomenon,byLevel}`;
los dos grupos son arrays `{phenomenon,count}` y `{level,count}` respectivamente.
GET id devuelve `{event,sensors,alerts}`.

EventResponse: `id`, `occurredAt`, `updatedAt`, `closedAt`, `communityId`,
`communityName`, `phenomenon`, `level`, `description`, `status`, `sensorId`,
`sensorName`, `sensorCode`, `value`, `unit`, `representativeAlertId`,
`responsibleUserId`, `responsibleUserName`, `responsibility`, `alertCount`.
EventSensorResponse es `{id,name,code,communityId}` y alerts usa AlertResponse.
[Tipos exactos](../../backend/src/ClimateAlert.Application/Features/Events/EventModels.cs).
Conservar enums en [Domain/Enums](../../backend/src/ClimateAlert.Domain/Enums),
incluidos niveles y estados; traducir etiquetas solo en presentación.

## Audit

GET `/api/audit-actions`: paginado con `page`, `pageSize`, `username`, `action`,
`entity`, `dateFrom`, `dateTo`; aliases actuales `user`, `from`, `to` tienen prioridad.
Respuesta por fila: `{id,occurredAt,username,action,affectedEntity,affectedRecordId,description}`.
[AuditActionService](../../backend/src/ClimateAlert.Api/Audit/AuditActionService.cs).

La outbox de Auth entrega POST `/internal/auth-events`, con clave de servicio
`X-Internal-Service-Key`, sin exponer esa ruta al navegador. Cuerpo actual:
`{id,actor,target,action,description,affectedRecordId,occurredAt}`; actor/target son
`{id,name,username,role,isActive,createdAt,lastAccessAt}`. Ver
[ExternalIdentity](../../backend/src/ClimateAlert.Api/Authentication/ExternalIdentity.cs).
Auth también envía `permissions`; el receptor actual acepta ese campo adicional.
204 tanto para recepción como para reenvío duplicado por id. Conservar acciones
Login, Logout, UsuarioCreado, UsuarioEditado, UsuarioActivado, UsuarioDesactivado y
RolUsuarioCambiado; descripción no vacía y máximo 1,000 caracteres. Los eventos
administrativos del monolito todavía se registran dentro de sus operaciones SQL:
su entrega al futuro Audit requiere diseño de outbox/consistencia y contrato de
ingesta adicional acordado. No afirmar que basta cambiar una URL para extraerla.

## Cambio exacto de destinos, sin cambiar Angular

Crear clusters YARP solamente cuando existan destinos reales. No activar nombres
DNS hipotéticos. Variables de configuración .NET para un servicio ya entregado:

```text
ReverseProxy__Clusters__community__Destinations__primary__Address=<URL interna Community terminada en />
ReverseProxy__Routes__communities__ClusterId=community
```

| Cluster nuevo | IDs de rutas cuyo ClusterId se cambia conjuntamente |
|---|---|
| community | communities |
| sensor | sensors, sensor-readings, community-sensors |
| alert | alerts, alert-rules, events, community-alerts |
| audit | audit-actions |

Por ejemplo `ReverseProxy__Routes__community-sensors__ClusterId=sensor`; los guiones
forman parte del ID. Conservar Match/Order existentes en
[appsettings.json](../../gateway/src/ClimateAlert.Gateway/appsettings.json),
en especial Order -20 de rutas anidadas y 100 del fallback. No cambiar todas las
rutas `/api/communities/*` hacia Community, porque perdería sensores y alertas.
Dashboard y fallback continúan en monolito hasta acordar su composición.

Report consulta directamente APIs internas, por lo que además debe cambiarse
`COMMUNITY_API_URL`, `SENSOR_API_URL`, `READING_API_URL`,
`ALERT_API_URL`, `EVENT_API_URL` según propietario. Auth debe cambiar
`AUTH_AUDIT_URL` hacia la ingesta de Audit cuando esté lista. Revisar redes Compose,
dependencias y readiness de Gateway: actualmente comprueba monolito/Auth/Report;
agregar comprobaciones de los nuevos destinos y acordar disponibilidad parcial.
En el Compose entregado esas cinco URLs de Report se parametrizan desde el archivo
de entorno como `REPORT_COMMUNITY_API_URL`, `REPORT_SENSOR_API_URL`,
`REPORT_READING_API_URL`, `REPORT_ALERT_API_URL`, `REPORT_EVENT_API_URL`.

Cada integrante debe entregar liveness independiente de bases/servicios y
readiness que compruebe su persistencia y dependencias indispensables, con rutas
documentadas (recomendación `/health/live`, `/health/ready`, compatible con los
servicios existentes). Confirmar esas rutas antes del corte, no asumirlas.

Validar contra el mismo conjunto de datos: cuerpos/status/filtros/orden/paginación,
identidad vigente, 401/403, mutaciones y auditoría, ocho PDFs y navegación Angular.
Guardar valores anteriores para rollback de configuración; si ya hubo escrituras
en la base nueva, se requiere reconciliación antes de volver al monolito. Cambiar
rutas no migra datos ni resuelve por sí solo claves foráneas de Fase 2.

Steve puede preparar configuración, pruebas de contrato y despliegue, conservar
Angular estable y mantener Gateway/Auth/Report. El corte y la migración requieren
los servicios, datos compatibles y validaciones de los otros integrantes.
