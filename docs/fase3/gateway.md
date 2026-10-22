# API Gateway: primer incremento de Fase 3

Resultados ejecutados y límites de la validación: [validacion-gateway.md](validacion-gateway.md).

## Propósito y recorrido

Antes: `Angular → proxy/Nginx → ClimateAlert.Api → SQL Server`.

Ahora: `Angular → proxy/Nginx → Gateway → ClimateAlert.Api → SQL Server`.

YARP es el reverse proxy de ASP.NET Core. El Gateway centraliza enrutamiento,
CORS, validación JWT preliminar, correlación y errores de transporte sin trasladar
negocio ni persistencia. El backend de Fase 2 se conserva.

## Rutas

Todas se envían al cluster `monolith`, sin retirar el prefijo `/api`, transformar
DTOs ni reescribir cuerpos de negocio:

| Ruta | Identificador YARP |
|---|---|
| `/api/auth/{**remainder}` | `auth` |
| `/api/users/{**remainder}` | `users` |
| `/api/communities/{id}/sensors/{**remainder}` | `community-sensors` |
| `/api/communities/{id}/alerts/{**remainder}` | `community-alerts` |
| `/api/communities/{**remainder}` | `communities` |
| `/api/sensors/{**remainder}` | `sensors` |
| `/api/sensor-readings/{**remainder}` | `sensor-readings` |
| `/api/alerts/{**remainder}` | `alerts` |
| `/api/alert-rules/{**remainder}` | `alert-rules` |
| `/api/events/{**remainder}` | `events` |
| `/api/audit-actions/{**remainder}` | `audit-actions` |
| `/api/dashboard` | `dashboard` |
| `/api/reports/{**remainder}` | `reports-reserved` |
| `/api/{**remainder}` | `api-fallback` |

El catch-all también admite la raíz sin segmentos adicionales. Las dos rutas
anidadas tienen `Order=-20`; el fallback tiene `Order=100`. Reports continúa
devolviendo 404 porque aún no existe el servicio. El fallback conserva el
comportamiento del backend para rutas API desconocidas.

## JWT y encabezados

Gateway valida firma HS256, issuer, audience y expiración con `ClockSkew=0`.
Utiliza exactamente `SHA256(UTF8(JWT_KEY))`, igual que el monolito. No genera
tokens, no consulta usuarios y no modifica el bearer que reenvía.

No hay política de autorización global: un token inválido no convierte una ruta
pública en protegida. La autenticación preliminar no es una barrera de acceso en
este incremento. Las rutas protegidas siguen rechazándose en el backend, que
consulta el estado y rol actual del usuario. Los roles y guards no cambian.

Se eliminan encabezados arbitrarios de identidad y forwarding antes de reenviar
la petición. El backend debe confiar en el JWT, no en esos encabezados. Gateway
no confía en `Forwarded`/`X-Forwarded-*` entrantes. La configuración actual no
necesita confiar en una subred fija para funcionar detrás de Nginx.

Se retiran encabezados de respuesta de infraestructura conocidos. Un `Location`
HTTP(S) absoluto del backend se convierte en relativo para evitar exponer su
autoridad; esto conserva el recurso de los `CreatedAtAction` actuales. Una futura
integración que requiera redirects externos necesitará una política explícita.

## CORS, correlación y logs

`Gateway:AllowedOrigins` es una lista explícita; en desarrollo permite
`http://localhost:4200`. No se habilitan cookies cross-origin ni wildcard. El
frontend Docker usa mismo origen. `X-Correlation-ID` se expone al navegador.
Se eliminan los headers CORS del upstream para que no puedan ampliar la política
del Gateway, incluso si el backend tiene CORS de desarrollo habilitado.

Se conserva un correlation ID entrante únicamente si es un único UUID válido;
se normaliza. En los demás casos se genera un UUID nuevo. Se envía al upstream,
se devuelve al cliente y aparece en cada log de petición. El backend no incorpora
todavía ese ID a sus propios scopes de logging: puede verlo como header.

Los logs de aplicación contienen ID, método, endpoint, status y duración. No se
registran cuerpos, query strings, contraseñas ni bearer tokens. Los proveedores
de logging de YARP/HTTP/JWT que podrían emitir destinos o detalles de excepciones
se silencian por defecto. No habilitar logging detallado en producción sin revisar
la información que registra.

## Errores y tiempos de espera

Los HTTP de negocio, incluidos 400, 401, 403, 404 y 409, se conservan. Los fallos de
transporte del proxy generan Problem Details con mensaje genérico y correlation
ID, sin URL interna, nombre de contenedor o stack trace. Conexión rechazada produce
502; falta de destino puede producir 503; inactividad del upstream produce 504.
YARP tiene un timeout de actividad de 30 segundos, configurable en el cluster.

Una respuesta que ya empezó a transmitirse no puede convertirse en Problem
Details: si el upstream falla a mitad de un cuerpo, se interrumpe la transferencia.

Angular conserva los status originales y normaliza el detalle de 401/403,
conectividad, 502/503 y 504. Cancela una petición API sin respuesta tras 40 segundos.
No reintenta escrituras ni elimina automáticamente la sesión ante un fallo.
Las pantallas conservan sus propios mensajes y manejo de errores.

## Health

| Endpoint | Significado | Upstream caído |
|---|---|---|
| `/health` | Liveness | 200 |
| `/health/live` | Liveness | 200 |
| `/health/ready` | Backend `/health`, timeout 3 s | 503 |

Las respuestas identifican únicamente `ClimateAlert.Gateway` y su estado. Readiness
comprueba indirectamente SQL porque el health existente del backend lo incluye.

## Desarrollo local

En PowerShell, desde la raíz, configurar los mismos valores de JWT que usa la API.
No guardar valores reales en archivos versionados:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:JWT_KEY = '<misma clave del backend, mínimo 24 caracteres>'
$env:JWT_ISSUER = 'climate-alert-api'
$env:JWT_AUDIENCE = 'climate-alert-client'
dotnet run --project gateway/src/ClimateAlert.Gateway
```

Gateway escucha en `http://localhost:5160` y el upstream predeterminado es
`http://localhost:5152/`. Cambiarlo mediante:

```powershell
$env:ReverseProxy__Clusters__monolith__Destinations__backend__Address = 'http://localhost:5152/'
```

Iniciar el backend con su configuración existente. Cuando se requiera impedir
migraciones de arranque, establecer `Database__ApplyMigrationsOnStartup=false` y
usar una base ya inicializada. Esta opción conserva `true` como valor predeterminado
para no alterar el arranque de Fase 2. No modifica los archivos de migraciones.

En otra terminal:

```powershell
cd frontend/angular-app
npm start
```

El proxy de Angular envía `/api` al Gateway. `API_BASE_URL` sigue siendo `/api`.
El interceptor solo añade el JWT al origen y al límite de ruta de la API configurada,
incluidas URLs absolutas del mismo origen; no lo adjunta a terceros ni a `/api-other`.

## Docker Compose de Fase 3

Los Compose y Nginx de Fase 2 no se reemplazan. Fase 3 monta sus propias
configuraciones Nginx desde `deploy/nginx`, apuntando a `gateway:8080`.

El volumen SQL es **externo y debe estar inicializado**. Por defecto reutiliza
`climate-alert-sqlserver-data`. Para una copia restaurada usar `PHASE3_SQL_VOLUME`.
No iniciar simultáneamente dos SQL Server sobre el mismo volumen. Detener primero
el stack de Fase 2 si se va a reutilizar su volumen. No usar `down -v`.

Este Compose no ejecuta migraciones ni demo seeding. Requiere una base Fase 2
existente/restaurada y sus usuarios. No crea una instalación vacía desde cero.

Desde la raíz, con `.env` local configurado:

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml up -d --build
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml ps
```

Frontend: `http://localhost:8082`. Solo publica `127.0.0.1:8082` de manera
predeterminada. Gateway, backend y SQL no publican puertos. Las redes internas
separan frontend/Gateway, Gateway/backend y backend/SQL. Los healthchecks controlan
el orden de arranque. La simulación conserva su intervalo configurable; establecer
`SIMULATED_READING_INTERVAL_SECONDS=0` para una validación sin lecturas nuevas.

Producción requiere TLS en el proxy del host y `FRONTEND_ORIGIN` con el origen HTTPS:

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.phase3.production.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.phase3.production.yml up -d --build
```

No publicar directamente Nginx sin el proxy TLS. Gateway y backend incorporan
`curl` en sus imágenes para los healthchecks. Se preservan los datos existentes.

## Pruebas

```powershell
dotnet restore gateway/ClimateAlert.Gateway.sln
dotnet build gateway/ClimateAlert.Gateway.sln -c Release --no-restore
dotnet test gateway/ClimateAlert.Gateway.sln -c Release --no-build
dotnet test backend/ClimateAlert.sln -c Release
cd frontend/angular-app
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

Las pruebas Gateway levantan Kestrel real para Gateway y backend y usan controllers,
JWT, hashing, políticas y repositorios de Fase 2. La persistencia es EF InMemory,
aislada por fixture: **no demuestra comportamiento de SQL Server ni ejecuta
migraciones**. Solo los probes de encabezados/latencia son endpoints sintéticos.

Cubren health, login, rutas públicas/protegidas, 400/401/403/404/409, CORS,
correlación, filtrado de headers, Location, precedencia YARP, caída y timeout.

Con el stack iniciado pueden ejecutarse llamadas reales desde el mismo origen:

```powershell
Invoke-RestMethod http://localhost:8082/health
Invoke-RestMethod http://localhost:8082/health/ready
Invoke-RestMethod http://localhost:8082/api/communities
# Leer credenciales sin escribirlas en comandos/logs compartidos.
$credentials = Get-Credential
$loginBody = @{ username = $credentials.UserName; password = $credentials.GetNetworkCredential().Password } | ConvertTo-Json
$session = Invoke-RestMethod http://localhost:8082/api/auth/login -Method Post -ContentType application/json -Body $loginBody
Invoke-RestMethod http://localhost:8082/api/users -Headers @{ Authorization = "Bearer $($session.accessToken)" }
```

Para comprobar 401, omitir Authorization en `/api/users`. Para 403 usar una cuenta
de consulta. La prueba automatizada de caída emplea un upstream aislado inaccesible,
sin detener servicios ni afectar usuarios.

## Futuros microservicios y pendientes

Crear un cluster con destino por nombre de servicio y cambiar solo el `ClusterId`
de las rutas correspondientes. Mantener prioridad de rutas anidadas y añadir
pruebas de contrato antes de cada cambio. No reutilizar `/health` para esconder
una caída del Gateway detrás de la del backend.

Quedan pendientes la extracción de los seis servicios, persistencias propias,
outbox/idempotencia, trazabilidad de correlación dentro del backend, despliegue K3s,
rotación de claves y OAuth/OIDC en Fase 4. Los fallos de negocio se siguen manejando
en el backend; Gateway no sanitiza arbitrariamente sus cuerpos JSON.

Referencias: [middleware y errores de YARP](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/middleware?view=aspnetcore-10.0),
[paquete YARP](https://www.nuget.org/packages/Yarp.ReverseProxy/2.3.0).
