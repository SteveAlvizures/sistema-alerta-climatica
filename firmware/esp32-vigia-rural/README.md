# Vigía Rural: cuatro sensores ESP32 y dashboard local

ESP32 DevKit / ESP-WROOM-32, DHT11, LM393 con placa resistiva de lluvia y sensor
analógico de nivel. Banco local de Fase 4 en `fase4-steve`, sin despliegue VPS/K3s.

## Ruta validada y banco de demostración

ESP32 → hotspot/WiFi 2.4 GHz → HTTPS → Nginx → Gateway → Sensor Service →
SQL Server → `/api/dashboard` → Angular.

URL actual de API y dashboard: **https://172.20.10.4:18444**.
Dashboard: **https://172.20.10.4:18444/**, comunidad **La Isla**.
API base sin `/api`; envío a `POST /api/device-readings`.

El usuario confirmó la primera temperatura física con HTTP 201. SQL tenía 37
lecturas de ese sensor al validar esta integración. Humedad, lluvia y nivel
tenían cero: su primera comprobación física queda pendiente de cargar el nuevo
firmware. No se inventó evidencia física para esos tres sensores.

Se crearon los tres sensores por `POST /api/sensors` y las credenciales por
`POST /api/device-credentials`, con administrador autenticado en Auth real.
Comunidad La Isla: `df92ab53-95b5-4b69-89b8-35a165ae21e5`.
Todos son Physical/Active. Una credencial autoriza exactamente un SensorId.

| Canal | Código | SensorId | CredentialId (no es una clave) |
|---|---|---|---|
| TEMPERATURE | ESP32-DHT11-TEMP-01 | 4d2b0578-3af7-4cd8-a709-5ec5f2eb604f | 79743250-3ff8-4a75-9e44-104236f4353d |
| HUMIDITY | ESP32-DHT11-HUM-01 | 7cf92f36-bca1-4559-b50b-2834a87baeab | 3992854E-0FD8-4399-846F-820DE41AA889 |
| RAIN | ESP32-RAIN-01 | 478f70dd-cb4d-4b77-ad1a-72e71be02884 | 91898343-BB21-4806-A500-2B599171C738 |
| WATER_LEVEL | ESP32-WATER-LEVEL-01 | 8418a2d8-c32b-4d87-a390-68ce0df2a500 | 479518C7-C65B-46B5-8792-F6667E2B4D19 |

Variables canónicas del modelo existente:

| Canal | MeasurementType | Type | Unidad |
|---|---|---|---|
| Temperatura | Temperature | Temperature | C |
| Humedad | RelativeHumidity | Humidity | % |
| Lluvia | RainfallLevel | Rainfall | estado |
| Nivel | RiverOrReservoirLevel | RiverLevel | % |

`Humidity` es el Type; `RelativeHumidity` es el MeasurementType admitido por la
API. No se añadieron aliases ni se cambiaron contratos para el dispositivo.

La ruta física conserva su base `climate_sensors_device_validation` en
`climate-alert-phase3-sensor-sqlserver-1`. El banco anterior de Fase 3 en 8083
conserva sus fuentes. Solo el Gateway de demostración enruta `sensors`,
`sensor-readings` y `community-sensors` al cluster Sensor existente, además de
las rutas device ya validadas. Un clon local de backend compone dashboard con
`DASHBOARD_SENSOR_URL=http://device-validation-sensor:8080`; comunidad y alertas
siguen consultándose a sus servicios locales. Su cluster monolith apunta a
`http://device-demo-backend:8080/`. Esto evita que el dashboard siga consultando
la base anterior mientras el ESP32 escribe en la base física.

El dashboard del banco muestra los cuatro sensores físicos de La Isla; no se
copió ni alteró el histórico de la base anterior. Los workers del Sensor aislado
conservan sus destinos de prueba: se persiste outbox, pero no se certificó aquí
la entrega de alertas/auditoría externas ni se cambiaron reglas de producción.

## Conexiones y precauciones

```text
ESP32 3V3 ---+--- VCC DHT11
             +--- VCC módulo LM393 lluvia
             +--- VCC sensor nivel
ESP32 GND ---+--- GND de todos los módulos
GPIO4  ---------- DATA DHT11
GPIO34 ---------- AO lluvia (DO libre)
GPIO35 ---------- S nivel
USB  ------------ PC / Serial 115200
```

Identificar GPIO por serigrafía, no por posición física. Confirmar polaridad
en cada módulo; desconectar USB antes de cablear. DHT11 sin pull-up integrado:
resistencia de 10 kΩ entre DATA y 3V3; terminal NC libre si tiene cuatro pines.
GPIO34/35 son entradas ADC1; no usar pull-up interno. No conectar 5 V a GPIO.
La atenuación ADC no protege frente a sobretensión. Mantener ESP32, conectores y
electrónica secos: mojar solo el área sensible, sin sumergir su electrónica.
Desconectar y secar las sondas resistivas al terminar la demostración.

## Calibración

ADC a 12 bits (0–4095), ADC_11db por pin. El RAW de lluvia mostrado es el promedio
entero de cinco conversiones consecutivas, sin delays añadidos.

| RAW lluvia promediado | Serial / dashboard | Value API |
|---|---|---|
| ≥3600 | Seco | 0 |
| 2600–3599 | Mojado leve | 1 |
| <2600 | Muy mojado | 2 |

Observado: seco ≈4095; una gota ≈3120–3220; más mojado ≈1950–2500.
Es un detector relativo de superficie, **no un pluviómetro**: no mide mm ni
intensidad de precipitación. Los umbrales son prácticos, no una escala lineal.
Recalibrar con diez RAW por condición, revisar dispersión y retorno al seco,
y ajustar RAIN_DRY_THRESHOLD/RAIN_LIGHT_THRESHOLD manteniendo el primero mayor.

Nivel: WATER_EMPTY=0, WATER_FULL=1630. Zona muerta: RAW ≤100 → 0 %. Por encima
se conserva `RAW*100/1630`, entero truncado y limitado a 0–100. No se remapea
101 a cero. Medido: mitad ≈1350–1377; máximo seguro ≈1617–1635. Es porcentaje
relativo de inmersión, **no centímetros absolutos ni volumen**. La mitad física
puede indicar 82–84 %. Recalibrar vacío y máximo seguro con la misma agua/montaje,
anotando diez valores estables por punto y comprobando un punto intermedio.

## Frecuencias, errores e idempotencia

- `SENSOR_SAMPLE_INTERVAL_MS = 2000`: DHT11 y diagnóstico cada dos segundos.
- `API_SEND_INTERVAL_MS = 2000`: objetivo de un ciclo por muestra, unos 30
  intentos/minuto por credencial, por debajo del límite servidor 60/minuto.
- `DASHBOARD_REFRESH_INTERVAL_MS = 1000`: datos cada segundo, sin recargar página.

Los cuatro POST son independientes y secuenciales. Cada medición nueva genera
un UUID v4 con RNG y WiFi activo. Value/UUID/SensorId se escriben en NVS antes del
primer intento; 201 o 200 elimina el pendiente. Si falla, se reutiliza exactamente
ese valor y UUID. Un sensor pausado por error 3xx/4xx o NVS no detiene a los demás.
408/429/5xx y errores de transporte conservan pendientes para reintentar.

NVS conserva un pendiente por sensor: namespaces vigia-temp, vigia-hum,
vigia-rain y vigia-level, clave pending. El formato anterior de temperatura se
mantiene para recuperar su pendiente al actualizar. No se guarda una cola
ilimitada: mientras un canal tiene pendiente no se acumulan todas sus muestras.
No borrar NVS ni rotar una credencial con escritura ambigua pendiente.
No se guardan Device Keys en estos registros de lectura NVS.

HTTP/TLS corre en core 0 y el loop de sensores en core 1. Conexión/lectura HTTP:
800 ms; handshake TLS: 1 s. Se reutiliza la conexión TLS validada. Presupuesto de
inicio de nuevos POST por ciclo: 1600 ms, con rotación de canal inicial para
evitar que un fallo deje siempre al último sin turno. Una operación en curso
puede superar ese presupuesto hasta su timeout; el diagnóstico continúa.
En mala red la cadencia baja, no se garantiza 1–3 s ni se generan ráfagas.

WiFi: timeout 15 s y reconexión controlada cada 30 s. DHT inválido omite solo su
canal; lluvia y nivel continúan. No se lee DHT11 más rápido de dos segundos.
La escritura NVS cada dos segundos es para esta demostración; revisar desgaste
y almacenamiento antes de operación prolongada.

Angular reutiliza sensors/alerts/readingEvolution del resumen del backend para
evitar una consulta latest por sensor cada segundo. Mantiene compatibilidad con
el resumen anterior. Solo hay un timer por página; destroy lo limpia y cancela
la consulta activa. No inicia otra mientras está pendiente; tras error puede
recuperarse en el siguiente ciclo. Conserva últimas lecturas y timestamps.

Solo el código Physical ESP32-RAIN-01 se muestra como estado. No afecta lluvias
en mm. Su gráfico numérico se omite para no presentar 0/1/2 como magnitud; nivel
permanece en %. Las reglas antiguas en mm/m no se aplican a estos dos sensores
relativos con unidades estado/%. Los demás sensores conservan su representación.

## Configuración y seguridad

Completar `secrets.h` local, ignorado por Git; ya contiene WiFi, CA, cuatro keys
y cuatro IDs de este banco. No sobrescribirlo con la plantilla. Las variables:

- WIFI_SSID, WIFI_PASSWORD, API_BASE_URL, API_ROOT_CA.
- TEMPERATURE_SENSOR_ID / DEVICE_KEY_TEMPERATURE.
- HUMIDITY_SENSOR_ID / DEVICE_KEY_HUMIDITY.
- RAIN_SENSOR_ID / DEVICE_KEY_RAIN.
- WATER_LEVEL_SENSOR_ID / DEVICE_KEY_WATER_LEVEL.

`secrets.example.h` tiene IDs/keys/WiFi/CA vacíos y una URL LAN de ejemplo.
Sin secrets.h o configuración válida sigue disponible el diagnóstico Serial.
No imprimir ni versionar contraseñas, Device Keys o claves privadas.

Servidor: hashes SHA-256 de las claves, credencial por sensor, JWT humano solo
para administración, sensor Physical/Active, PK de recibo compuesta y transacción.
Gateway exige HTTPS; únicamente confía al Nginx interno 10.231.44.10.
El cambio de IP WiFi no modifica esta allowlist. Nginx fija X-Forwarded-Proto con
su esquema. Puerto LAN publicado: 172.20.10.4:18444; Gateway/Sensor solo loopback.
La plantilla TLS existente no fue debilitada. CA, hostname y vigencia se validan
con WiFiClientSecure y hora SNTP. Sin hora no se envía; no existe modo inseguro.

CA y claves TLS están solo en tmp/esp32-tls ignorado; el firmware incluye
únicamente el certificado público de CA. Certificado de servidor para IP
172.20.10.4 válido hasta 4 de diciembre de 2026. Si cambia IP, reemitir SAN y
actualizar URL; si cambia CA, actualizar API_ROOT_CA. Firewall Público del equipo
ya estaba desactivado; no se cambió su estado ni se crearon reglas. El aislamiento
entre clientes del hotspot requiere la prueba física.

## Compilar, cargar y comprobar

Arduino IDE 2.x: instalar esp32 by Espressif Systems (probado 3.3.12), DHT sensor
library by Adafruit (1.4.7) y Adafruit Unified Sensor (1.1.15). Las librerías WiFi,
HTTPClient, TLS, Preferences y FreeRTOS vienen con el core. Índice oficial:
https://espressif.github.io/arduino-esp32/package_esp32_index.json

1. Conectar PC y ESP32 al mismo hotspot de 2.4 GHz; habilitar compatibilidad
   2.4 GHz del teléfono si es necesaria. Confirmar IP del PC 172.20.10.4.
2. Mantener activos device-validation-sensor, device-demo-backend,
   device-validation-gateway y device-esp32-nginx. La imagen del Nginx de demo
   es climate-device-demo-frontend, construida del frontend actual.
3. Abrir esp32-vigia-rural.ino; conservar secrets.h privado ya preparado.
   Elegir ESP32 Dev Module, COM correcto, Upload Speed 115200, Core Debug Level None.
   Pulsar Verificar y Subir. Si se queda en Connecting, mantener BOOT hasta iniciar
   escritura y soltar. No borrar toda la flash: así se preservan pendientes NVS.
4. Monitor Serie a 115200. Confirmar cuatro diagnósticos cada dos segundos,
   conexión WiFi/hora y Temperatura/Humedad/Lluvia/Nivel -> HTTP 201 o 200.
5. Abrir https://172.20.10.4:18444/ y seleccionar La Isla. Para validar
   TLS en el navegador, confiar explícitamente en el certificado público de la
   CA local tmp/esp32-tls/ca.pem en el almacén del usuario; nunca importar su key.
   No se instaló automáticamente esa CA en Windows ni se desactivó TLS.
6. Variar con cuidado humedad ambiente, gotas sobre la placa y altura de inmersión
   segura. Verificar estados y porcentaje; mantener ESP32/electrónica secos.
   Anotar hora y demora observada. No asumir latencia física por el benchmark.
7. Cortar/restaurar red: Serial continúa y pendientes reintentan sin duplicarse.
   Confirmar las lecturas en la base indicada, recibos únicos y LastCommunicationAt.
8. Desconectar USB y secar sondas al terminar; evita llenar la base cada dos segundos.

CLI (en este equipo puede requerir la ruta del ejecutable integrado en Arduino IDE):

```powershell
arduino-cli compile --jobs 1 --fqbn esp32:esp32:esp32 firmware/esp32-vigia-rural
docker start device-validation-sensor device-demo-backend device-validation-gateway device-esp32-nginx
docker exec device-esp32-nginx nginx -t
```

Para reconstruir frontend: docker build -t climate-device-demo-frontend
frontend/angular-app. Recrear solo el Nginx de demo con esa imagen, los dos
montajes TLS de solo lectura y la plantilla nginx.device-tls.example.conf;
conservar IP privada 10.231.44.10 y enlace 172.20.10.4:18444:443.
Configuración de ejecución del banco conservada localmente en tmp, sin secretos
versionados. No iniciar simultáneamente device-validation-nginx (proxy antiguo).

## Verificación del cierre

Firmware compilado para esp32:esp32:esp32: 1039548 bytes de flash (79 % de 1310720),
49224 bytes de variables globales (15 % de 327680), quedan 278456 bytes.
No se cargó automáticamente al hardware.

Sensor: 43 pruebas aprobadas. Gateway: 63 aprobadas y 2 omitidas por fixture Nest
externo dedicado; administración/credenciales usaron Auth Nest real local.
Angular: 295 aprobadas. TypeScript app/spec sin errores. Build Angular correcto
(aviso de tamaño SCSS ya existente en sensors-page). git diff --check correcto.
Auditoría: cuatro Device Keys y WiFi fuera de archivos versionables; secrets.h
ignorado; sin material PEM privado rastreado. Sin push ni despliegue externo.

Benchmark de software: cinco POST válidos en sensor técnico separado DEV-TEST-0,
con su credencial temporal revocada al terminar; cada repetición devolvió 200 y
mismo Id. No se insertaron medidas sintéticas en los cuatro sensores principales.
POST: 15.26–35.17 ms, mediana 18.05 ms.
Dashboard: 81.37–115.00 ms, mediana 96.91 ms.
Polling 1 s confirmado por pruebas de timer/limpieza/recuperación. El objetivo
visual 1–3 s depende de WiFi, cuatro POST y consultas; todavía no se midió la
latencia física del firmware de cuatro sensores. Evidencia local sin secretos:
tmp/fase4-sensors.json y tmp/fase4-latency.json.
