# Entrega Bloque 8: dashboard y auditoria

Rama steve, HEAD conservado: 3ecda2f feat: implementar historial de eventos de fase 2.
No se hizo commit, push, merge ni se cambiaron otras ramas. No se crearon migraciones.

## Funcionalidad y requisitos

RF-ADM-56: catalogo completo de operaciones importantes; RF-ADM-57: usuario/accion/fecha/hora/entidad/ID/descripcion;
RF-ADM-58: bitacora solo Administrator; RF-ADM-59: filtros backend y Angular antes de paginacion.
RF-ADM-60 a RF-ADM-68: KPIs reales, conteo global de comunidades, sensores activos/inactivos,
alertas solo Open, distribucion de cuatro niveles, lecturas persistidas, filtro de comunidad,
eventos persistidos y polling cada 15 segundos sin repetir la carga inicial.

Fuentes y alcance: [Dashboard](dashboard.md). Catalogo final, auditoria previa y faltantes corregidos,
logout stateless y permisos: [Auditoria](auditoria.md).

Endpoints modificados: GET /api/dashboard; GET /api/audit-actions (parametros oficiales y aliases);
DELETE /api/communities/{id} (auditoria); PUT /api/alert-rules/{id} (auditoria de cambio de estado);
POST /api/sensor-readings (nombre oficial de accion nueva).
Endpoint nuevo: POST /api/auth/logout, autenticado, 204/401, actor JWT; limpieza local y redireccion a /.

## Validacion final

| Comprobacion | Resultado |
| --- | --- |
| dotnet test backend/ClimateAlert.sln --no-restore --verbosity normal | 247 correctas: Domain 44, Infrastructure 17, Application 186; 0 fallidas/omitidas |
| npm test -- --watch=false --browsers=ChromeHeadless | 228 correctas, 0 fallidas |
| npm run build -- --configuration production | Correcto; unico aviso de presupuesto CSS preexistente en sensors-page.scss: 7.02 kB frente a 6 kB |
| git diff --check | Correcto, sin errores de whitespace |
| Migraciones | Ninguna |

Pruebas nuevas: aislamiento de comunidad, sensores activos/inactivos, total global de comunidades,
alertas atendidas/cerradas excluidas, distribucion, identidad y ausencia de duplicados de Event,
ventana de treinta lecturas, logout JWT/stateless, roles 401/403/200, filtros oficiales,
eliminacion/manual/simulacion, auditoria de reglas, activacion/reinicio de sensores,
consulta historica y email; Angular cubre KPIs, eventos reales, enlaces, catalogo y logout con error.
Pruebas existentes mantienen CRUD/estado/rol de usuarios, comunidades/sensores, lifecycle,
guards/sidebar de todos los roles, filtros/paginacion y redireccion de logout.

## Riesgos y pendientes reales

- Las pruebas HTTP usan EF InMemory: pendiente validar contra SQL Server real y probar visualmente
  con datos del entorno de despliegue. La consulta de lecturas limita y filtra en la base de datos.
- Logout no revoca JWT; si falla la red, la limpieza local funciona pero no se registra en backend.
- Persiste la separacion transaccional previa entre operacion y auditoria de varios controladores;
  detalle en auditoria.md. No se modifica persistencia ni se pierde historial.
- Aviso CSS previo de sensores. La suite frontend tambien conserva un aviso previo de una prueba
  de AlertsPage sin expectativas. No se introdujeron esos avisos en este bloque.

## Archivos modificados, nuevos y git status final

M = archivo existente modificado; ?? = archivo nuevo sin seguimiento. Todos los cambios estan sin stage.

```text
 M backend/src/ClimateAlert.Api/Audit/AuditActionService.cs
 M backend/src/ClimateAlert.Api/Authentication/AuthorizationPolicies.cs
 M backend/src/ClimateAlert.Api/Controllers/AlertRulesController.cs
 M backend/src/ClimateAlert.Api/Controllers/AuditActionsController.cs
 M backend/src/ClimateAlert.Api/Controllers/AuthController.cs
 M backend/src/ClimateAlert.Api/Controllers/CommunitiesController.cs
 M backend/src/ClimateAlert.Api/Controllers/DashboardController.cs
 M backend/src/ClimateAlert.Api/Controllers/SensorReadingsController.cs
 M backend/tests/ClimateAlert.Application.Tests/AlertRuleEditingTests.cs
 M backend/tests/ClimateAlert.Application.Tests/AuditActionServiceTests.cs
 M backend/tests/ClimateAlert.Application.Tests/AuthorizationTests.cs
 M docs/endpoints.md
 M docs/eventos.md
 M docs/seguridad.md
 M frontend/angular-app/src/app/core/models/climate-alert.model.ts
 M frontend/angular-app/src/app/core/models/climate-dashboard.model.ts
 M frontend/angular-app/src/app/core/services/api-services.spec.ts
 M frontend/angular-app/src/app/core/services/audit-action-api.service.ts
 M frontend/angular-app/src/app/core/services/auth.service.spec.ts
 M frontend/angular-app/src/app/core/services/auth.service.ts
 M frontend/angular-app/src/app/core/services/dashboard-data.service.spec.ts
 M frontend/angular-app/src/app/core/services/dashboard-data.service.ts
 M frontend/angular-app/src/app/features/audit-log/pages/audit-log-page/audit-log-page.spec.ts
 M frontend/angular-app/src/app/features/audit-log/pages/audit-log-page/audit-log-page.ts
 M frontend/angular-app/src/app/features/dashboard/components/recent-history/recent-history.html
 M frontend/angular-app/src/app/features/dashboard/components/recent-history/recent-history.ts
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.html
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.scss
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.spec.ts
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.ts
?? backend/tests/ClimateAlert.Application.Tests/DashboardAuditHttpTests.cs
?? docs/auditoria.md
?? docs/bloque8-entrega.md
?? docs/dashboard.md
?? frontend/angular-app/src/app/core/services/dashboard-api.service.ts
```
