# Guía de desarrollo interno

> Integración final: [informe del 23/10/2026](INFORME_FINAL_FASE3.md). El Compose base
> conserva el modo estable; `docker-compose.final.yml` agrega los cuatro propietarios
> nuevos y el corte es explícito después de importar/conciliar. No equivale a producción certificada.

Esta guía describe mantenimiento del código, no estudio o defensa personal.
Conservar contratos Fase 2 y no extraer servicios nuevos sin datos/contratos
entregados y un plan incremental. [Integración pendiente](integracion-pendiente.md)
es la referencia para propietarios y rutas.

## Ubicación de decisiones

| Área | Archivos y motivo documentado |
|---|---|
| Gateway | Program y appsettings: carga YARP, prioridades; ProxyTransforms: frontera de cabeceras; CorrelationMiddleware: UUID y logs seguros; JwtConfiguration: validación inicial y clave compatible; Health: readiness frente a vida; ProxyErrorMiddleware: transporte frente a negocio |
| Auth | passwords/tokens: formatos Identity y derivación de clave; identity.guard/controller: estado vigente y doble credencial interna; outbox: atomicidad con el cambio de identidad, reenvío e idempotencia del receptor; import-users/database: snapshot no destructivo y migración aditiva/checksum |
| Report | integrations: HTTP a propietarios y límites; application: composición por tipo; persistence: propiedad, metadatos y GridFS; rendering: Jinja2/WeasyPrint aislado y límites; main/auth: autorización, capacidad y PDFs existentes |
| Angular | Interceptores: destino del JWT y errores sin reintentos de escritura; reports.guard: navegación frente a autorización; ReportsPage: capacidades/filtros, concurrencia de historial y liberación Blob; ReportApiService: nombre seguro de descarga |
| Monolito | ExternalIdentity/JwtAuthentication: proyección para FKs y permisos vigentes; AuthIntegrationController: ingesta idempotente por ID; Program: bloqueo de auth local en modo externo y control de migraciones; repositorio de lecturas: última fila determinista antes de paginar |

Se agregan comentarios/XML/JSDoc/docstrings para decisiones; no se comenta cada
asignación, endpoint obvio o DTO. Los comentarios no sustituyen pruebas ni deben
prometer una transacción entre GridFS/metadatos o una cola global que no existe.

## Contratos y configuración

Angular mantiene API_BASE_URL `/api`, nunca URLs de bases o servicios internos.
Los filtros de Report vienen de `/api/reports/types`; cambiar tipo limpia selección.
No inferir conectividad física a partir de estado administrativo ni precipitación
acumulada a partir de niveles. Mantener N/D para información ausente.

Una ruta anidada de comunidad puede pertenecer a Sensor/Alert, no a Community.
Cambiar ClusterId conservando Match/Order, probar contratos y ajustar clientes
internos de Report. Agregar readiness y redes del nuevo destino cuando exista.
No activar DNS ficticios, cambiar Angular para cada extracción ni compartir bases.

La importación Auth se realiza antes del corte; no sobreescribe cuentas ya
administradas en PostgreSQL. Las migraciones versionadas no se editan después de
aplicarse. Evitar publicar SQL/PG/Mongo al host salvo un override local aislado de
pruebas con loopback, nunca en el Compose entregado.

## Comprobaciones para cambios futuros

Ejecutar las suites pertinentes de [pruebas](pruebas.md), revisar 401/403 y propiedad,
contratos JSON, filtros/paginación y rutas anidadas. Cambios de plantillas exigen
PDFs reales y renderizado; no basta extraer texto. Cambios de persistencia requieren
base real, no solo mocks o EF InMemory. Mantener evidencia y su contexto/fecha.

Para documentación únicamente, comparar el código ejecutable antes/después y
validar enlaces/rutas/espacios. No atribuir una regresión previa a la revisión
actual ni instalar dependencias para obtener capturas. Capturas pendientes del
manual deben completarse manualmente con cuentas/datos de prueba.

## Dependencias del equipo

El README del repositorio identifica a Astrid como Product Owner y Walter como
Developer, pero no asigna aquí un servicio concreto a cada uno. No se inventa ese
reparto. Requieren confirmación del equipo:

- Astrid: validación funcional de textos, alcance del manual, permisos de usuario y
  criterios de aceptación; aprobación del reparto y del corte, según su rol de producto.
- Walter y los responsables que el equipo asigne: URLs internas reales, imágenes,
  contratos/versiones, health, persistencia, migración y operación de Community,
  Sensor, Alert y Audit. La correspondencia nombre→servicio está **PENDIENTE**.
- Equipo: certificar cobertura de auditoría, paridad del dashboard compuesto por
  HTTP, recuperación/retención y disponibilidad parcial en K3s.

Community y Alert ya se incorporaron selectivamente; Sensor y Audit cuentan con
implementaciones mínimas verificadas. La aceptación de negocio y operación por
Astrid/Walter sigue pendiente, sin confundirla con ausencia de estos servicios.
