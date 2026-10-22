# Auth Service: segundo incremento de Fase 3

## Responsabilidades y propiedad

```text
Angular /api → Nginx → Gateway .NET 10 / YARP
                         ├─ /api/auth/*  → Auth NestJS → PostgreSQL propio
                         ├─ /api/users/* → Auth NestJS → PostgreSQL propio
                         └─ resto       → monolito    → SQL Server existente

monolito ── HTTP autenticado ──> Auth /internal/identity/current
Auth outbox ── HTTP autenticado ──> monolito /internal/auth-events
```

Auth es una aplicación NestJS/TypeScript sobre Node 24. PostgreSQL almacena usuarios,
credenciales, roles, estado, fechas y eventos pendientes de auditoría. Las consultas
usan `pg` con parámetros; ningún cliente normal de Auth se conecta a SQL Server.
Gateway y monolito no reciben credenciales PostgreSQL.

Community, Sensor, Alert, Audit y Report no se extrajeron. Los controllers y clases
de autenticación de Fase 2 permanecen en el repositorio. Cuando se configura
`AUTH_SERVICE_URL`, el monolito deshabilita sus antiguas rutas públicas de auth/users
para impedir escrituras sobre una fuente de identidad obsoleta. Sin esa variable,
Fase 2 conserva el funcionamiento anterior.

## Login y contrato público

`POST /api/auth/login` recibe `{ username, password }` y devuelve:

```text
{ accessToken, expiresAt, name, email, role }
```

Primero busca un identificador/correo activo, normalizando espacios exteriores y
minúsculas. Si no existe, admite el nombre visible solo cuando identifica exactamente
una cuenta activa. Una contraseña incorrecta, cuenta inexistente o inactiva devuelve
401 con `{ message: "Usuario o contraseña incorrectos." }`, también para credenciales
vacías o compuestas por espacios. Campos requeridos ausentes/null producen 400.
Esta diferencia se verificó mediante comparación HTTP con el API anterior.

Se conserva el contrato que consume Angular: `API_BASE_URL='/api'`, interceptor y
guards no necesitan cambios. `expiresAt` refleja el instante de expiración del JWT
en segundos; las fechas públicas se serializan como ISO UTC.

## Compatibilidad PasswordHasher

El código de Fase 2 registra `PasswordHasher<User>` sin opciones personalizadas.
La herramienta de fixtures usa esa misma clase de .NET para generar hashes reales.

- Identity V2: marcador 0, salt de 16 bytes, PBKDF2-HMAC-SHA1, 1.000 iteraciones,
  subclave de 32 bytes.
- Identity V3: marcador 1, PRF, iteraciones y longitud de salt como UInt32 big endian,
  seguidos de salt y subclave. Se admiten PRF SHA1, SHA256 y SHA512.
- El formato V3 actual generado por la clase predeterminada utiliza SHA512 y 100.000
  iteraciones. Las contraseñas nuevas mantienen ese formato; no se introduce bcrypt.
- Verificación asíncrona con PBKDF2 y comparación de tiempo constante.
- Se rechazan base64 no canónico, cabeceras inválidas, tamaños incorrectos y más de
  2.000.000 de iteraciones. Un hash importado excepcional que exceda ese límite
  requiere revisión privada; no se intenta derivación sin límite de coste.

No se reescriben hashes importados tras login. No existe migración de algoritmo
ni reseteo obligatorio de contraseña. Contraseñas, hashes y tokens no se registran.

Referencia del formato: [PasswordHasher de ASP.NET Core](https://source.dot.net/Microsoft.Extensions.Identity.Core/PasswordHasher.cs.html).

## JWT interoperable

La clave efectiva sigue siendo `SHA256(UTF8(JWT_KEY))`, no los bytes directos del
texto. Se conserva HS256, issuer, audience, expiración configurable y tolerancia cero.
Los claims incluyen `sub`, `email`, nombre y rol con las URI de claims de .NET,
`jti`, `nbf`, `exp` e `iat` estándar. Auth no acepta algoritmos alternativos, tokens
sin expiración, firma alterada, issuer/audience distintos o sujetos no válidos.

Gateway hace validación preliminar y mantiene rutas públicas. Auth vuelve a validar
JWT y carga el usuario vigente desde PostgreSQL. En rutas del monolito, .NET valida
la firma y solicita la identidad vigente a Auth, sin consultar PostgreSQL directamente.

## Roles, permisos y estado dinámico

| Rol | Políticas vigentes |
|---|---|
| Administrator | AuthenticatedUser, ConsultEvents, OperateSystem, AdministratorOnly |
| Operator | AuthenticatedUser, ConsultEvents, OperateSystem |
| ConsultationUser | AuthenticatedUser, ConsultEvents |

`User` persistido se normaliza a `ConsultationUser`. No puede asignarse como rol
nuevo. Los permisos reflejan las políticas existentes del monolito.

Un token no congela sus permisos: cada solicitud protegida consulta el estado actual.
Desactivar una cuenta invalida su acceso con JWT anteriores; cambiar el rol modifica
sus permisos inmediatamente en Auth y en el monolito. No hay caché ni fallback a los
roles almacenados en SQL Server cuando se habilita el modo externo.

401 significa identidad ausente, token inválido, cuenta inactiva o imposibilidad de
verificar identidad en el monolito. 403 significa identidad válida sin el permiso.
La dependencia de Auth es síncrona: si está caído, el monolito falla de forma cerrada
en solicitudes protegidas; sus consultas públicas siguen disponibles.

## Endpoints públicos

| Método | Ruta | Acceso |
|---|---|---|
| POST | `/api/auth/login` | Público |
| POST | `/api/auth/logout` | Usuario autenticado; 204 |
| GET | `/api/users` | Administrator |
| GET | `/api/users/{id}` | Administrator |
| POST | `/api/users` | Administrator; 201 y Location relativo |
| PUT | `/api/users/{id}` | Administrator |
| PATCH | `/api/users/{id}/status` | Administrator |
| PATCH | `/api/users/{id}/role` | Administrator |

El listado conserva `page`, `pageSize`, `search`, `role`, `isActive` y la respuesta
`data/pageIndex/pageSize/totalPages/totalCount/hasPrevious/hasNext`. No expone hashes.
Se conserva la validación de nombre, login, contraseña de creación y roles, conflictos
por login duplicado, protección contra autodesactivación/autodegradación y conservación
de un administrador activo. El último caso se serializa mediante lock transaccional.

No se añadió DELETE ni cambio de contraseña, porque no existían en ese contrato.
Se conservan 200/201/204/400/401/403/404/409. No se inventa 422 donde el API anterior
usaba 400. Los errores inesperados se sanitizan como 500; fallos de conexión conocidos
se informan como 503 sin detalles internos.

## APIs internas y auditoría transitoria

- Auth `GET /internal/identity/current`: requiere bearer válido **y**
  `X-Internal-Service-Key`. Devuelve ID, perfil, rol, estado y políticas vigentes.
- Monolito `POST /internal/auth-events`: requiere la misma credencial de servicio.
  Recibe un evento durable con ID, actor, cuenta afectada, acción, descripción y fecha.
  El ID de auditoría se reutiliza como clave de idempotencia, sin añadir tablas SQL.

Estas rutas no se publican por Gateway. Sus transforms eliminan encabezados de
identidad, servicio y permisos aportados por clientes. Las APIs públicas de Auth
siempre exigen JWT donde corresponde; una clave interna no sustituye ese JWT.

SQL Server conserva usuarios originales y una proyección mínima para las claves
foráneas de auditoría y responsables de alertas. Para cuentas nuevas se crea un
registro sin contraseña local utilizable; no se replican hashes desde PostgreSQL.
La proyección no es fuente de autenticación. Los cambios autorizados y su evento
outbox se confirman en una transacción PostgreSQL.

Un worker entrega eventos ordenados con reintentos y lock por lote. El monolito
guarda proyección y auditoría juntos mediante EF. Si la respuesta se pierde, el
reintento no duplica la acción. No se extrae Audit Service ni se hace escritura
directa de Auth sobre SQL Server.

Logout conserva el comportamiento anterior: registra una intención durable de
auditoría y devuelve 204; Angular elimina la sesión. **No revoca el access token**.
No se crea blacklist. Las entidades refresh token de Fase 2 no tenían flujo HTTP
activo; no se inventa uno ni se crean sesiones PostgreSQL innecesarias.

La auditoría SQL pasa a ser eventual. Una caída del monolito no pierde el logout;
el evento queda pendiente. La indisponibilidad de PostgreSQL sí impide confirmar
una operación. Se necesita monitorización de retraso de outbox antes de producción.

## Importación segura de usuarios

1. Preparar un respaldo/restauración o dataset aislado. Para el corte real, detener
   escrituras de identidad antiguas y producir una exportación consistente.
2. Compilar `tools/ClimateAlert.AuthMigration`. El modo `export` usa EF en solo lectura,
   sin migraciones ni seeders, y escribe JSON privado. No muestra filas ni hashes.
3. Iniciar PostgreSQL y Auth para crear su esquema propio.
4. Ejecutar el importador. Usa una transacción y conserva IDs, nombre, email/login,
   hash original, rol, estado y fechas. `imported_source` retiene los valores originales
   para conciliación, incluida la precisión temporal del documento fuente.
5. Repetir el mismo archivo: informa registros sin cambios y no pisa cuentas que ya
   se hayan administrado desde Auth. Un ID con fuente diferente o login duplicado
   aborta la transacción completa; no se realiza un merge silencioso.
6. Conciliar conteos/IDs y probar credenciales existentes antes de habilitar el tráfico.

PostgreSQL tiene precisión temporal de microsegundos y la API Node representa fechas
en milisegundos. El documento fuente se conserva sin esa conversión. No se borran
usuarios ni tablas del origen. Archivos de exportación contienen hashes y deben
mantenerse fuera de Git y con permisos privados; en Linux la herramienta fija 0600.
En Windows usar un directorio con ACL restringida al operador.

```powershell
dotnet restore tools/ClimateAlert.AuthMigration/ClimateAlert.AuthMigration.csproj
dotnet build tools/ClimateAlert.AuthMigration/ClimateAlert.AuthMigration.csproj -c Release -m:1
# SQLSERVER_CONNECTION_STRING debe apuntar a la copia/autorizado, nunca imprimirse.
dotnet tools/ClimateAlert.AuthMigration/bin/Release/net10.0/ClimateAlert.AuthMigration.dll export tmp/auth-users.json
```

En local, con variables PostgreSQL/JWT configuradas:

```powershell
cd services/auth-service
npm ci
npm run build
npm run import:users -- ../../tmp/auth-users.json
npm start
```

Auth escucha en 3000; Gateway en desarrollo envía allí auth/users y conserva el
monolito en 5152. Configurar en el monolito `AUTH_SERVICE_URL=http://localhost:3000/`
y la misma `AUTH_INTERNAL_KEY`. Auth usa `AUTH_AUDIT_URL` para la API interna de
auditoría. Ninguna de esas claves se configura en Angular.

## Docker y producción

Los Compose de Fase 3 incorporan `auth-service` y `auth-postgres`. El segundo tiene
volumen propio y red `auth-persistence` accesible solo desde Auth. No se publican
sus puertos ni los de Auth. Gateway accede a `auth-service:3000`. El monolito depende
de Auth saludable; la outbox permite iniciar Auth sin esperar al monolito.

Completar `.env` con los placeholders documentados en `.env.example`, especialmente
`AUTH_DATABASE_PASSWORD` y `AUTH_INTERNAL_KEY` (mínimo 32 caracteres). No usar los
placeholders en producción. Gateway, backend y Auth comparten JWT_KEY/issuer/audience.

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml up -d --build auth-postgres auth-service
# Importar por stdin evita copiar un archivo secreto al filesystem del contenedor:
# desde un shell que preserve bytes UTF-8, transmitir el JSON exportado a:
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml exec -T auth-service node dist/src/persistence/import-users.js -
# El comando anterior espera el JSON en stdin; no pegar su contenido en logs/chat.
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml up -d --build --wait
```

Para automatizar stdin en Windows, usar una API de procesos que conserve UTF-8,
como `subprocess.run(..., input=archivo, encoding='utf8')`, o importar localmente
contra PostgreSQL accesible solo en loopback durante una ventana controlada.

Producción añade el overlay existente y exige `FRONTEND_ORIGIN` HTTPS; TLS termina
en el proxy del host. Los Compose de Fase 2 no cambian. No iniciar dos SQL Server
sobre el mismo volumen. `PHASE3_SQL_VOLUME` permite usar una copia restaurada.
`AUTH_POSTGRES_VOLUME` permite aislar el volumen Auth de validación.

## Health, logs y pruebas

Auth `/health` y `/health/live` comprueban proceso; `/health/ready` comprueba PostgreSQL.
Gateway readiness comprueba tanto Auth como monolito. Liveness no depende de ellos.
Son endpoints utilizables para futuras probes Kubernetes; no se agregan IPs fijas.

Auth registra solo correlation ID, método, status y duración. No registra URL/query,
cuerpo, contraseña, hash, bearer, cadena de conexión ni errores internos. El filtro
global evita stack traces incluso en desarrollo.

Generar fixtures .NET antes de las pruebas Node:

```powershell
dotnet tools/ClimateAlert.AuthMigration/bin/Release/net10.0/ClimateAlert.AuthMigration.dll fixtures services/auth-service/test/fixtures/generated.json
cd services/auth-service
npm ci
npm run build
npm run test:unit
```

Integración requiere `AUTH_INTEGRATION_TESTS=true`, base cuyo nombre termine en
`_test`, variables PostgreSQL y `AUTH_TEST_CONTAINER=climate-alert-auth-tests-postgres-1`.
El Compose `docker-compose.auth-test.yml` crea esa instancia con puerto de loopback
55432 y volumen separado. Las pruebas limpian **solo esa base de pruebas**, verifican
importación/rollback/unicidad y reinician únicamente ese contenedor.

Las dos pruebas .NET adicionales requieren `AUTH_INTEROP_URL` de una instancia Nest
de pruebas y `AUTH_INTEROP_FIXTURE` apuntando al fixture generado. Verifican que
Gateway realmente autentica un JWT emitido por Nest y que Nest acepta uno emitido
por .NET. Sin esas variables se omiten explícitamente; las 47 pruebas anteriores
siguen ejecutándose con sus fixtures originales.

No hay script lint; `npm run build` ejecuta comprobación TypeScript estricta. Las
pruebas Node se compilan con el mismo tsconfig. Los resultados ejecutados del
incremento se registran en `validacion-auth-service.md`.

## Extensión de Fase 4 y límites

`CredentialVerifier` es el punto de extensión para verificación local; emisión JWT,
usuarios y estado están separados. Un futuro proveedor externo podrá resolver una
identidad local y reutilizar esa emisión y políticas. No hay OAuth/OIDC implementado.

Este incremento no incluye rotación de claves, mTLS, gestión de secretos externa,
blacklist ni refresh HTTP. La clave interna compartida protege servicios de confianza;
en producción debe rotarse y transmitirse sobre una red/TLS controlada. La primera
importación y el cambio de Gateway requieren una ventana coordinada. Revertir a SQL
después de crear usuarios nuevos en PostgreSQL exige conciliación; no basta cambiar
la ruta. Monitorización de outbox y operación de backups siguen siendo necesarias.
