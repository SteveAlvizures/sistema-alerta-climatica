# Validación del tercer incremento: Report Service

Fecha: 22 de octubre de 2026. Rama: `steve-fase3`.
Base de trabajo: `a41f2e9`. Sin commit ni push realizados por el agente.

## Resultado y arquitectura

Se implementaron Report FastAPI/Python, MongoDB propio con GridFS, Jinja2/WeasyPrint,
ocho reportes PDF, historial/descarga propios y la sección Angular integrada. Todo
tráfico Angular sigue en `/api` por Nginx/Gateway. Auth conserva PostgreSQL; Report
solo consulta APIs HTTP y su Mongo. El monolito conserva el resto de módulos/SQL.

Se añadió únicamente una API de últimas lecturas por lote al propietario actual de
lecturas, para evitar recorrer 69,120 filas históricas o hacer N+1 por sensor. No
hay migración de esquema ni eliminación de datos del sistema existente.

El stack aislado queda disponible en `http://localhost:8083/reports`, con login del
entorno de validación y sin publicar Report/Mongo. Esta URL es exclusivamente local.

## Pruebas y resultados

| Área | Resultado final |
| --- | --- |
| Python / FastAPI / Mongo / PDF | 36 pruebas con Mongo real, incluyendo los ocho PDF con WeasyPrint |
| Ruff | `All checks passed` |
| Gateway | 58/58; incluye las 49 previas, Auth real/JWT .NET-Nest, siete casos Report y dos del endpoint por lote |
| Auth | `npm ci`, build y 43/43 (27 unitarias + 16 de integración PostgreSQL real), sin omisiones |
| Backend | 285/285 con SQL real: Domain 44, Infrastructure 18, Application 223; sin omisiones |
| Angular | 279/279 ChromeHeadless, incluyendo ruta, guard, tipos, filtros, validación, loading, errores, historial y Blob |
| Angular producción | Build correcto; sin bibliotecas PDF frontend añadidas |
| Compose | Base, desarrollo y producción válidos; imágenes construidas; ocho servicios saludables |
| E2E por Nginx/Gateway | 31 comprobaciones/entradas de evidencia y ocho archivos generados/descargados |
| Seguridad/persistencia/regresión HTTP | 39 comprobaciones; ocho PDF conservados tras reinicio Mongo/Report |
| Caídas upstream/Report y logs | 10 comprobaciones adicionales |

El build Angular conserva la advertencia preexistente de `sensors-page.scss` (7.02 kB
frente al presupuesto de advertencia de 6 kB). No se elevó el presupuesto. Los estilos
nuevos pasan el build. TestClient/Starlette emite una deprecación de su integración
con httpx; las pruebas pasan. No se presenta la advertencia como un fallo funcional.

Se fijó explícitamente la ruta de importación de pytest y de la imagen Python para
que las pruebas usen la fuente actual, en lugar de una copia instalada previamente.
La suite completa se repitió después de esta corrección y de verificar el conteo N/D.

Las dos pruebas SQL nuevas verifican la lectura más reciente por sensor contra SQL
Server real, tanto con filtro de sensor como con filtro de comunidad. La consulta
incluye paginación, carga de nombres y orden determinista. Gateway comprueba también
respuesta 200 y validación 400 de esta API aditiva.

## Evidencia E2E final

`python tools/validate-reports.py`, usando credenciales desde el entorno, ejecutó
login 200, tipos sin bearer/inválido 401, tipos válidos 200, ocho POST 201, ocho
descargas 200, historial 200 y rango inválido 400. Verificó estado Completed,
`application/pdf`, cabecera `%PDF-` y tamaño idéntico al de metadata.

| Reporte | ID final | PDF (bytes) |
| --- | --- | ---: |
| Comunidades | 04be0df6-54c7-4b26-a1fb-076e85f86876 | 14,391 |
| Sensores | 30c53b29-b468-4c66-9d5b-b98523269214 | 23,329 |
| Lecturas | 6f717cf2-c42e-4ee1-b6cf-60202153bc2a | 69,506 |
| Alertas | b4adcf55-4652-4018-8958-fc3a57ccb233 | 16,734 |
| Eventos | 1641bd64-e6da-45ac-865e-b651973717a0 | 15,955 |
| Estado de sensores | a3881e66-3b39-4608-86e9-dd6782551bb3 | 22,229 |
| Estadísticas | 3c6fccce-2161-4b01-b928-b9eb4d7c3661 | 14,483 |
| Ejecutivo | 505ea025-d573-4311-a485-ae850529a743 | 15,469 |

Datos de la copia existente: ocho comunidades, 38 sensores, 14 alertas, 14 eventos y
69,120 lecturas históricas. Para lecturas/estadísticas/ejecutivo el smoke final usó
los cinco minutos anteriores a la lectura más reciente; alertas/eventos sin rango.
La solicitud sin filtros de las 69,120 lecturas devolvió 422 y metadata Failed,
sin generar un documento truncado. Los valores y faltantes de la fuente se conservaron.

Evidencia local ignorada por Git: `tmp/report-evidence/http-results.json`,
`edge-results.json`, `failure-results.json`, `docker-health.json`, los ocho PDF,
PDF de fixtures, renderizados PNG y PDF multipágina. No se versionan datos de
validación, credenciales, exportaciones de usuarios ni tokens. Tamaños/IDs pueden
variar en nuevas ejecuciones por fechas, referencias y contenido del sistema.

## MongoDB y estados

- Inserción, actualización, recuperación, historial paginado e índices probados contra MongoDB real.
- Metadata propia y GridFS consultados por separado en las pruebas, sin mock de almacenamiento.
- Secuencia Pending/Processing/Completed y Failed ante fuente indisponible.
- Solicitud interrumpida/antigua pasa a Failed al consultarse.
- Reinicio del contenedor Mongo y de Report: ocho metadata y ocho PDF previos siguen accesibles con el mismo tamaño.
- Con Mongo detenido: Report live 200, Report ready 503, historial 503; Gateway live 200 y ready 503.
- Mongo sin puertos públicos, volumen propio y usuario de aplicación limitado a su base.

Los datos del stack original Fase 2 no se borraron. La validación usó el volumen SQL
de copia `climate-alert-phase3-validation-data`, PostgreSQL de validación y Mongo
`climate-alert-report-validation-data`. Los tests automáticos usan otra base/volumen
Mongo (`reports_test`) y PostgreSQL/SQL temporales aislados. La limpieza del fixture
SQL solo elimina bases generadas con su prefijo protegido.

## Seguridad, errores y casos límite

Probados: bearer ausente/inválido, issuer/audience/expiración incorrectos, rol sin
permiso, cambio de rol con JWT anterior, cuenta desactivada, acceso a recurso ajeno,
reporte inexistente, UUID inválido, fecha sin zona horaria, rango invertido, tipo
desconocido, estado/filtro incompatible, campos desconocidos y página inválida.

También: conjunto vacío, filtros sin resultados, muchas filas, PDF multipágina,
escape de HTML, respuesta de fuente incompleta, límite de registros, timeout HTTP,
terminación del render por timeout, servidor ocupado 429, metadata inconsistente,
archivo ausente, descarga repetida, persistencia después de reinicio y fuentes caídas.

Se detuvo exclusivamente el backend de validación: POST Report respondió 502,
tipos continuó 200, Gateway live 200/ready 503. Al detener Report, Gateway respondió
502 seguro, live 200/ready 503. Se recuperaron los servicios después de cada prueba.
Se revisaron logs de Report/Gateway/Auth sin encontrar las credenciales configuradas
ni el JWT usado. Las respuestas no mostraron direcciones internas ni stack traces.

Gateway prueba 401/403/404/503, upstream 502, limpieza de headers internos y correlation
ID en la ruta Report. Los timeouts 504 y CORS de la suite previa siguen pasando.
El route ID interno `reports-reserved` se conserva para compatibilidad de pruebas;
su cluster ahora es `reports`, no el monolito. Ese nombre no se expone al usuario.

## PDF y revisión visual

Se extrajo texto con pypdf para verificar título, datos de fixture, filtros, autor,
marca, paginación y filas finales. El caso de textos largos genera 27 páginas para
130 registros, con encabezados repetidos, contenido escapado y última fila presente.
Los ocho tipos generaron PDF reales; alertas/eventos se verificaron además con sus
14 registros reales del stack.

Los PDF se renderizaron con Poppler y se inspeccionaron las primeras páginas de
los ocho tipos, páginas interiores del ejecutivo y del caso largo. Se revisaron
alineación, contraste, tamaños, tablas, falta de datos, encabezados, pie y números
de página. Se corrigió el formato de fechas UTC para evitar cadenas ISO extensas
en columnas estrechas. No se usan screenshots como sustituto de la validez del PDF.

El PDF final de lecturas contiene 222 mediciones de 37 sensores en 21 páginas.
Comunidades: 2 páginas; sensores: 5; alertas: 3; eventos: 3; estado de sensores: 5;
estadísticas: 2; ejecutivo: 2. Se conservaron tanto filas como unidades y estados.

**Inspección visual interactiva en navegador pendiente:** la herramienta reportó
cero navegadores disponibles y el navegador integrado no está habilitado. El usuario
autorizó continuar sin bloquear el incremento. Se revisaron código, estilos, temas,
breakpoints, accesibilidad básica, pruebas Angular y build de producción. No se
instalaron navegadores ni dependencias extra para eludir esa limitación. No se afirma
haber realizado una revisión interactiva desktop/móvil del frontend.

## Performance y límites reales

- Fuentes paginadas de 50 registros; 2,000 por fuente por defecto; error explícito, nunca truncado silencioso.
- Para el conjunto de validación: comunidades/sensores/alertas/eventos requieren una página cada uno.
- Estado de sensores usa dos consultas HTTP paginadas (sensores + últimas lecturas), no una por sensor.
- Ejecutivo usa cinco clientes; lecturas necesita tantas páginas como requiera el rango acotado.
- Timeout HTTP de tres segundos, un reintento seguro, 22 segundos de datos/render; PDF máximo 10 MB.
- Una generación por proceso; 429 cuando está ocupado. Historial/descargas no necesitan consultar fuentes de negocio.
- Render en proceso separado; contenedor limitado a 512 MB/una CPU. No se ha hecho benchmark de carga sostenida ni garantía de escalado horizontal.

La consistencia entre APIs es de corte de consulta, no transaccional. Las estadísticas
son descriptivas, sin pronóstico ni agregación temporal sofisticada. Lluvia acumulada
sigue N/D por falta de semántica de incrementos. No hay generación asíncrona durable,
cola global, retención/limpieza automática de PDF ni eliminación de huérfanos por crash.
Estas son limitaciones documentadas, no funcionalidades presentadas como terminadas.

La readiness global de Gateway incluye Report; si Report falla, Gateway queda
Unhealthy aunque su proceso continúe atendiendo otras rutas. En un despliegue K3s
debe decidirse si se conserva esta política global o se requieren rutas con
disponibilidad parcial. No se desplegaron manifiestos K3s en este incremento.

## Cambios de interfaz

Reportes aparece en sidebar para Administrator/Operator; ruta protegida y carga lazy.
Dashboard agrega solo acceso rápido. Formulario de dos columnas con filtros dinámicos,
rango válido, feedback y botón bloqueado durante generación. Historial presenta
tipo, fecha, usuario, alcance, estado, tamaño y descarga con paginación 10/20/50.
Descarga Blob toma `Content-Disposition`; errores de sesión/permisos son comprensibles.
Se reutilizan variables de tema, radios, paneles, badges y patrones actuales; una
columna en pantallas pequeñas y desplazamiento horizontal de tablas. No hay datos
ficticios añadidos al dashboard ni generación PDF en Angular.

## Archivos y reproducibilidad

El árbol completo de Report está en [report-service.md](report-service.md), que
también documenta endpoints, configuración, Mongo, JWT, clientes, Docker, K3s y
**CÓMO DEFENDER REPORT SERVICE**. La arquitectura resultante está en
[arquitectura.md](arquitectura.md). El script E2E entregable es
`tools/validate-reports.py`; pruebas Mongo/PDF en
`deploy/compose/docker-compose.report-test.yml`.

La sección siguiente registra `git status --short`, archivos creados/modificados y
`git diff --stat` al finalizar. El diff estándar solo incluye archivos ya rastreados;
los nuevos se muestran como `??` porque no se hizo staging, commit ni push.

## Inventario final de archivos

34 archivos nuevos y 18 archivos rastreados modificados.

### Creados

```text
deploy/compose/docker-compose.report-development.yml
deploy/compose/docker-compose.report-test.yml
deploy/mongo/init-report.js
docs/fase3/report-service.md
docs/fase3/validacion-reportes.md
frontend/angular-app/src/app/core/guards/reports.guard.spec.ts
frontend/angular-app/src/app/core/guards/reports.guard.ts
frontend/angular-app/src/app/core/models/report.model.ts
frontend/angular-app/src/app/core/services/report-api.service.ts
frontend/angular-app/src/app/features/reports/reports-page.html
frontend/angular-app/src/app/features/reports/reports-page.scss
frontend/angular-app/src/app/features/reports/reports-page.spec.ts
frontend/angular-app/src/app/features/reports/reports-page.ts
gateway/src/ClimateAlert.Gateway/Health/ReportUpstreamHealthCheck.cs
gateway/tests/ClimateAlert.Gateway.Tests/ReportRoutingTests.cs
services/report-service/.dockerignore
services/report-service/.env.example
services/report-service/Dockerfile
services/report-service/app/__init__.py
services/report-service/app/application.py
services/report-service/app/auth.py
services/report-service/app/config.py
services/report-service/app/errors.py
services/report-service/app/integrations.py
services/report-service/app/main.py
services/report-service/app/models.py
services/report-service/app/persistence.py
services/report-service/app/rendering.py
services/report-service/app/templates/base.html
services/report-service/app/templates/report.html
services/report-service/pyproject.toml
services/report-service/requirements.lock
services/report-service/tests/test_reports.py
tools/validate-reports.py
```

### Modificados

```text
.env.example
.gitignore
backend/src/ClimateAlert.Api/Controllers/SensorReadingsController.cs
backend/src/ClimateAlert.Application/Common/Interfaces/PersistenceContracts.cs
backend/src/ClimateAlert.Application/Features/SensorReadings/SensorReadingService.cs
backend/src/ClimateAlert.Infrastructure/Persistence/Repositories.cs
backend/tests/ClimateAlert.Application.Tests/SqlServerAtomicityTests.cs
deploy/compose/docker-compose.phase3.yml
docs/fase3/arquitectura.md
frontend/angular-app/src/app/app.routes.ts
frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.html
frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.ts
frontend/angular-app/src/app/layout/sidebar/sidebar.spec.ts
frontend/angular-app/src/app/layout/sidebar/sidebar.ts
frontend/angular-app/src/styles.scss
gateway/src/ClimateAlert.Gateway/Program.cs
gateway/src/ClimateAlert.Gateway/appsettings.json
gateway/tests/ClimateAlert.Gateway.Tests/GatewayFixture.cs
```

### git status --short

```text
 M .env.example
 M .gitignore
 M backend/src/ClimateAlert.Api/Controllers/SensorReadingsController.cs
 M backend/src/ClimateAlert.Application/Common/Interfaces/PersistenceContracts.cs
 M backend/src/ClimateAlert.Application/Features/SensorReadings/SensorReadingService.cs
 M backend/src/ClimateAlert.Infrastructure/Persistence/Repositories.cs
 M backend/tests/ClimateAlert.Application.Tests/SqlServerAtomicityTests.cs
 M deploy/compose/docker-compose.phase3.yml
 M docs/fase3/arquitectura.md
 M frontend/angular-app/src/app/app.routes.ts
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.html
 M frontend/angular-app/src/app/features/dashboard/pages/dashboard-page/dashboard-page.ts
 M frontend/angular-app/src/app/layout/sidebar/sidebar.spec.ts
 M frontend/angular-app/src/app/layout/sidebar/sidebar.ts
 M frontend/angular-app/src/styles.scss
 M gateway/src/ClimateAlert.Gateway/Program.cs
 M gateway/src/ClimateAlert.Gateway/appsettings.json
 M gateway/tests/ClimateAlert.Gateway.Tests/GatewayFixture.cs
?? deploy/compose/docker-compose.report-development.yml
?? deploy/compose/docker-compose.report-test.yml
?? deploy/mongo/
?? docs/fase3/report-service.md
?? docs/fase3/validacion-reportes.md
?? frontend/angular-app/src/app/core/guards/reports.guard.spec.ts
?? frontend/angular-app/src/app/core/guards/reports.guard.ts
?? frontend/angular-app/src/app/core/models/report.model.ts
?? frontend/angular-app/src/app/core/services/report-api.service.ts
?? frontend/angular-app/src/app/features/reports/
?? gateway/src/ClimateAlert.Gateway/Health/ReportUpstreamHealthCheck.cs
?? gateway/tests/ClimateAlert.Gateway.Tests/ReportRoutingTests.cs
?? services/report-service/
?? tools/validate-reports.py
```

### git diff --stat

```text
 .env.example                                       |  6 +++
 .gitignore                                         |  7 ++-
 .../Controllers/SensorReadingsController.cs        |  8 +++
 .../Common/Interfaces/PersistenceContracts.cs      |  3 ++
 .../SensorReadings/SensorReadingService.cs         | 12 +++++
 .../Persistence/Repositories.cs                    | 18 +++++++
 .../SqlServerAtomicityTests.cs                     | 26 +++++++++
 deploy/compose/docker-compose.phase3.yml           | 62 ++++++++++++++++++++++
 docs/fase3/arquitectura.md                         | 27 +++++++---
 frontend/angular-app/src/app/app.routes.ts         |  2 +
 .../pages/dashboard-page/dashboard-page.html       |  1 +
 .../pages/dashboard-page/dashboard-page.ts         |  5 +-
 .../src/app/layout/sidebar/sidebar.spec.ts         |  7 ++-
 .../angular-app/src/app/layout/sidebar/sidebar.ts  |  5 +-
 frontend/angular-app/src/styles.scss               |  2 +
 gateway/src/ClimateAlert.Gateway/Program.cs        |  5 +-
 gateway/src/ClimateAlert.Gateway/appsettings.json  |  6 ++-
 .../ClimateAlert.Gateway.Tests/GatewayFixture.cs   | 11 +++-
 18 files changed, 199 insertions(+), 14 deletions(-)
```

Este comando excluye los 34 archivos nuevos sin staging. `git diff --check`
no presenta errores de espacios. HEAD permanece en `a41f2e9`, sin staging, commit ni push.
