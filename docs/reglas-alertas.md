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

Estas variables describen posibles criterios de evaluación. Los valores y combinaciones definitivos deberán acordarse con las personas responsables del monitoreo.

## Configuración y evaluación

Los umbrales serán configurables y podrán variar por comunidad y tipo de sensor. No deben escribirse directamente dentro de componentes o controladores. La capa de aplicación será responsable de evaluar las reglas y coordinar el resultado.

El flujo previsto es:

```text
Lectura recibida → validación → evaluación de reglas → determinación del nivel → generación o actualización de alerta → creación o actualización del evento asociado → notificación
```

No se crea un evento nuevo por cada lectura. Mientras continúe la misma situación, se actualiza el evento abierto; cuando comienza un incidente diferente, se crea un evento nuevo. La alerta mantiene el estado operativo del riesgo detectado.

Las lecturas con origen `Simulated` y `Physical` seguirán exactamente el mismo proceso de validación, evaluación y registro.

## Notificación

El sistema mostrará una notificación visual del nivel determinado. Para los niveles que se definan como relevantes, también podrá emitir un sonido, considerando la configuración y el contexto de uso de la comunidad.
