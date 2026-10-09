# Reglas de alertas climáticas

## Niveles de peligro

- **Verde — Normal:** las condiciones se mantienen dentro del comportamiento esperado.
- **Amarillo — Precaución:** existen señales que requieren mayor vigilancia.
- **Naranja — Alerta:** el riesgo es relevante y requiere atención o preparación.
- **Rojo — Emergencia:** la situación requiere una respuesta prioritaria.

## Fenómenos y variables

- **Inundación:** nivel de lluvia, nivel de río o reservorio, humedad relativa y evolución de lecturas recientes.
- **Sequía:** nivel de lluvia, temperatura, humedad relativa y comportamiento acumulado del periodo evaluado.
- **Tormenta:** velocidad del viento, nivel de lluvia, temperatura y cambios rápidos entre lecturas.
- **Helada:** temperatura, humedad relativa, duración de la condición y ubicación de la comunidad.
- **Incendio forestal:** temperatura, humedad relativa, velocidad del viento y ausencia o bajo nivel de lluvia.

Estas variables describen criterios contemplados por el dominio. Las reglas creadas definen la variable, límites, nivel, comunidad, sensor opcional y periodo de vigencia.

## Configuración y evaluación

Los umbrales son configurables y pueden variar por comunidad y sensor. No se escriben directamente dentro de componentes o Controllers. La capa Application evalúa las reglas y coordina el resultado.

Una lectura manual no omite este proceso: se persiste como una nueva `SensorReading` y pasa por el mismo evaluador que una lectura simulada. Por ello puede abrir o actualizar una alerta mediante la estrategia existente, sin una lógica paralela de duplicados.

El flujo implementado es:

```text
Lectura recibida → validación → evaluación de reglas → determinación del nivel → generación o actualización de alerta → creación o actualización del evento asociado → notificación
```

No se crea un evento nuevo por cada lectura. Mientras continúe la misma situación, se actualiza el evento abierto; cuando comienza un incidente diferente, se crea un evento nuevo. La alerta mantiene el estado operativo del riesgo detectado.

Las lecturas con origen `Simulated` y `Physical` seguirán exactamente el mismo proceso de validación, evaluación y registro.

## Integridad por sensor y fenómeno

Las alertas abiertas se consultan mediante `SupportingReading.SensorId` y la variable de la lectura, sin añadir otra FK de sensor. Se prefiere la alerta de la misma regla; una escalada o reducción de nivel puede reutilizar una alerta únicamente del mismo sensor y fenómeno, con un evento compatible y abierto. Una lectura de otro sensor no modifica ni cierra esa alerta.

Al cambiar el fenómeno ganador se cierra la alerta anterior y se crea otra. La anterior conserva su lectura, regla, fenómeno y evento; no se reasigna al evento del nuevo fenómeno. Se mantiene la selección existente de una regla ganadora por lectura y la compatibilidad con reglas anteriores.

Un evento sigue agrupando alertas de una comunidad y un fenómeno, incluso de distintos sensores. Solo se cierra cuando todas sus alertas están cerradas, comprobando el evento realmente asociado y su colección completa de alertas.

La búsqueda de incidentes abiertos no depende de que su regla continúe activa o vigente. En la siguiente lectura del mismo sensor, si ya no hay una regla aplicable se cierra la alerta; si existe otra del mismo fenómeno puede continuar bajo esa regla. Desactivar una regla no modifica inmediatamente los incidentes ni equivale a una medición normal. Si el sensor no vuelve a emitir lecturas, permanece disponible el cierre manual mediante el endpoint existente `PATCH /api/alerts/{id}/resolve`.

El formulario exige seleccionar explícitamente Inundación, Sequía, Tormenta, Helada o Incendio forestal. No preselecciona `Wildfire`. El backend rechaza valores de fenómeno no definidos mediante un error de validación HTTP 400.

Esta corrección no migra ni reinterpreta datos históricos que ya estuvieran asociados incorrectamente.

## Notificación

El sistema muestra notificaciones visuales y estados de peligro. La emisión de avisos mediante canales externos o dispositivos físicos queda como mejora futura.

## Fase 2: rangos y compatibilidad

Las reglas nuevas y las editadas usan `UsesRange = true`: mínimo y máximo inclusivos, solo mínimo (lectura >= min) o solo máximo (lectura <= max). Ambos nulos o min > max se rechazan con 400. La API expone `minValue`/`maxValue`; las columnas `LowerLimit`/`UpperLimit` se conservan. Los campos anteriores se aceptan al crear por compatibilidad de DTO, con semántica de rango.

Orden: actividad y vigencia, comunidad/variable/sensor, rango válido para Fase 2, fallback operador/punto para reglas antiguas o sin rango válido, exclusión de Green y selección de mayor nivel (Red > Orange > Yellow > Green). Las reglas anteriores conservan `UsesRange = false`, aun cuando tengan límites: convertir un `>` en `>=` cambiaría su significado. Editarlas convierte explícitamente a rango inclusivo mostrado en el formulario.

`Name` identifica la regla; `Message` es el aviso, con texto predeterminado independiente si se omite. `PUT /api/alert-rules/{id}` conserva Id, código, comunidad, variable y sensor; permite editar nombre, rango, nivel, fenómeno, mensaje, vigencia y estado, y audita `ReglaEditada`.

La migración aditiva `AddPhaseTwoAlertRules` copia el antiguo texto Name a Message y fija el umbral mostrado en cada alerta existente en `ActivationPointSnapshot`. Mensaje, nivel y fenómeno ya se almacenan en la alerta. Editar una regla no modifica estas instantáneas ni alertas cerradas. Una nueva lectura puede actualizar un incidente abierto según el ciclo habitual. La migración fue generada, no aplicada a una base real.
