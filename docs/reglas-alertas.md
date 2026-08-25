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

El flujo implementado es:

```text
Lectura recibida → validación → evaluación de reglas → determinación del nivel → generación o actualización de alerta → creación o actualización del evento asociado → notificación
```

No se crea un evento nuevo por cada lectura. Mientras continúe la misma situación, se actualiza el evento abierto; cuando comienza un incidente diferente, se crea un evento nuevo. La alerta mantiene el estado operativo del riesgo detectado.

Las lecturas con origen `Simulated` y `Physical` seguirán exactamente el mismo proceso de validación, evaluación y registro.

## Notificación

El sistema muestra notificaciones visuales y estados de peligro. La emisión de avisos mediante canales externos o dispositivos físicos queda como mejora futura.
