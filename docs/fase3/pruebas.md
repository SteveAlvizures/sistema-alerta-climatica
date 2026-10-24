# Pruebas y evidencia vigente

Ejecución del 23/10/2026 (America/Guatemala), rama `integracion-final-fase3`.
Los timestamps UTC de algunos logs corresponden al 24/10. Las pruebas se ejecutaron
en entornos aislados; no certifican producción, carga sostenida ni paridad completa.

| Bloque | Resultado local de este cierre |
|---|---|
| Gateway build Release | 0 errores, 0 advertencias en compilación secuencial |
| Gateway | 58 aprobadas, 0 fallidas/omitidas; interoperabilidad Auth real |
| Auth | 43 aprobadas: 27 unitarias/seguridad y 16 PostgreSQL real |
| Auth build / npm audit | Build correcto; 0 vulnerabilidades reportadas tras actualizar lock/override Multer |
| Community | 8 aprobadas, 0 fallidas/omitidas |
| Sensor | 8 aprobadas, 0 fallidas/omitidas |
| Alert dominio | 11 aprobadas; la prueba opcional de PostgreSQL se omite en el build ordinario |
| Alert PostgreSQL real | 1 aprobada por separado: asociación alerta-evento, replay idempotente y outbox |
| Audit MongoDB real | 11 aprobadas, 0 fallidas |
| Report | 53 pytest aprobadas; MongoDB real y WeasyPrint; Ruff limpio |
| Backend | 285 aprobadas: Domain 44, Infrastructure 18, Application 223; SQL real, sin omisiones |
| Angular | 282 aprobadas; build de producción correcto |
| Docker | 16 contenedores saludables, imágenes construidas localmente |
| Compose config | Final y producción válidos; la configuración entregable solo publica frontend |
| E2E integrado | 52 comprobaciones HTTP aprobadas; tres roles, lectura, alerta, cierre de evento y cuatro auditorías de dominio |
| E2E Report | 33 comprobaciones aprobadas; ocho tipos PDF, descarga, historial y validación |
| Fallos controlados | 7 comprobaciones aprobadas: exceso de filas 422, Community detenido 502, readiness 503, liveness 200 y recuperacion |

Avisos: Angular conserva el aviso previo de presupuesto SCSS (7.02 kB frente a
6 kB); Karma cerró Chrome forzosamente después de SUCCESS. FastAPI TestClient
advierte de deprecación HTTPX; Java muestra avisos del agente Mockito/ByteBuddy.
No se ocultaron avisos cambiando presupuestos. La auditoría npm no sustituye una
revisión de seguridad de todas las imágenes y dependencias.

## Reproducir las suites

Desde la raíz:

```powershell
dotnet restore gateway/ClimateAlert.Gateway.sln
dotnet build gateway/ClimateAlert.Gateway.sln -c Release --no-restore -m:1
dotnet test gateway/ClimateAlert.Gateway.sln -c Release --no-build --no-restore -m:1
dotnet test backend/ClimateAlert.sln -c Release -m:1
dotnet build tools/ClimateAlert.AuthMigration/ClimateAlert.AuthMigration.csproj -c Release -m:1
dotnet tools/ClimateAlert.AuthMigration/bin/Release/net10.0/ClimateAlert.AuthMigration.dll fixtures services/auth-service/test/fixtures/generated.json
```

Para incluir interoperabilidad Gateway, configurar `AUTH_INTEROP_URL` hacia una
instancia Nest de pruebas y `AUTH_INTEROP_FIXTURE` al fixture generado. Sin esas
variables se omiten dos pruebas; no describir esa ejecución como la regresión
completa. Las pruebas Gateway usan Kestrel y EF InMemory para negocio; no sustituyen
SQL Server. Backend requiere `CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING` para sus
casos SQL; usar una instancia dedicada, nunca la base real de negocio.

En `services/auth-service`: `npm ci`, `npm run build`, `npm run test:unit`.
Integración requiere PostgreSQL de
[docker-compose.auth-test.yml](../../deploy/compose/docker-compose.auth-test.yml),
`AUTH_TEST_PASSWORD` privado para Compose, `AUTH_INTEGRATION_TESTS=true`,
`AUTH_DATABASE_HOST=127.0.0.1`, `AUTH_DATABASE_PORT=55432`,
`AUTH_DATABASE_NAME=auth_integration_test`, `AUTH_DATABASE_USER=auth_test`,
`AUTH_DATABASE_PASSWORD` igual a la clave privada usada por Compose y
`AUTH_TEST_CONTAINER=climate-alert-auth-tests-postgres-1`. Entonces ejecutar
`npm run test:integration`. La suite limpia solo su base `_test` y reinicia su
contenedor dedicado. Variables completas en
[integration.test.ts](../../services/auth-service/test/integration.test.ts).

```powershell
docker compose -f deploy/compose/docker-compose.report-test.yml run --build --rm tests
docker compose -f deploy/compose/docker-compose.report-test.yml run --rm tests ruff check app tests
```

Report test usa su Mongo independiente y solo limpia `reports_test`; genera PDFs
reales con fixtures HTTP para fuentes/Auth. En `frontend/angular-app`, ejecutar
`npm run build` y `npm test -- --watch=false --browsers=ChromeHeadless --reporters=dots`.

## Nuevos propietarios y E2E

```powershell
dotnet test services/community-tests/Community.Tests.csproj -c Release -m:1
dotnet test services/sensor-tests/Sensor.Tests.csproj -c Release -m:1
docker build --target build -t climate-alert-build services/alert-service
```

La prueba `PersistenceTests` requiere `ALERT_INTEGRATION_TESTS=true` y las variables
Spring de una base PostgreSQL dedicada: `SPRING_DATASOURCE_URL`,
`SPRING_DATASOURCE_USERNAME`, `SPRING_DATASOURCE_PASSWORD`. Ejecutar
`mvn -B -Dtest=PersistenceTests test` en la imagen build conectada a esa red.
No ejecutarla contra una base productiva. Audit se verifica con
`python -m unittest discover -s tests -v` dentro de su contenedor: crea y limpia
solo colecciones de prefijo `audit_test_` en su Mongo propio.

Configurar privadamente `PHASE3_E2E_URL`, `PHASE3_E2E_USERNAME`,
`PHASE3_E2E_PASSWORD` y `PHASE3_E2E_ALLOW_WRITES=true`; ejecutar
`python tools/validate-phase3-final.py`. Crea datos de prueba y verifica contratos;
no es una consulta de solo lectura. Para PDF, configurar `REPORT_E2E_URL`,
`REPORT_E2E_USERNAME`, `REPORT_E2E_PASSWORD` y ejecutar
`python tools/validate-reports.py`. No escribir credenciales en comandos compartidos.

## PDF y revisión visual

| PDF de la última ejecucion | Páginas | Bytes |
|---|---:|---:|
| Comunidades | 2 | 15163 |
| Sensores | 5 | 24235 |
| Lecturas | 1 | 12231 |
| Alertas | 3 | 17881 |
| Historial de eventos | 3 | 17022 |
| Estado de sensores | 5 | 23301 |
| Estadísticas climáticas | 2 | 14046 |
| Resumen ejecutivo | 2 | 15096 |

Se comprobaron título, usuario, fecha UTC, filtros, numeración, firma/MIME y tamaño.
Se inspeccionaron primeras páginas renderizadas de los ocho documentos y página 2
de 27 de un fixture con nombres largos y HTML escapado. No se observaron cortes ni
superposiciones en esas muestras; no se afirma revisión visual de todas las páginas.

Evidencia local ignorada por Git: `tmp/final-e2e-results.json`,
`tmp/final-failures-results.json`, `tmp/alert-real-postgres.log` y
`tmp/report-evidence/` (PDF, imágenes y resultados). Los auxiliares de caídas solo
detuvieron servicios del stack aislado y restauraron su disponibilidad.

## Pendiente

Inspección interactiva de Angular y capturas del manual por falta de navegador
integrado. ChromeHeadless sí estuvo disponible para las pruebas automatizadas.
Faltan carga sostenida, paridad completa del dominio, restauración integral,
TLS/despliegue real y K3s. Los servicios ya existen; falta certificar el corte
productivo y los criterios de aceptación del equipo. La opción Reportes del
sidebar ya es única en la rama base de Steve; no se modificó Angular en este cierre.
