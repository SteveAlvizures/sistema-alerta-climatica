# Nivel conductivo final: tres estados

El promedio de ocho lecturas ADC se conserva, sin delays adicionales.
RAW <= 100: Seco (0); RAW 101–1900: Nivel bajo (1); RAW >1900: Nivel alto (2).
El RAW permanece interno y en Serial. No se interpreta profundidad ni porcentaje.

Frontend: valores, ejes, tooltips, detalle, historial, alertas y eventos usan texto.
La normalización de unidad en el navegador no altera severidades ni inventa reglas.
La severidad procede de la configuración real y las alertas/eventos de la API.

La regla real SF13-WATER-HIGH-FINAL del sensor físico existente de Santa Fe Zona 13
activa Alerta (Orange) desde valor 2, sin límite superior. Las reglas anteriores
quedan desactivadas; sus registros y el historial se conservan. Seco y Nivel bajo
no activan esta regla. SensorId y credencial permanecen intactos.

No se flashea automáticamente. Para comprobar el recorrido físico completo,
flashear manualmente el firmware compilado y llevar el sensor a RAW >1900.
Confirmar que la lectura 2 crea una alerta Orange con supportingReadingId y eventId,
y que el toast existente aparece una vez por alerta/evento, sin repetirse en cada poll.
No se insertan lecturas falsas en el sensor físico para simular esa comprobación.
