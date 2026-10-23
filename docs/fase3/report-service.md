# Report Service y Reportes en Angular

## Alcance del tercer incremento

Report Service genera ocho tipos de PDF mediante FastAPI, Jinja2 y WeasyPrint.
MongoDB guarda metadata y archivos en GridFS. El navegador consume exclusivamente
`/api` a través de Nginx/Gateway. No existe conexión de Report con SQL Server ni
con PostgreSQL. No se extraen otros módulos ni se modifica el esquema SQL.

```text
Angular 22 /api
  -> Nginx -> Gateway .NET 10 + YARP
               |-> /api/auth, /api/users -> Auth NestJS -> PostgreSQL de Auth
               |-> /api/reports -> Report FastAPI -> MongoDB de Report / GridFS
               |                      |-> HTTP Auth: identidad vigente
               |                      |-> HTTP APIs: comunidades, sensores,
               |                          lecturas, alertas, eventos
               |-> resto -> monolito .NET -> SQL Server
```

El endpoint aditivo `/api/sensor-readings/latest` pertenece al monolito y devuelve
la última lectura de cada sensor con paginación. Selecciona por fecha y, en empate,
por ID descendente en SQL. Evita leer todo el histórico o ejecutar N+1 consultas
HTTP para el reporte de estado. El servicio Report no conoce esa consulta SQL;
consume el contrato HTTP del propietario de las lecturas.

## Estructura completa

```text
services/report-service/
├── app/
│   ├── __init__.py
│   ├── main.py             # HTTP, ciclo de vida, health, coordinación
│   ├── config.py           # configuración tipada y secretos
│   ├── models.py           # tipos, solicitudes, validación de filtros
│   ├── auth.py             # JWT y autorización con identidad vigente
│   ├── errors.py           # errores controlados
│   ├── integrations.py     # cinco clientes HTTP separados
│   ├── application.py      # recolección y transformación por reporte
│   ├── persistence.py      # metadata, índices, GridFS y recuperación
│   ├── rendering.py        # subproceso aislado de renderizado
│   └── templates/
│       ├── base.html       # identidad, cabecera, alcance, indicadores, CSS
│       └── report.html     # tablas, totales y notas
├── tests/
│   └── test_reports.py
├── pyproject.toml
├── requirements.lock
├── Dockerfile
├── .dockerignore
└── .env.example
```

Se usan módulos pequeños en vez de carpetas con un único archivo. Las fronteras
entre HTTP, aplicación, integración, seguridad, persistencia y presentación siguen
siendo explícitas. `requirements.lock` fija las dependencias transitivas probadas.

## Reportes implementados y semántica

| Tipo | Contenido real |
| --- | --- |
| `communities` | Nombre, municipio, departamento, país, coordenadas, estado, cantidad de sensores de la API |
| `sensors` | Código, nombre, tipo, comunidad, ubicación, unidad, estado, instalación |
| `readings` | Sensor, comunidad, fecha/hora UTC, variable, valor, unidad, estado al medir |
| `alerts` | Fecha/hora, comunidad, sensor, fenómeno, nivel, valor, umbral/rango, mensaje, estado |
| `events` | Fecha, comunidad/sensor, fenómeno/nivel, descripción, estado, alertas asociadas |
| `sensor-status` | Estado administrativo, comunidad, tipo, última comunicación, última lectura/valor y cantidad sin comunicación |
| `statistics` | Promedio aritmético, mínimo, máximo y muestras por variable/unidad; distribución de alertas |
| `executive` | Inventario actual, sensores activos/inactivos, alertas y eventos del período, indicadores climáticos |

`GET /api/reports` implementa el historial, y `/{id}/download` la descarga autorizada.

Los valores faltantes se imprimen como **N/D**, sin sustituirlos por cero. La cantidad
cero de muestras significa que no hay observaciones; promedio/mínimo/máximo siguen
siendo N/D. Las estadísticas no ponderan por tiempo y nunca mezclan unidades.
El nivel de lluvia actual no garantiza ser un incremento por intervalo: **lluvia
acumulada = N/D**. No se suma una serie de niveles ni se inventa precipitación.

La fecha de instalación no se deduce de la creación del registro. La comunicación
no se deduce del estado activo. El estado histórico de una lectura anterior a la
incorporación del snapshot sigue siendo N/D. El estado de sensores utiliza la última
lectura real, aunque su snapshot de estado difiera del estado administrativo actual.

Un reporte ejecutivo con fechas combina inventario actual con actividad detectada
en ese período. Sus alertas abiertas no incluyen alertas detectadas fuera del rango.
Los PDFs explican esta semántica; no constituyen una instantánea transaccional entre
varias APIs. Las lecturas con origen simulado conservan los valores existentes de
la API, sin crear mediciones en Report Service.

## Endpoints y validación

Todos los endpoints `/api/reports` requieren autenticación y permiso de operación.

| Método y ruta | Resultado |
| --- | --- |
| `GET /api/reports/types` | Ocho tipos, nombres, filtros admitidos y estados aplicables |
| `POST /api/reports` | 201 con metadata del PDF terminado |
| `GET /api/reports` | Historial del usuario vigente; `page`, `pageSize`, `type`, `state` |
| `GET /api/reports/{uuid}` | Metadata propia, sin referencia interna de GridFS |
| `GET /api/reports/{uuid}/status` | Metadata/estado propio |
| `GET /api/reports/{uuid}/download` | `application/pdf`, `Content-Disposition` y `no-store` |
| `GET /health`, `/health/live` | Proceso vivo, independiente de otros servicios |
| `GET /health/ready` | MongoDB/índices e identidad Auth disponibles |

Ejemplo de solicitud:

```json
{
  "type": "readings",
  "filters": {
    "dateFrom": "2026-10-01T00:00:00Z",
    "dateTo": "2026-10-22T23:59:59.999Z",
    "variable": "Temperature"
  }
}
```

Filtros opcionales: `communityId`, `sensorId`, `dateFrom`, `dateTo`, `variable`,
`status`. IDs deben ser UUID; fechas deben incluir zona horaria y estar ordenadas.
Se rechazan campos desconocidos, tipos o estados incompatibles. Los inventarios
actuales no aceptan fechas; eventos no acepta variable. Los filtros que la fuente
no ofrece, como ID de comunidad en su listado, se aplican después de una lectura
acotada. En inventarios muy grandes esto puede exigir reducir el conjunto en la
fuente; no se elude el límite para un selector individual.

Errores: 400 validación; 401 sesión inválida/inactiva; 403 sin permiso; 404 recurso
inexistente o de otro propietario; 409 documento no disponible/inconsistente;
422 conjunto o PDF excesivo; 429 capacidad ocupada; 502 fuente no disponible,
respuesta parcial o contrato incompatible; 503 persistencia/identidad indisponible;
504 timeout. Un fallo inesperado devuelve 500 genérico. Los problemas contienen
correlation ID, sin stack, dirección interna ni detalles del motor de persistencia.

## Seguridad y autorización

Report verifica HS256 con `SHA256(UTF8(JWT_KEY))`, issuer, audience, expiración y
`sub` UUID, igual que Auth y .NET. Después consulta
`GET /internal/identity/current` con el bearer y `X-Internal-Service-Key`. Verifica
identidad coincidente, cuenta activa, rol Administrator u Operator y permiso
`OperateSystem`. Una degradación/desactivación se aplica incluso si el JWT no venció.
ConsultationUser recibe 403. No se introduce OAuth/OIDC en este incremento.

Cada usuario consulta y descarga exclusivamente sus propios reportes. El administrador
no obtiene acceso global implícito. Una consulta ajena devuelve 404 para no revelar
la existencia del recurso. No se confía en `X-User-*`, `X-Role-*` ni permisos enviados
por el navegador. Gateway sigue limpiando estos encabezados.

Mongo usa un usuario `readWrite` limitado a la base de Report; la credencial root
solo inicializa la base. Los tokens no se guardan en metadata, GridFS ni trabajos.
Los logs propios contienen método, estado, duración y correlation ID; no URL/query,
body, contraseñas, tokens ni cadenas de conexión. La configuración oculta valores
de entrada en errores de validación. CORS permanece en Gateway y ahora expone
`Content-Disposition` para la descarga del navegador.

## Persistencia y generación

Colección `reports`: UUID, tipo, usuario (ID/nombre), solicitud UTC, filtros, estado,
archivo, generación, última descarga, tamaño, error controlado y correlation ID.
Índices: usuario, tipo, fecha, estado, usuario/fecha/ID y estado/fecha. GridFS utiliza
`pdfs.files`/`pdfs.chunks` e índices propios. El volumen es `report-mongodb-data`.

La generación es síncrona: Pending -> Processing -> Completed o Failed. Se guarda
el PDF antes de publicar Completed; si falla la actualización de metadata se intenta
eliminar el archivo recién creado. Una interrupción abrupta entre escrituras puede
dejar archivos huérfanos: no hay transacción distribuida ni recolector automático.
Solicitudes abandonadas por más de dos minutos pasan a Failed al consultar historial
o metadata. No se reejecutan automáticamente sin credenciales del solicitante.

Descarga comprueba estado, referencia, tamaño, existencia y cabecera PDF. La fecha
`downloadedAt` indica la última entrega HTTP iniciada, no que el usuario guardó el
archivo en disco. Descargas repetidas están permitidas. No hay borrado ni caducidad
automática; retención y limpieza necesitan una política futura.

El render se ejecuta en un subproceso cancelable. Jinja2 escapa datos y WeasyPrint
rechaza recursos externos/locales mediante un fetcher cerrado. Solo recibe datos
del reporte: nunca bearer o secretos. Las plantillas A4 horizontal contienen marca,
alcance, autor, corte UTC, indicadores, tablas con encabezados repetidos, totales,
notas y números de página. El backend genera el PDF completo antes de descargarlo.

## Integraciones y límites

| Cliente | Configuración | Contrato HTTP |
| --- | --- | --- |
| CommunityClient | `COMMUNITY_API_URL` | `/api/communities?page&pageSize` |
| SensorClient | `SENSOR_API_URL` | `/api/sensors?page&pageSize` |
| ReadingClient | `READING_API_URL` | `/api/sensor-readings`, `/api/sensor-readings/latest` |
| AlertClient | `ALERT_API_URL` | `/api/alerts` |
| EventClient | `EVENT_API_URL` | `/api/events`; adapta fechas a `from`/`to` |

Para extraer Community/Sensor/Alert más adelante, apuntar estas URLs a sus servicios
y conservar los contratos. Compose permite `REPORT_COMMUNITY_API_URL`,
`REPORT_SENSOR_API_URL`, `REPORT_READING_API_URL`, `REPORT_ALERT_API_URL`,
`REPORT_EVENT_API_URL`. No es necesario modificar el motor de PDF.

Lotes de 50 filas; máximo predeterminado de 2,000 por fuente (configurable hasta
10,000). Cada respuesta se lee en streaming y se limita a 2 MB antes de decodificar.
Se comprueba el total y la continuidad; una página fallida no produce un reporte
Completed parcial. Consultas concurrentes pueden cambiar los datos entre páginas;
la comprobación de totales detecta algunos cambios, no todos. Un snapshot entre APIs
requiere contratos/versionado posteriores.

Timeout HTTP de 3 segundos; un único reintento para timeout o HTTP 502/503/504 en
GET, sin reintentos de POST. Se propagan correlation ID y cancelación. Presupuesto
de datos/render: 22 segundos, inferior al timeout YARP de 30 y Angular de 40.
Las escrituras/lecturas de Mongo tienen sus propios timeouts de 2-3 segundos. No
existe garantía de latencia máxima bajo saturación del motor de almacenamiento.

Máximo PDF: 10 MB; una generación por proceso. El resto recibe 429 y puede reintentar.
La imagen se ejecuta sin root; Compose limita Report a 512 MB y una CPU. La memoria
es acotada por filas/respuestas y límites del contenedor, pero WeasyPrint mantiene
el documento completo en memoria. No es un motor de exportación masiva por streaming.
Para grandes volúmenes deben acotarse filtros; no se truncan filas silenciosamente.

## Angular

Ruta lazy `/reports`, guard de operador, enlace en sidebar y acceso rápido discreto
en dashboard. Usa variables de tema, paneles, formularios, tablas, badges y paginación
existentes. Filtros se muestran según el tipo y se limpian al cambiarlo. Fechas de
formulario representan días completos en UTC. Hay loading, errores, vacío, éxito,
generación bloqueada durante solicitudes y controles de descarga por estado.

`ReportApiService` consume solo `API_BASE_URL + '/reports'`, conserva interceptores
y descarga como Blob, con nombre de `Content-Disposition` y fallback saneado. Los
modelos no utilizan `any`. Historial propio filtra tipo/estado y pagina 10/20/50.
Comunidades/sensores para selectores se cargan paginados, máximo 2,000 opciones con
aviso explícito si se alcanza el límite. No se instaló ninguna biblioteca PDF frontend.

## Operación, Docker y pruebas

Completar los secretos de `.env.example`, con claves JWT/issuer/audience coincidentes
en Auth, Gateway, monolito y Report; dos contraseñas Mongo diferentes. No publicar
Mongo ni Report. El init JS solo crea el usuario dentro de un volumen Mongo nuevo;
cambiar una contraseña en `.env` no rota credenciales dentro de un volumen existente.

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml up -d --build --wait
# Producción, detrás de TLS del despliegue y FRONTEND_ORIGIN correcto:
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.phase3.production.yml config --quiet
# Tests: Mongo y credenciales exclusivamente de pruebas, volumen separado:
docker compose -f deploy/compose/docker-compose.report-test.yml run --build --rm tests
docker compose -f deploy/compose/docker-compose.report-test.yml run --rm tests ruff check app tests
# Desarrollo local con recarga del código Python, sin publicar Report/Mongo:
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.report-development.yml up -d --build
```

El Compose original de Fase 2 no cambia. Fase 3 conserva su volumen SQL inicializado,
migraciones al arranque desactivadas y perfil de producción separado mediante overlay.
El stack de validación de este incremento usa copias/volúmenes distintos del original.

Para desarrollo fuera de Docker: Python 3.13, `pip install -r requirements.lock`,
`pip install --no-deps -e .`, `.env` basado en el ejemplo y
`uvicorn app.main:create_app --factory --port 8000`. WeasyPrint requiere Pango,
HarfBuzz y fuentes; para evitar diferencias del host se recomienda el Dockerfile.
Las instrucciones de instalación se contrastaron con la
[documentación oficial de WeasyPrint](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html).

Smoke E2E reproducible: definir `REPORT_E2E_URL` con la URL de Nginx/Gateway,
`REPORT_E2E_USERNAME`/`REPORT_E2E_PASSWORD` desde el entorno seguro y ejecutar
`python tools/validate-reports.py`. Guarda IDs/estados/tamaños y ocho PDF en
`tmp/report-evidence`. No imprime bearer ni credenciales. El rango inicial se acota
a los cinco minutos anteriores a la última lectura para lecturas/estadísticas/ejecutivo;
alertas y eventos se consultan sin rango por defecto. Usar `REPORT_E2E_FROM` para
fijar un rango común y respetar límites.
Usar cuentas/entornos de validación: este script crea metadata/PDF y registra login.

Liveness no depende de fuentes. Readiness verifica Mongo y Auth, sin obligar a que
todas las APIs de datos estén disponibles para descargar un documento ya generado.
Gateway incorpora readiness de Report. En K3s, conservar estas probes, configurar
Secrets/Services/PVC para Mongo y los destinos por DNS. Cada réplica acepta una
generación; el control no es una cola global. Workers/broker/leases y almacenamiento
administrado quedan para cuando se requiera procesamiento asíncrono o mayor escala.

## CÓMO DEFENDER REPORT SERVICE

- **Independencia:** generar documentos tiene sus propios recursos, estado y persistencia.
  Puede evolucionar sin añadir dependencias de PDF al backend de sensores ni al navegador.
- **Python/FastAPI:** simplifica validación HTTP y composición de clientes con I/O asíncrona.
  La tarea intensiva de PDF sale del event loop a un proceso que puede detenerse.
- **MongoDB:** guarda documentos de metadata con filtros distintos por tipo. GridFS permite
  mantener archivo y metadata en la persistencia del mismo servicio, sin carpetas compartidas.
- **Jinja2:** separa datos y presentación y escapa contenido. Una base visual sirve para los
  ocho reportes sin duplicar cabeceras y estilos.
- **WeasyPrint:** convierte HTML/CSS de impresión en PDF con tablas y paginación reales.
  Las dependencias de Linux están encapsuladas en Docker.
- **Backend y Angular:** el servidor aplica permisos y produce un documento consistente;
  Angular solicita y descarga el resultado, sin depender del rendimiento o fuentes del cliente.
- **Datos:** Report llama a APIs de los propietarios. Cambiar una URL mantiene el motor;
  no existe acceso a tablas ajenas ni unión entre bases de distintos servicios.
- **JWT y permisos:** primero comprueba la firma/issuer/audience/vencimiento, después la
  identidad vigente. Un token con rol viejo no recupera permisos retirados. Cada reporte
  pertenece a quien lo solicitó; ser administrador no concede descarga ajena.
- **Fuente caída:** la solicitud falla de forma controlada y queda Failed. No se presenta
  una tabla incompleta como un informe terminado. Un PDF ya guardado no depende de la fuente.
- **Reportes grandes:** lotes, límite por fuente, presupuesto de tiempo, tamaño de archivo y
  capacidad por proceso. Si se supera el límite, se pide acotar filtros; no se omiten filas.
- **Kubernetes:** contenedor sin estado local de negocio, configuración externa, DNS, probes
  separadas y persistencia propia. Escalar requiere dimensionar recursos y capacidad Mongo.
- **Próximas extracciones:** los clientes separados apuntarán a Community/Sensor/Alert reales;
  esos servicios seguirán siendo los únicos autorizados para consultar sus persistencias.

Evidencia, resultados y pendientes: [validacion-reportes.md](validacion-reportes.md).
