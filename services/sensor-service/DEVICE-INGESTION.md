# Ingesta de dispositivos (Fase 4, sin despliegue)

El endpoint manual `POST /api/sensor-readings` y su política `Operate` no cambian.
La ruta nueva siempre pertenece a Sensor Service; no cambia el destino de las rutas de Fase 3.
Antes de usarla, reconciliar los sensores físicos registrados en la base `climate_sensors`
y las fuentes de dashboard/Reportes. No cambiar los clusters antiguos automáticamente.

## Credenciales

Un administrador, con su JWT humano, solicita por HTTPS:

```http
POST /api/device-credentials
Authorization: Bearer <JWT de Administrator>
Content-Type: application/json

{"name":"ESP32 temperatura norte","sensorId":"<UUID de sensor Physical>"}
```

Respuesta 201: `id`, `name`, `sensorId`, `key`. La clave aleatoria de 256 bits se entrega
una sola vez, con `Cache-Control: no-store`. Guardarla de forma segura en el dispositivo;
no copiarla al repositorio, argumentos de shell, logs ni URLs. El servidor almacena
solo SHA-256, suficiente para una clave aleatoria de alta entropía (no una contraseña humana).
Una credencial autoriza exactamente un sensor; crear otra para otro sensor.

Revocación: `DELETE /api/device-credentials/<id>`, con JWT de administrador; devuelve 204
y establece `IsActive=false`, sin borrar recibos históricos. Para rotar, emitir una nueva,
actualizar el dispositivo y revocar la anterior. No existe recuperación de la clave original.
La revocación afecta nuevas peticiones; una transacción ya autorizada puede terminar antes
de que la revocación consiga su bloqueo.

## Envío

```http
POST /api/device-readings
X-Device-Key: <key>
Content-Type: application/json

{"sensorId":"<UUID>","value":24.5,"messageId":"<UUID nuevo por medición>"}
```

No se necesita JWT de usuario. Conservar `messageId` y el mismo contenido al reintentar
una medición, incluso después de reiniciar el ESP32. Primera recepción: 201; repetición
idéntica: 200 con la misma lectura; reutilización con otro valor: 409. La clave primaria
del recibo es `(DeviceCredentialId, MessageId)`; al rotar credenciales no reutilizar envíos
pendientes como mensajes nuevos. No hay purga automática de recibos.

Campos extra (incluidos origin, unit, variable, role, user y timestamps) devuelven 400.
El sensor debe existir (404), estar autorizado (403), activo y ser Physical (409).
La credencial ausente, incorrecta o revocada devuelve 401. JSON inválido/valor ausente,
messageId vacío o valor fuera de rango: 400; Content-Type no JSON: 415; cuerpo >1024 bytes:
413. Máximo 60 solicitudes/minuto por credencial por proceso: 429, sin cola.
El límite se aplica después de autenticar; no sustituye protección de abuso antes de Auth.

Rangos en unidades registradas: Temperature [-100,100], RelativeHumidity [0,100],
WindSpeed [0,500], RainfallLevel y RiverOrReservoirLevel [0,10000], SmokeConcentration
[0,1000000]. OtherEnvironmental acepta el rango técnico decimal; máximo 6 decimales,
valor absoluto <=999999999999. Revisar/calibrar límites y unidades con cada hardware;
no se convierten unidades. Estos límites solo afectan la vía de dispositivo.

## Persistencia y auditoría

Migración aditiva `DeviceIngestion`: tablas DeviceCredential y DeviceReadingReceipt,
FKs restrictivas e índice único del hash. Aplicada y revertida en SQL Server local de prueba; ver evidencia abajo.
Lectura, recibo, LastCommunicationAt, LastUsedAt y despachos se guardan en una transacción
serializable. Se conservan EvaluationDispatch/EvaluationWorker/Alert Service.
Los errores transitorios de concurrencia deben reintentarse con el mismo messageId.
El recibo y las FKs impiden duplicados confirmados. Se verificaron reintentos secuenciales en SQL Server; no se certificó carga concurrente de dispositivos.

Auditoría: `LecturaDispositivoCreada`, actor con UUID de DeviceCredential y username
`device:<UUID>`, nunca usuario humano. Solo la lectura nueva genera auditoría/evaluación.
El consumidor debe ser Audit Service, que admite identidades externas. No apuntar estos
eventos al adaptador antiguo de Auth del monolito, que proyecta identidades humanas.
El alta/revocación de credenciales no tiene aún un evento de auditoría administrativo dedicado.

## HTTPS y Gateway

Fuera de Development, Gateway exige `Request.IsHttps` en ambas rutas nuevas.
Por defecto no procesa headers de protocolo reenviados. Opcionalmente configurar
`Gateway__TrustedProxyAddresses__0=<IP exacta del Nginx conectado al Gateway>` (y entradas
numeradas adicionales si son necesarias). No usar subredes, IPs de clientes ni comodines.
Solo se acepta X-Forwarded-Proto desde esas IPs, con un salto m?ximo, exclusivamente en
las rutas nuevas; el resto de rutas conserva su comportamiento de Fase 3. La configuraci?n
inv?lida impide el arranque. Gateway sigue eliminando headers de forwarding hacia upstreams.

Preparaci?n sin activar: `deploy/nginx/nginx.device-tls.example.conf`. Nginx termina TLS
directamente y sobrescribe X-Forwarded-Proto con `$scheme`; no reutiliza el header recibido
ni una cadena de proxies. Rechaza HTTP. Montar certificados reales de solo lectura, fijar la
IP privada del proxy y restringir acceso directo a Gateway antes de activar esta alternativa.
No confiar en IPs din?micas que otros contenedores puedan ocupar. Si TLS termina en otro proxy
anterior, ese dise?o requiere otra revisi?n: no basta copiar esta allowlist.

Sensor Service tambi?n exige HTTPS por defecto. Compose permite HTTP **solo en el salto
privado Gateway -> Sensor** mediante `DeviceIngestion__RequireHttps=false`; no publicar ese
puerto ni desactivar el control p?blico. El Nginx actual de Fase 3 permanece intacto y borra
X-Forwarded-Proto: las rutas nuevas seguir?n bloqueadas en producci?n hasta activar una
terminaci?n TLS y confianza expl?cita revisadas. No se despleg? nada.
El ESP32 debe validar el certificado TLS; nunca usar una opción equivalente a insecure.

Gateway conserva X-Device-Key exclusivamente en /api/device-readings y lo elimina de
respuestas. Sigue eliminando headers internos/de identidad aportados por clientes.
No registrar cuerpos de emisión de claves ni el header X-Device-Key en infraestructura futura.

## Verificación

Las pruebas de Sensor ejecutan endpoints reales con JWT firmado y Auth simulado,
persistencia relacional SQLite y workers detenidos. Comprueban permisos manuales,
credenciales, validaciones, idempotencia, auditoría, persistencia y límites.
Las pruebas de Gateway comprueban ruta, headers y rechazo de HTTP con forwarding falso.
Las suites automatizadas se complementaron con las pruebas SQL Server y HTTP descritas abajo.
No certifican entrega efectiva de workers a Alert, hardware, TLS público ni coherencia
del dashboard/Reportes tras un cutover.

## Validación real del Bloque 3 — 3 de noviembre de 2026

Rama `fase4-steve`. Docker Desktop 4.47.0, cliente y servidor Engine 28.4.0:
`docker version`, `docker info` y `docker ps` correctos. El primer acceso desde el
sandbox fue denegado; el motor respondió al ejecutar con acceso autorizado.

Se identificó por etiquetas Compose y conexión del Sensor Service el contenedor
`climate-alert-phase3-sensor-sqlserver-1` (SQL Server 2022). Su base existente
`climate_sensors` tenía las tres migraciones anteriores. Se conservó intacta.
La base exclusiva de prueba `climate_sensors_device_validation`, en ese mismo
servidor local, contiene únicamente datos de esta validación. No se usaron los
otros contenedores SQL ni datos de otros proyectos.

Se ejecutó el SQL generado por EF: esquema inicial hasta
`20261103031206_DeviceIngestion`, rollback a `20261024035001_AuditDispatchTarget`
y reaplicación de DeviceIngestion. Historial final correcto. Snapshot sin cambios
pendientes. Rollback real elimina recibos y credenciales; por ello pierde esos
datos y debe respaldarse antes de usarlo fuera de una base desechable.

SQL confirmó:

- DeviceCredential: Id/SensorId uniqueidentifier, Name nvarchar(150), KeyHash
  nvarchar(64), IsActive bit, CreatedAt datetimeoffset, LastUsedAt datetimeoffset nullable.
- DeviceReadingReceipt: DeviceCredentialId/MessageId/ReadingId uniqueidentifier.
- PK de credencial y PK compuesta (DeviceCredentialId, MessageId); índice único
  IX_DeviceCredential_KeyHash; índices SensorId y ReadingId.
- FK a Sensors, DeviceCredential y Readings con NO_ACTION (Restrict).

Sensores Physical de prueba:

| Estado | GUID |
|---|---|
| Activo principal | 7dc07b19-6d35-4fbc-ac26-acf93fe0d747 |
| Inactivo | b3eaa648-0f88-49e1-b716-7e82dca1e64b |
| Segundo activo | 9ff65814-1e24-473a-bbc6-a893c318629f |

Se construyeron imágenes de los cambios actuales y contenedores independientes,
sin reemplazar los servicios anteriores. POST /api/device-credentials devolvió 201
usando login y resolución de identidad de Auth real. SQL comprobó SensorId,
IsActive y SHA-256 de la clave emitida; la clave en claro no se almacenó en tablas.

POST directo /api/device-readings devolvió 201. SQL comprobó lectura 24.5,
Origin Physical, variable/unidad del sensor, MeasuredAt=ReceivedAt del servidor,
LastCommunicationAt, recibo, EvaluationDispatch de Alert y auditoría
LecturaDispositivoCreada con actor DeviceCredential. Los destinos de los workers
se aislaron deliberadamente; esta prueba verifica outbox persistido, no entrega.

Idempotencia: mismo mensaje/valor devuelve 200 y mismo Id; otro valor devuelve 409.
Seguridad: clave ausente/inválida/revocada 401; otro sensor 403; inexistente 404;
inactivo 409; value null, JSON inválido y campo extra 400; cuerpo de 1025 bytes 413.
Las 18 comprobaciones HTTP directas pasaron. Al terminar: tres lecturas, tres
recibos, cero grupos duplicados; incluyen dos envíos nuevos por Gateway.

Gateway se ejecutó en Production con proxy permitido exacto 10.231.44.10 en red
aislada. Nginx usó la plantilla sin cambios y certificado efímero local validado
por el cliente mediante CA explícita. `nginx -t` correcto. TLS → Gateway → Sensor
→ SQL devolvió 201 y conservó correlación hasta auditoría. Un X-Forwarded-Proto
https desde cliente directo no permitido devolvió 400; HTTP por Nginx también 400.
El cliente TLS envió X-Forwarded-Proto http falso: Nginx lo sobrescribió con https.
La suite Gateway comprobó cuerpo JSON byte por byte, conservación de X-Device-Key,
eliminación de headers internos falsificados y correlación.

Regresiones:

- Sensor: 36 aprobadas.
- Gateway: 63 aprobadas, 2 omitidas (fixture Nest externo dedicado no configurado).
  La validación HTTP adicional sí utilizó Auth Nest real del entorno local.
- Angular: 285 aprobadas en ChromeHeadless.
- TypeScript: tsconfig.app.json y tsconfig.spec.json sin errores.
- git diff --check sin errores; solo avisos de normalización CRLF/LF.
- POST /api/sensor-readings idéntico a HEAD; permisos manuales verificados por suite.
- Rutas anteriores sensores, lecturas, comunidades, Reportes y dashboard: HTTP 200
  por Gateway actual. Dashboard tuvo un primer 503 transitorio; luego pasó directo
  y por Gateway sin cambios de código ni reinicios del backend.
- Reportes: smoke E2E local, 33 comprobaciones, generación y descarga de los ocho
  tipos de PDF con estado Completed, tamaño y Content-Type correctos.

Se revocaron todas las credenciales de prueba. Evidencia local sin claves ni JWT:
tmp/device-schema.txt, tmp/device-http-results.json, tmp/device-gateway-results.json
y tmp/device-test-state.json. Los scripts y archivos temporales no forman parte
del commit. Los contenedores de validación se detuvieron al cerrar; la base de
prueba se conserva para inspección.

Límites pendientes: carga concurrente SQL, entrega de workers a Alert/Audit,
dispositivos reales y certificados/TLS públicos. El 503 transitorio de dashboard
merece seguimiento si se repite. No se realizó cutover de fuentes, despliegue,
push, operación en VPS/K3s ni modificación de producción.
