# Bloque 6: comunidades y sensores

## Cobertura funcional

| RF | Implementación |
|---|---|
| RF-ADM-08–10 | Crear/editar comunidades completas y activar/desactivar con auditoría. |
| RF-ADM-11–12 | Listado, búsqueda, filtros de estado/municipio/departamento y paginación backend. |
| RF-ADM-13 | `sensorCount` incluye todos los sensores asociados, activos e inactivos. |
| RF-ADM-14 | Se conserva el filtro de comunidad del dashboard y sus consultas históricas. |
| RF-ADM-15–17 | Crear/editar sensores completos y activar/desactivar. |
| RF-ADM-18 | Sensor inactivo: lectura manual/automática rechazada con 409, simulación lo excluye y evaluador no genera alertas. Historial conservado. |
| RF-ADM-19–20 | Consulta por comunidad y filtros por comunidad/tipo/variable/estado/código/búsqueda antes de contar y paginar. |
| RF-ADM-21 | Administrator/Operator registran valores simulados con el endpoint manual existente; cada valor se agrega como lectura. |
| RF-ADM-22 | SensorCreado, SensorEditado, SensorActivado, SensorDesactivado y ValorSimuladoModificado. |

Las escrituras usan `OperateSystem`: Administrator/Operator permitidos, ConsultationUser sin permiso (403), identidad ausente/inválida (401). Las consultas públicas existentes se conservan. Usuarios y roles no se reciben del body: la auditoría identifica al usuario mediante el JWT actual.

## Comunidad

Campos nuevos nullable: `Municipality`, `Department`, `Country`, `Latitude`, `Longitude`. Ya existían `Name`, `Location`, `Description`, `IsActive`, `CreatedAt`.

POST y PUT completos requieren nombre, ubicación, municipio, departamento, país y coordenadas. Latitud -90..90, longitud -180..180; precisión persistida decimal(10,7). Descripción opcional. POST admite `isActive` (true por defecto); PUT lo conserva si se omite. Los datos históricos desconocidos permanecen null y deben completarse al editar; no se inventan coordenadas.

GET `/api/communities?search=&isActive=&municipality=&department=&page=1&pageSize=20`: búsqueda en nombre/ubicación/descripción, filtros parciales de municipio/departamento, estado booleano. GET por id incluye datos completos y `sensorCount`.

Nuevo PATCH `/api/communities/{id}/status` recibe `{ "isActive": false }`. Auditoría: ComunidadCreada, ComunidadEditada, ComunidadActivada, ComunidadDesactivada. Se mantiene el DELETE anterior solo para comunidades sin dependencias.

Las comunidades inactivas se muestran y permanecen seleccionables para consultar el historial, con su estado visible en los filtros de sensores. Desactivar una comunidad no desactiva en cascada sus sensores ni borra datos; la recepción depende del estado individual de cada sensor.

## Sensor y variable medida

Campos nuevos nullable: `Type`, `Unit`, `InstallationDate` (date), `Description`. Se conservan los demás campos, incluyendo `Origin`, `DeviceCode`, `Status` y las relaciones con comunidad/lecturas. `IsActive` en el DTO se deriva de `Status`.

| Type | Etiqueta | MeasurementType | Unidad por defecto |
|---|---|---|---|
| Temperature | Temperatura | Temperature | °C |
| Humidity | Humedad | RelativeHumidity | % |
| WindSpeed | Velocidad del viento | WindSpeed | km/h |
| Rainfall | Lluvia | RainfallLevel | mm |
| RiverLevel | Nivel de río | RiverOrReservoirLevel | m |
| ReservoirLevel | Nivel de reservorio | RiverOrReservoirLevel | m |
| SmokeFire | Humo/incendio | SmokeConcentration | ppm |
| OtherEnvironmental | Otro sensor ambiental | OtherEnvironmental | u |

No se cambian los valores ni nombres de las cinco variables existentes. Las dos nuevas variables se agregan al final del enum. Río y reservorio comparten la variable histórica para conservar reglas/lecturas. Un sensor antiguo sin tipo usa el tipo derivado de su variable; el antiguo tipo combinado de agua se interpreta como RiverLevel hasta que el operador lo precisa como ReservoirLevel.

POST `/api/sensors` conserva `communityId`, `measurementType`, `location`, `isActive`; añade `name`, `code`, `type`, `unit`, `installationDate`, `description`, `origin`, `deviceCode`. Tipo y variable deben coincidir. El formulario envía fecha explícita; un cliente antiguo que la omita usa la fecha de registro. Nombre/código se generan si se omiten, conservando Phase 1. Origen Simulated por defecto; restricciones del código de dispositivo se mantienen.

PUT `/api/sensors/{id}` permite editar esos campos administrativos, comunidad, tipo y estado; el origen se conserva. Puede omitirse cualquier campo adicional para compatibilidad con el PUT antiguo de ubicación. Las fechas históricas desconocidas permanecen null mientras el cliente no las proporcione. El formulario requiere completar los datos al editar.

La restricción única existente `(CommunityId, Code)` se conserva en SQL; códigos explícitos nuevos/editados se verifican además contra el listado global. Una escritura duplicada en la misma comunidad responde 409, incluida la restricción SQL. No se renumeran códigos históricos.

La unidad se persiste y se usa en lecturas manuales/simuladas; datos antiguos la resuelven mediante la variable. No se modifica la unidad de lecturas antiguas. Variable, comunidad y unidad no pueden cambiar si hay lecturas o reglas asociadas (409); el tipo puede precisarse entre río/reservorio porque comparten variable. Nombre, código, ubicación, descripción, instalación y estado siguen siendo editables.

GET `/api/sensors?communityId=&type=&variable=&isActive=&code=&search=&page=1&pageSize=20`: código y búsqueda parciales; búsqueda en nombre/código/ubicación. GET por id incluye tipo, unidad, instalación, descripción, nombre de comunidad y estado. Se mantiene GET `/api/communities/{id}/sensors`.

PATCH status y POST `/api/sensor-readings` mantienen sus rutas. Modificar un valor simulado agrega una lectura y audita `ValorSimuladoModificado`, además del evento manual anterior. No reemplaza el historial.

## Inactividad e integración

Repositorio de simulación selecciona solo activos/simulados; el ciclo también valida ese estado. El registrador consulta el estado actual y rechaza sensores inactivos antes de agregar la lectura/evaluar reglas. El evaluador incluye una comprobación defensiva. El seeder de demostración tampoco agrega lecturas a sensores inactivos.

`Sensor.Status` es token de concurrencia: una desactivación concurrente invalida una actualización de comunicación basada en estado antiguo. SQL Server usa la transacción de SaveChanges para la lectura/evaluación.

Dashboard conserva sensores/historial pero sus indicadores operativos excluyen sensores inactivos. Los indicadores de los cinco fenómenos meteorológicos anteriores se mantienen; los nuevos KPIs ambientales quedan para Bloque 8. Los tipos nuevos sí tienen detalle, lecturas y gráficos. Alertas históricas no se eliminan al desactivar.

## Migración y validación

Migración `20261007040200_CompleteCommunityAndSensorManagement`: nueve columnas nullable, sin borrar/alterar columnas o datos existentes y sin reemplazar índices. Se generó, **no se aplicó** a una base real. El arranque actual de API aplica migraciones pendientes cuando se ejecute. Down elimina solo los campos de este bloque y no debe usarse si se necesita conservar sus nuevos datos.

Pruebas: 214 backend (44 Domain, 17 Infrastructure, 153 Application) y 191 Angular. Incluyen roles reales HTTP, CRUD, filtros/paginación, coordenadas, tipos, metadatos, códigos duplicados, auditoría JWT, bloqueo de lecturas, historial, concurrencia, formularios, filtros y botones por rol. Build Angular production correcto; sigue la advertencia existente del presupuesto CSS de sensores (7.02 kB / 6 kB).

Las pruebas de persistencia usan EF InMemory; migración y snapshot se verifican, pero queda la validación de despliegue contra SQL Server real. La unicidad global entre comunidades para códigos explícitos se valida en aplicación, mientras la garantía concurrente SQL conserva el alcance histórico por comunidad.

## Inventario final de archivos

### Modificados

- `backend/src/ClimateAlert.Api/Controllers/CommunitiesController.cs`
- `backend/src/ClimateAlert.Api/Controllers/DashboardController.cs`
- `backend/src/ClimateAlert.Api/Controllers/SensorReadingsController.cs`
- `backend/src/ClimateAlert.Api/Controllers/SensorsController.cs`
- `backend/src/ClimateAlert.Api/DemoDataSeeder.cs`
- `backend/src/ClimateAlert.Application/Common/Interfaces/PersistenceContracts.cs`
- `backend/src/ClimateAlert.Application/Features/Alerts/AlertEvaluator.cs`
- `backend/src/ClimateAlert.Application/Features/Communities/CommunityModels.cs`
- `backend/src/ClimateAlert.Application/Features/Communities/CommunityService.cs`
- `backend/src/ClimateAlert.Application/Features/SensorReadings/SensorReadingService.cs`
- `backend/src/ClimateAlert.Application/Features/SensorReadings/SimulatedReadingCycle.cs`
- `backend/src/ClimateAlert.Application/Features/Sensors/SensorModels.cs`
- `backend/src/ClimateAlert.Application/Features/Sensors/SensorService.cs`
- `backend/src/ClimateAlert.Domain/Entities/Community.cs`
- `backend/src/ClimateAlert.Domain/Entities/Sensor.cs`
- `backend/src/ClimateAlert.Domain/Enums/ClimateVariable.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Configurations/CommunityConfiguration.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Configurations/SensorConfiguration.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Migrations/ClimateAlertDbContextModelSnapshot.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Repositories.cs`
- `backend/src/ClimateAlert.Infrastructure/Simulation/SimulatedReadingValueGenerator.cs`
- `backend/tests/ClimateAlert.Application.Tests/AlertEvaluationTests.cs`
- `backend/tests/ClimateAlert.Application.Tests/AlertLifecycleHttpTests.cs`
- `backend/tests/ClimateAlert.Application.Tests/AlertRepositoryIntegrityTests.cs`
- `backend/tests/ClimateAlert.Application.Tests/ApplicationServiceTests.cs`
- `docs/endpoints.md`
- `frontend/angular-app/src/app/core/models/api.model.ts`
- `frontend/angular-app/src/app/core/services/community-api.service.ts`
- `frontend/angular-app/src/app/core/services/dashboard-data.service.ts`
- `frontend/angular-app/src/app/core/services/sensor-api.service.ts`
- `frontend/angular-app/src/app/features/alert-rules/pages/alert-rules-page/alert-rules-page.ts`
- `frontend/angular-app/src/app/features/alerts/alert-presentation.ts`
- `frontend/angular-app/src/app/features/audit-log/pages/audit-log-page/audit-log-page.ts`
- `frontend/angular-app/src/app/features/communities/pages/communities-page/communities-page.html`
- `frontend/angular-app/src/app/features/communities/pages/communities-page/communities-page.scss`
- `frontend/angular-app/src/app/features/communities/pages/communities-page/communities-page.spec.ts`
- `frontend/angular-app/src/app/features/communities/pages/communities-page/communities-page.ts`
- `frontend/angular-app/src/app/features/history/pages/history-page/history-page.ts`
- `frontend/angular-app/src/app/features/sensors/pages/sensor-detail-page/sensor-detail-page.html`
- `frontend/angular-app/src/app/features/sensors/pages/sensor-detail-page/sensor-detail-page.ts`
- `frontend/angular-app/src/app/features/sensors/pages/sensors-page/sensors-page.html`
- `frontend/angular-app/src/app/features/sensors/pages/sensors-page/sensors-page.scss`
- `frontend/angular-app/src/app/features/sensors/pages/sensors-page/sensors-page.spec.ts`
- `frontend/angular-app/src/app/features/sensors/pages/sensors-page/sensors-page.ts`

### Nuevos

- `backend/src/ClimateAlert.Domain/Enums/SensorType.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Migrations/20261007040200_CompleteCommunityAndSensorManagement.Designer.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/Migrations/20261007040200_CompleteCommunityAndSensorManagement.cs`
- `backend/tests/ClimateAlert.Application.Tests/CommunitySensorManagementTests.cs`
- `backend/tests/ClimateAlert.Infrastructure.Tests/CommunitySensorMigrationTests.cs`
- `docs/comunidades-sensores.md`
- `frontend/angular-app/src/app/core/models/sensor-types.ts`
