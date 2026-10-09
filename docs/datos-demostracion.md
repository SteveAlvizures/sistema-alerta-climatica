# Datos de demostración

El backend puede cargar un conjunto idempotente de datos académicos al iniciar con `DEMO_DATA_SEED_ENABLED=true`. Las localidades son reales, pero las lecturas son simuladas y no representan mediciones meteorológicas oficiales.

El conjunto oficial incluye:

- San Juan La Laguna, Sololá.
- Santa Catarina Palopó, Sololá.
- San Juan Chamelco, Alta Verapaz.
- Lanquín, Alta Verapaz.
- Livingston, Izabal.
- Todos Santos Cuchumatán, Huehuetenango.

Cada comunidad oficial tiene cinco sensores activos. El seed inicial incorpora doce lecturas históricas por sensor, aunque el simulador puede aumentar posteriormente ese conteo. También se crean reglas y escenarios académicos para demostrar alertas.

El administrador puede registrar una lectura manual sobre un sensor activo para demostrar umbrales. Esta operación agrega una medición al historial, participa en tendencias y Dashboard, evalúa las reglas normales y queda identificada en la Bitácora. No edita ni elimina lecturas anteriores.

## Comportamiento del seed

El inicio normal no elimina información. El seeder reconoce comunidades, códigos de sensores y reglas existentes, por lo que puede ejecutarse nuevamente sin duplicar el dataset oficial.

## Restablecimiento de una instalación exclusivamente demostrativa

Eliminar datos es una operación destructiva y no forma parte del inicio normal. Solo con autorización y tras confirmar que la base no contiene información que deba conservarse:

1. Detener servicios con `docker compose down`.
2. Eliminar manualmente el volumen `climate-alert-sqlserver-data`.
3. Crear `.env` a partir de `.env.example`.
4. Ejecutar `docker compose up -d --build`.

En una presentación o entorno compartido no debe eliminarse el volumen para “limpiar” la demostración. Las comunidades temporales de prueba deben gestionarse mediante el CRUD, respetando las restricciones de relaciones.
