# Demostración de Santa Fe Zona 13

CommunityId: `8abadd5d-288f-4801-8a38-42ff09df119b`.

La comunidad se crea o reutiliza mediante la API. La reasignación modifica el
CommunityId del registro existente; conserva SensorId, código, origen Physical,
credenciales, recibos de idempotencia y lecturas. Las alertas históricas conservan
su comunidad original; las nuevas lecturas se evalúan en Santa Fe.

| Variable | SensorId | Código | Unidad |
| --- | --- | --- | --- |
| Temperatura | 4d2b0578-3af7-4cd8-a709-5ec5f2eb604f | ESP32-DHT11-TEMP-01 | °C |
| Humedad relativa | 7cf92f36-bca1-4559-b50b-2834a87baeab | ESP32-DHT11-HUM-01 | % |
| Superficie de lluvia | 478f70dd-cb4d-4b77-ad1a-72e71be02884 | ESP32-RAIN-01 | estado |
| Nivel de río o reservorio | 8418a2d8-c32b-4d87-a390-68ce0df2a500 | ESP32-WATER-LEVEL-01 | % |

## Administración web

En Sensores se elige la fuente explícita Physical/Físico o Simulated/Simulado.
Los permisos de Operator existentes se conservan para la administración de
sensores. Viewer permanece en consulta. Solo Administrator administra las
DeviceCredentials, desde Ver detalle → Dispositivo físico.

La clave aparece exclusivamente en la respuesta de creación y permanece en la
pantalla hasta pulsar «Ya guardé la clave» o abandonar el detalle. No se almacena
en el navegador ni se recupera mediante consultas. Para regenerar hay que revocar
la credencial activa primero. No se rotan las credenciales de los cuatro ESP32.

Editar permite cambiar comunidad, nombre, ubicación, código y metadatos
administrativos sin cambiar el identificador. La variable y el origen de un sensor
existente permanecen protegidos para conservar el significado del historial.

El historial detallado ofrece Reciente, 1 h, 6 h, 24 h y 7 días. Los periodos
largos muestran hasta 121 observaciones reales distribuidas en el periodo,
seleccionando la última lectura de cada intervalo. No se promedian estados de
lluvia ni se inventan datos. La consulta completa sigue disponible en Historial.
El valor cero se conserva como Seco; uno es Mojado leve y dos es Muy mojado.

## Umbrales operativos configurados para el prototipo académico

Estos umbrales no son estándares meteorológicos oficiales. Cada regla se vincula
a su sensor físico y a Santa Fe mediante el motor existente.

| Variable | Normal | Precaución | Alerta | Emergencia |
| --- | --- | --- | --- | --- |
| Temperatura | <30 °C | 30–32 °C | >32–34 °C | >34 °C |
| Humedad | <80 % | 80–90 % | >90–95 % | >95 % |
| Nivel | 0–30 % | >30–50 % | >50–75 % | >75 % |
| Lluvia | Seco | Mojado leve | Muy mojado | No se activa solo por lluvia |

El motor usa rangos inclusivos y lecturas decimal(18,6); los límites estrictos
se expresan con el siguiente valor representable: 32.000001, 34.000001,
90.000001, 95.000001, 30.000001, 50.000001 y 75.000001.
El estado Normal es la ausencia de una regla de peligro coincidente; el motor
cierra automáticamente la alerta al volver a Normal.

El motor actual evalúa una variable por lectura y no admite reglas compuestas.
No se configura una emergencia de lluvia aislada ni se presenta una regla
«Muy mojado + Nivel >75 %» como implementada. Nivel >75 % sí activa Emergencia.

## Calibración del nivel: cinco mediciones pendientes

`firmware/esp32-vigia-rural/WaterCalibration.h` contiene WATER_RAW_0,
WATER_RAW_25, WATER_RAW_50, WATER_RAW_75 y WATER_RAW_100 con -1 (sin medir).
Hasta completar cinco RAW válidos, estrictamente crecientes, se usa exactamente
la fórmula anterior: zona muerta RAW ≤100, RAW ×100/1630, limitado a 0–100 %.
No se cambia la URL de API, las claves ni los identificadores del firmware.

1. Mantener la electrónica seca y la alimentación a 3.3 V. Usar la misma sonda,
   agua y orientación para todos los puntos; no sumergir los componentes.
2. Dividir en cuatro la longitud sensible segura de la sonda. El porcentaje se
   refiere a inmersión relativa de esa longitud, no a volumen ni a altura de río.
3. En 0 %, dejar la superficie sensible seca y fuera del agua. Esperar estabilidad
   y anotar diez valores RAW del Serial: `Nivel: XX % (RAW: XXXX)`.
4. Repetir a 25 %, 50 %, 75 % y 100 % de la longitud sensible segura. En 100 %,
   cubrir únicamente la zona de detección, nunca la placa ni los conectores.
5. Enviar los diez RAW de cada posición. Usar valores estables representativos,
   comprobar que aumentan y que la sonda regresa al valor seco. No asignar RAW
   ficticios ni activar una tabla con valores repetidos o fuera de 0–4095.
6. Con las mediciones reales, completar las cinco constantes y compilar. La
   interpolación lineal por tramos se activa automáticamente solo si la tabla
   es válida. La carga al ESP32 se realiza por separado.

## Validación

Las pruebas incluyen creación Physical/Simulated, repetición de tipos en distintas
comunidades, reasignación conservando lecturas/credenciales/recibos, permisos,
clave de una sola visualización, lluvia cero en tarjetas/gráficas/detalle y
consultas de periodos completos en SQL Server real. Alert Service verifica
límites y eventos sobre PostgreSQL real. La calibración tiene una prueba C++ con
datos sintéticos exclusivos de pruebas, sin incorporarlos a la configuración.

El despliegue actualiza únicamente frontend, sensor-service y alert-service.
Alert Service añade mediante Flyway una columna opcional `unit` a las reglas;
las reglas existentes conservan sus unidades anteriores por defecto.
No se cambian OAuth, JWT, Ingress, TLS ni las bases de datos ajenas a ese servicio.
No se cargan firmware ni lecturas sintéticas a los cuatro sensores físicos.

Para la comprobación física final, observar las cuatro tarjetas e historial en
https://vigiarural.site seleccionando Santa Fe, y variar las sondas reales.
Las transiciones provocadas solo por software se documentan como pruebas
simuladas separadas; no certifican cambios físicos que no se hayan observado.
