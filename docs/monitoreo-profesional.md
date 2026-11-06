# Experiencia final de monitoreo

Los cuatro sensores de Santa Fe mantienen sus identificadores, códigos internos,
origen Physical, credenciales e historial. En las vistas principales se presentan
como Temperatura, Humedad relativa, Superficie de lluvia y Nivel de agua. Su
ubicación visible es «Estación Santa Fe Zona 13», sin atribuir una dirección física.

El detalle separa Monitoreo, Información general, Información técnica y
Administración del dispositivo. La sección técnica es desplegable. La
administración de credenciales se muestra únicamente a administradores y conserva
la autorización del servicio. La clave recién generada permanece exclusivamente
en memoria hasta cerrarla o abandonar la vista, sin recuperar claves anteriores.

## Actualización y notificaciones

El detalle usa un solo temporizador de dos segundos y una solicitud compuesta
activa como máximo. Cancela solicitudes y temporizador al destruirse. La comunidad
y las reglas se cargan al entrar; se refrescan lecturas, alertas y eventos sin
reemplazar la página ni perder el rango de gráfica elegido. Los errores conservan
el último dato y permiten reintentar en el ciclo siguiente. Una comunicación con
más de dos minutos se marca como no reciente, sin desactivar administrativamente
el sensor. La interfaz muestra la hora exacta de la última lectura.

Los avisos reciben los resultados del polling existente. No agregan otra consulta
periódica. Mantienen durante la sesión los AlertId y el máximo nivel notificado;
un nuevo incidente o aumento de severidad produce un aviso. Una lectura repetida
no vuelve a notificar. Los avisos son descartables, no bloquean la navegación y
anuncian su severidad con texto y atributos accesibles.

La lluvia categórica conserva cero como Seco, uno como Mojado leve y dos como Muy
mojado. Este formato se comparte entre tarjetas, detalle, historial, alertas,
eventos, ejes, tooltips y reportes PDF nuevos. Los reportes de resumen cuentan
observaciones por estado; no presentan un promedio numérico de estados de lluvia.
Los archivos PDF históricos existentes no se modifican.

Las condiciones muestran símbolos ≥, ≤ o >. Un mínimo almacenado como 34.000001
se presenta «> 34», conservando la comparación estricta; no se modifica el motor,
los rangos ni las seis posiciones decimales de los datos. Los títulos de eventos
ESP32 se presentan según la variable y severidad de su evidencia/regla; la
clasificación histórica del fenómeno permanece intacta.

## Captura física de los cinco RAW

La calibración definitiva sigue pendiente. WATER_RAW_0, WATER_RAW_25,
WATER_RAW_50, WATER_RAW_75 y WATER_RAW_100 permanecen en -1. Las observaciones
anteriores (seco 0–40, mitad aproximadamente 1350, máximo aproximadamente 1630)
no completan la tabla y no se usan como cinco mediciones nuevas.

El firmware incorpora un promedio de ocho conversiones ADC, sin retardos largos.
Conserva la fórmula anterior hasta que las cinco constantes tengan valores
reales, crecientes y dentro de 0–4095. Una tabla válida activa la interpolación
lineal por tramos y limita el resultado a 0–100 %.

1. Cargar el firmware únicamente cuando el usuario lo decida. Abrir Serial a
   **115200 baudios** y elegir terminación **Nueva línea**.
2. Mantener `WATER_CALIBRATION_MODE = true` (valor preparado). La captura es
   opcional, no bloquea el muestreo ni muestra secretos.
3. Usar la misma sonda, agua y orientación. Dividir la longitud sensible segura
   en cuatro partes iguales. Mantener placa, conectores y ESP32 fuera del agua.
4. **0 %:** sonda sensible seca y fuera del agua. Esperar estabilidad. Escribir
   `0` y Enter. Mantener la posición unos **20 segundos**, hasta recibir:
   `Muestras: 10`, `RAW promedio`, `RAW min` y `RAW max`.
5. **25 %:** sumergir un cuarto de la longitud sensible; esperar estabilidad,
   escribir `25` y Enter y mantener hasta terminar las diez muestras.
6. **50 %:** repetir a la mitad, enviando `50` y Enter.
7. **75 %:** repetir a tres cuartos, enviando `75` y Enter.
8. **100 %:** cubrir solo el máximo seguro de la zona sensible, enviando `100`
   y Enter. No sumergir componentes electrónicos.
9. Enviar para cada posición promedio, mínimo y máximo. Cada muestra registrada
   ya es un promedio de ocho ADC. Si hay dispersión grande o no crecen los RAW,
   repetir la posición y comprobar cableado/orientación, sin inventar valores.
10. Volver a seco y repetir `0` para comprobar retorno. Con los cinco grupos de
    mediciones se completará la tabla y se verificará 0/25/50/75/100 % antes de
    declarar calibración final. Después puede desactivarse el modo Serial con
    `WATER_CALIBRATION_MODE = false`.

Los umbrales académicos de nivel permanecen: Normal ≤30 %, Precaución >30–50 %,
Alerta >50–75 % y Emergencia >75 %. El porcentaje representa inmersión relativa
de la sonda, no una altura de río ni volumen real.
