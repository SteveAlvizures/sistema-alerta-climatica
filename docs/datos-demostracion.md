# Datos de demostración

El backend puede cargar un conjunto idempotente de datos académicos al iniciar con
`DEMO_DATA_SEED_ENABLED=true`. Las localidades son reales, pero todas las lecturas son
simuladas y no representan mediciones meteorológicas reales.

El conjunto incluye San Juan La Laguna, Santa Catarina Palopó, San Juan Chamelco,
Lanquín, Livingston y Todos Santos Cuchumatán. Cada localidad tiene cinco sensores
activos y doce lecturas históricas por sensor.

## Preparar una demostración limpia

El inicio normal nunca elimina información. Para restablecer de forma controlada una
instalación exclusivamente demostrativa:

1. Detener los servicios con `docker compose down`.
2. Eliminar manualmente el volumen `climate-alert-sqlserver-data` únicamente si se ha
   confirmado que no contiene información que deba conservarse.
3. Configurar un `.env` local a partir de `.env.example`.
4. Ejecutar `docker compose up --build`.

El seed también puede ejecutarse de nuevo sobre una base existente: reconoce
comunidades, códigos de sensores y reglas, por lo que no duplica el dataset.
