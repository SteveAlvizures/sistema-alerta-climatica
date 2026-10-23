# Manual técnico

## Estado y tecnologías

**IMPLEMENTADO:** Angular 22.2/TypeScript 6, Gateway ASP.NET Core 10/YARP 2.3,
Auth NestJS 11/Node 24 con PostgreSQL 17, Report FastAPI/Python 3.13 con MongoDB 8,
Jinja2 3.1.6 y WeasyPrint 68.1. El monolito .NET 10 conserva SQL Server 2022.
Versiones concretas de paquetes: package-lock de Angular/Auth, proyectos .NET y
`services/report-service/requirements.lock`; no actualizarlas durante una instalación.

**PROBADO LOCALMENTE:** builds, suites y stack aislado indicados en
[pruebas](pruebas.md). **PENDIENTE:** extracción de Community/Sensor/Alert/Audit,
OAuth/OIDC (Fase 4), manifiestos K3s y despliegue de producción verificado.

Consulta [arquitectura](arquitectura.md) para componentes y flujos, y
[seguridad](seguridad.md) para la frontera de confianza.

## Requisitos e instalación

Para Compose: Docker Engine con Compose v2 y soporte para los contenedores Linux
de las imágenes declaradas, espacio para volúmenes e imágenes y una base SQL de
Fase 2 inicializada o restaurada. No se ha establecido un mínimo de RAM global
por pruebas de carga; Report tiene límite de 512 MB y una CPU en Compose.

Para desarrollo nativo: SDK .NET 10, Node 24 (Docker fija 24.15.0), npm del entorno,
Python 3.13 y las bases accesibles desde el proceso que las usa. WeasyPrint requiere
Pango/HarfBuzz y fuentes; se recomienda la imagen Linux entregada para reproducirlo.
Karma necesita Chrome/Chromium compatible. No se exige un navegador para operar
los servicios backend.

Desde la raíz, crear `.env` a partir de [.env.example](../../.env.example) y completar
los valores reales fuera de Git. [deploy/compose/.env.example](../../deploy/compose/.env.example)
describe opciones adicionales de Fase 3; no sustituye la configuración raíz.
Antes del primer arranque, preparar volumen SQL y realizar el corte de identidad
documentado en [despliegue](despliegue.md). Auth no crea un administrador desde cero:
se importan las cuentas existentes.

## Variables y configuración

| Grupo | Variables / finalidad |
|---|---|
| JWT compartido | `JWT_KEY` (mínimo 24 caracteres), `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES` (Auth/monolito) |
| Integración interna | `AUTH_INTERNAL_KEY` (mínimo 32), `AUTH_SERVICE_URL` (Report/monolito), `AUTH_AUDIT_URL` (Auth) |
| SQL existente | `SQLSERVER_PASSWORD`, `SQLSERVER_CONNECTION_STRING`, `PHASE3_SQL_VOLUME`, `SQLSERVER_EDITION` |
| Auth PostgreSQL | `AUTH_DATABASE_HOST`, `AUTH_DATABASE_PORT`, `AUTH_DATABASE_NAME`, `AUTH_DATABASE_USER`, `AUTH_DATABASE_PASSWORD`, `AUTH_POSTGRES_VOLUME` |
| Report Mongo | `REPORT_MONGO_HOST`, `REPORT_MONGO_PORT`, `REPORT_MONGO_DATABASE`, `REPORT_MONGO_USER`, `REPORT_MONGO_PASSWORD`, `REPORT_MONGO_ROOT_PASSWORD` (init), `REPORT_MONGO_VOLUME` |
| Entrada web | `FRONTEND_BIND` (127.0.0.1), `FRONTEND_PORT` (8082), `FRONTEND_ORIGIN`; CORS Gateway `Gateway__AllowedOrigins__0` |
| Fuentes de Report | `COMMUNITY_API_URL`, `SENSOR_API_URL`, `READING_API_URL`, `ALERT_API_URL`, `EVENT_API_URL` |
| Simulación | `SIMULATED_READING_INTERVAL_SECONDS`; 0 desactiva nuevas lecturas simuladas |

En Compose las fuentes de Report se toman de `REPORT_COMMUNITY_API_URL`,
`REPORT_SENSOR_API_URL`, `REPORT_READING_API_URL`, `REPORT_ALERT_API_URL`,
`REPORT_EVENT_API_URL` y se inyectan con los nombres `*_API_URL` internos. Host/puertos
de bases, Auth, Report y destinos YARP están fijados por DNS interno en Compose;
no todas las variables del proceso se interpolan desde `.env`. Cambiar una variable
sin mapping en el YAML no modifica el contenedor.

Report admite `REPORT_MAX_ROWS=2000` (50..10000), `REPORT_MAX_PDF_BYTES=10000000`
(10000..50000000), `REPORT_DEADLINE_SECONDS=22` (5..25) y
`REPORT_HTTP_TIMEOUT_SECONDS=3` (0.1..5). El Compose actual usa esos defaults:
para modificarlos hay que añadir environment en un overlay. Revisar
[config.py](../../services/report-service/app/config.py) y
[Compose](../../deploy/compose/docker-compose.phase3.yml). Los valores de secretos
no se muestran en manuales, logs ni comandos compartidos.

## Puertos y propiedad de datos

| Componente | Nativo por defecto | DNS/puerto Compose | Persistencia |
|---|---|---|---|
| Angular | 4200 | frontend:80; único puerto host 127.0.0.1:8082 | Sesión en sessionStorage |
| Gateway | 5160 en Development | gateway:8080 | Ninguna |
| Auth | 3000 | auth-service:3000 | auth-postgres:5432, base climate_auth |
| Report | 8000 | report-service:8000 | report-mongodb:27017, base climate_reports / GridFS |
| Monolito | 5152 | backend:8080 | sqlserver:1433, base configurada por conexión existente |

8083 fue un override exclusivo del stack de validación; no es el puerto entregado
por defecto. Ninguna base ni API interna publica puertos en el Compose oficial.
Un `EXPOSE` del Dockerfile no equivale a un puerto publicado en el host.

## Desarrollo y compilación

```powershell
dotnet restore gateway/ClimateAlert.Gateway.sln
dotnet build gateway/ClimateAlert.Gateway.sln -c Release --no-restore
dotnet restore backend/ClimateAlert.sln
dotnet build backend/ClimateAlert.sln -c Release --no-restore
```

En `services/auth-service`: `npm ci`, `npm run build`, `npm start`, con variables
del [ejemplo Auth](../../services/auth-service/.env.example) cargadas en el entorno.
Nest no carga ese archivo automáticamente. En `frontend/angular-app`: `npm ci` y
`npm start`; el proxy `/api` apunta a Gateway 5160. Para producción, `npm run build`.

En `services/report-service`, dentro de un entorno virtual: `pip install -r
requirements.lock`, `pip install --no-deps -e .` y
`uvicorn app.main:create_app --factory --port 8000`. Report sí lee `.env` desde el
directorio de trabajo; usar su [ejemplo](../../services/report-service/.env.example).
En desarrollo nativo deben iniciarse también bases, Auth y monolito; el proxy
Angular solo reenvía `/api`, por lo que health se consulta directamente a Gateway.
Los builds nativos se probaron en la regresión; no se certificó una instalación
nativa desde cero en todos los sistemas operativos.

## Health, logs y recuperación

Gateway `/health` y `/health/live` comprueban vida. `/health/ready` agrega backend
`/health`, Auth `/health/ready` y Report `/health/ready`, con HTTP timeout de 3 s.
Auth ready consulta PostgreSQL. Report ready comprueba Mongo/índices y Auth; no exige
fuentes para permitir descarga de PDFs ya guardados. Backend `/health` consulta SQL.
Frontend health solo comprueba el documento web.

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml ps
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml logs --tail 100 gateway auth-service report-service
```

Gateway/Auth/Report registran correlación, método, estado y duración sin cuerpos,
tokens o destinos internos. Correlacionar con `X-Correlation-ID` de la respuesta;
el monolito recibe ese header, sin incorporarlo aún a sus propios scopes.

Ante una caída, comprobar primero health y persistencia antes de reiniciar.
La outbox retiene auditoría durante caída del receptor y reintenta con el mismo ID.
Report descarga documentos existentes si Auth/Mongo responden; generaciones
interrumpidas pendientes por más de dos minutos se marcan fallidas al consultar.
Reiniciar servicios no sustituye restaurar una base perdida. Respaldar SQL, PostgreSQL
y Mongo (incluidos reports y GridFS) de forma consistente. Restauración integral y
objetivos RPO/RTO de producción están **PENDIENTES**; no usar `down -v` para recuperar.
Detalles: [despliegue](despliegue.md), [troubleshooting](troubleshooting.md),
[pruebas](pruebas.md) y documentos de cada servicio.
