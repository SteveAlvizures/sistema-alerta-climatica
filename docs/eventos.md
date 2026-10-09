# Historial de eventos de riesgo — Bloque 7

| RF | Cobertura |
|---|---|
| RF-ADM-44 | Generación y agrupación automática existentes conservadas; historial consulta todos los Events persistidos. |
| RF-ADM-45 | Fechas, comunidad, fenómeno, máximo nivel, descripción, estado, evidencia principal y responsabilidad derivada. |
| RF-ADM-46 | Listado/detalle autenticados para los tres roles oficiales. |
| RF-ADM-47 | Fechas, comunidad, fenómeno, nivel y estado filtrados en backend antes de paginar. |
| RF-ADM-48 | Totales, activos/cerrados y distribuciones sobre Events reales del filtro completo. |

## Auditoría inicial del modelo

Antes del bloque, `Event` ya persistía Id, CommunityId, Phenomenon, Description, HighestLevel, Status, StartedAt, UpdatedAt, EndedAt y CreatedAt. Event ↔ Alert era una relación de uno a muchos mediante Alert.EventId; Community ↔ Event era uno a muchos. Alert ↔ SupportingReading ↔ Sensor proporcionaba los valores y sensores. La configuración EF conservaba los estados como texto y un índice por comunidad/estado/inicio.

El evaluador buscaba un incidente abierto por comunidad/fenómeno, añadía alertas y conservaba el máximo nivel alcanzado. El cierre automático y el cierre manual de alertas comprobaban que todas las alertas del incidente estuvieran Closed antes de cerrar Event. No existían endpoints de historial de eventos. Sensor/valor/responsable no eran columnas de Event; responsables y tiempos manuales estaban persistidos en Alert. El dashboard componía actividad reciente mezclando alertas y lecturas.

## Modelo final y compatibilidad

Se mantiene la entidad y generación existentes, sin duplicar incidentes ni cambiar su agrupación. No hay migración ni nuevas columnas: el DTO expone `occurredAt = StartedAt`, `level = HighestLevel`, `closedAt = EndedAt` y relaciones derivadas.

La alerta representativa se selecciona por nivel actual de la alerta descendente, fecha de detección descendente e Id descendente para desempate. Su SupportingReading proporciona el sensor, valor y unidad; nunca se suman valores de sensores con unidades distintas. El máximo histórico del incidente puede diferir del nivel actual de su evidencia representativa. El detalle incluye todos los sensores distintos y todas las alertas relacionadas, con regla, lectura, sensor, valores y condiciones capturadas en la alerta.

Un evento antiguo sin alertas conserva su información y devuelve sensor/valor/responsable null y listas vacías. No se fabrica evidencia ni responsables. Los cambios posteriores de reglas no modifican las condiciones/valores capturados en las alertas; sus nombres administrativos actuales pueden reflejar ediciones.

Estados conservados: `Open` = Activo, `Closed` = Cerrado. Una alerta Atendida todavía mantiene abierto el incidente; solo cuando todas las alertas relacionadas están cerradas se cierra Event.

Responsabilidad derivada:

- Evento abierto: usuario de la atención manual más reciente de sus alertas, por AcknowledgedAt e Id descendentes. `responsibility = Attention`.
- Evento cerrado: usuario de la última fecha de cierre de sus alertas únicamente si todos los cierres empatados en esa fecha tienen el mismo ClosedById no nulo. `responsibility = Closure`.
- Último cierre automático, cierres simultáneos ambiguos o historia sin responsables: null / Sin responsable. La atención anterior permanece visible en las alertas del detalle.

Generación y consultas de Event no generan auditoría de usuario. Atención/cierre manual siguen auditándose en Alert, sin duplicar registros.

## API y autorización

Los tres GET requieren `ConsultEvents`: JWT válido y rol Administrator, Operator o ConsultationUser. Sin JWT: 401. Identidad autenticada con rol insuficiente: 403. No se añaden escrituras manuales de Event.

| Endpoint | Resultado |
|---|---|
| GET /api/events | PagedResponse de eventos persistidos |
| GET /api/events/{id} | EventDetailResponse con event, sensors y alerts; 404 si no existe |
| GET /api/events/statistics | Estadísticas del resultado filtrado, sin paginación |

Listado y estadísticas comparten filtros `from`, `to` (DateTimeOffset inclusivos sobre StartedAt), `communityId`, `phenomenon`, `level`, `status`. Listado añade `page` >= 1 y `pageSize` 1..100 (por defecto 1/20). Fechas invertidas, enums inválidos y paginación inválida devuelven 400. Filtros y conteos se aplican en IQueryable antes de paginar. Orden StartedAt descendente, Id descendente.

El listado devuelve id, occurredAt, updatedAt, closedAt, communityId/communityName, phenomenon, level, description, status, sensorId/name/code, value/unit, representativeAlertId, responsibleUserId/name, responsibility y alertCount. Detalle añade sensores distintos y DTOs completos de las alertas.

Fenómenos: Flood/Inundación, Drought/Sequía, Storm/Tormenta, Frost/Helada, Wildfire/Incendio forestal. Niveles: Green/Normal, Yellow/Precaución, Orange/Alerta, Red/Emergencia.

Estadísticas: total, active, closed, byPhenomenon (cinco categorías incluso con cero) y byLevel (cuatro categorías incluso con cero). Se calculan en SQL sobre Events filtrados, no contando alertas ni actividad sintética.

## Frontend y dashboard

Módulo `/events` y detalle `/events/:id`, con guard autenticado y navegación lateral Eventos para sesiones existentes. Historial de lecturas y Bitácora se conservan separados. La página muestra filtros, paginación, etiquetas españolas, responsables, evidencia principal y estadísticas del filtro completo. El detalle enlaza sensores y alertas, incluyendo trazabilidad.

Bloque 8 integra los ocho eventos persistidos mas recientes en GET /api/dashboard mediante EventService. El resumen publico no expone responsables; el detalle conserva autenticacion. Se reemplaza la actividad sintetica del modo API; ver [Dashboard](dashboard.md).

## Demostración

1. Iniciar sesión como Administrator, Operator o ConsultationUser y abrir Eventos.
2. Aplicar fechas/comunidad/fenómeno/nivel/estado y comparar total y distribuciones con el listado completo filtrado.
3. Abrir un detalle y seguir los enlaces a sensores/alertas; comprobar regla, lectura, valor y responsables.
4. Como Operator, registrar una lectura de riesgo para un sensor activo con regla aplicable: el evento aparece o se añade una alerta al incidente abierto de igual comunidad/fenómeno.
5. Con dos alertas del mismo incidente, atender/cerrar una: el evento continúa Activo. Cerrar la última: pasa a Cerrado y refleja su responsable manual cuando no hay ambigüedad.
6. Normalizar mediante una lectura automática: el cierre conserva responsable null si la última operación fue automática, sin nueva acción de usuario en auditoría.
7. ConsultationUser puede consultar los tres endpoints; visitante recibe 401 y se redirige al login desde las rutas Angular.

## Validación y límites

232 pruebas backend pasadas (44 Domain, 17 Infrastructure, 171 Application) y 224 pruebas Angular pasadas. Angular production build correcto; advertencia previa de presupuesto de `sensors-page.scss` (7.02 kB / 6 kB). `git diff --check` correcto.

Las pruebas HTTP usan JWT real y persistencia EF InMemory; no se ejecutó contra SQL Server real. Las pruebas de snapshot existentes confirmaron que no hay cambios pendientes del modelo. Las consultas de eventos no generan ni modifican datos ni auditoría.

## Inventario de archivos

### Modificados

- `backend/src/ClimateAlert.Api/Authentication/AuthorizationPolicies.cs`
- `backend/src/ClimateAlert.Api/DependencyInjection.cs`
- `backend/src/ClimateAlert.Application/Features/Alerts/AlertService.cs`
- `backend/src/ClimateAlert.Infrastructure/DependencyInjection.cs`
- `backend/tests/ClimateAlert.Application.Tests/AlertLifecycleHttpTests.cs`
- `backend/tests/ClimateAlert.Application.Tests/AuthorizationTests.cs`
- `docs/endpoints.md`
- `frontend/angular-app/src/app/app.routes.ts`
- `frontend/angular-app/src/app/layout/sidebar/sidebar.spec.ts`
- `frontend/angular-app/src/app/layout/sidebar/sidebar.ts`

### Nuevos

- `backend/src/ClimateAlert.Api/Controllers/EventsController.cs`
- `backend/src/ClimateAlert.Application/Common/Interfaces/IEventHistoryRepository.cs`
- `backend/src/ClimateAlert.Application/Features/Events/EventModels.cs`
- `backend/src/ClimateAlert.Application/Features/Events/EventService.cs`
- `backend/src/ClimateAlert.Infrastructure/Persistence/EventHistoryRepository.cs`
- `backend/tests/ClimateAlert.Application.Tests/EventHistoryTests.cs`
- `docs/eventos.md`
- `frontend/angular-app/src/app/core/guards/authenticated.guard.spec.ts`
- `frontend/angular-app/src/app/core/guards/authenticated.guard.ts`
- `frontend/angular-app/src/app/core/models/event.model.ts`
- `frontend/angular-app/src/app/core/services/event-api.service.ts`
- `frontend/angular-app/src/app/features/events/event-presentation.ts`
- `frontend/angular-app/src/app/features/events/event-test-data.ts`
- `frontend/angular-app/src/app/features/events/pages/event-detail-page/event-detail-page.html`
- `frontend/angular-app/src/app/features/events/pages/event-detail-page/event-detail-page.spec.ts`
- `frontend/angular-app/src/app/features/events/pages/event-detail-page/event-detail-page.ts`
- `frontend/angular-app/src/app/features/events/pages/events-page/events-page.html`
- `frontend/angular-app/src/app/features/events/pages/events-page/events-page.scss`
- `frontend/angular-app/src/app/features/events/pages/events-page/events-page.spec.ts`
- `frontend/angular-app/src/app/features/events/pages/events-page/events-page.ts`
