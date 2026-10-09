# Ciclo de vida de alertas — Fase 2, Bloque 5

Cubre RF-ADM-37 a RF-ADM-43. `/alerts` muestra listado paginado, estado, fecha/hora de detección, comunidad, sensor, fenómeno, nivel, valor detectado, condición histórica y mensaje. `/alerts/:id` añade responsables, fechas y trazabilidad a regla, lectura de soporte y evento.

## Estados y transiciones

| Estado interno y API | Presentación | Acción manual permitida |
|---|---|---|
| `Open` | Activa | Atender |
| `Acknowledged` | Atendida | Cerrar |
| `Closed` | Cerrada | Ninguna |

Las operaciones manuales siguen Activa → Atendida → Cerrada. El cierre directo de una activa, la atención repetida y cualquier operación sobre una cerrada devuelven `409`. Los nombres de estados en la base de datos y los endpoints anteriores se conservan.

El motor automático conserva su comportamiento: puede cerrar una activa o atendida cuando no hay una regla aplicable o cambia el fenómeno ganador. Ese cierre no inventa un usuario responsable. `ClosedById` nulo significa sistema automático o historial anterior, distinción mostrada en la UI. Una nueva lectura peligrosa después de un cierre puede generar otro incidente conforme al motor existente.

## Endpoints y filtros

| Método y ruta | Función | Acceso |
|---|---|---|
| `GET /api/alerts` | Listado y filtros paginados | Consulta pública actual |
| `GET /api/alerts/{id}` | Detalle completo | Consulta pública actual |
| `GET /api/communities/{id}/alerts` | Historial de una comunidad | Consulta pública actual |
| `PATCH /api/alerts/{id}/acknowledge` | Atender una activa | Administrator / Operator |
| `PATCH /api/alerts/{id}/resolve` | Cerrar una atendida | Administrator / Operator |
| `GET /api/dashboard` | Alertas activas de la comunidad | Consulta pública actual |

El listado acepta `dateFrom`, `dateTo`, `communityId`, `sensorId`, `phenomenon`, `level`, `status`, además del filtro `variable` existente. Las fechas se comparan con `DetectedAt`, incluidos ambos extremos; Angular convierte la fecha/hora local a ISO antes de enviarla. Una fecha inicial posterior a la final o un valor de enum no definido devuelve `400`.

Se conservan `page` y `pageSize` (10, 20 o 50; otros tamaños mantienen el fallback previo a 20). El orden es detección descendente, seguido de Id para paginación estable. Los conteos por nivel se calculan sobre todo el resultado filtrado antes de paginar. Modificar un filtro en el formulario no altera la página consultada hasta pulsar **Aplicar filtros**.

Todas las operaciones usan `OperateSystem`: sin JWT válido, `401`; con rol insuficiente, `403`. El responsable proviene del Id del JWT validado. No se aceptan responsable ni timestamps del body. `ConsultationUser` y visitantes pueden consultar, pero no ven ni pueden ejecutar botones operativos.

## Persistencia y compatibilidad

La migración `20261007033653_AddAlertLifecycleTracking` añade siete columnas nullable a `Alerts`:

- `AcknowledgedAt`, `AcknowledgedById`, `ClosedById`.
- `LowerLimitSnapshot`, `UpperLimitSnapshot`, `UsesRangeSnapshot`, `ComparisonOperatorSnapshot`.

`ClosedAt` ya existía. Las nuevas FKs apuntan a `Users` con eliminación restrictiva; se crean índices para ambos responsables. La migración `Up` solo añade campos, índices y FKs. Se generó sin ejecutarla contra una base real. El arranque habitual de la API aplica migraciones pendientes mediante `MigrateAsync`.

Los campos históricos nuevos permanecen nulos: no se deduce quién atendió/cerró ni se copia el rango actual de una regla que pudo cambiar. La UI muestra el `ActivationPointSnapshot` existente como umbral histórico. Las alertas nuevas o actualizadas por una lectura conservan la condición de la regla utilizada en ese momento. Editar una regla no altera esta copia.

La atención y el cierre manual registran `AlertaAtendida` y `AlertaCerrada`, con usuario, fecha, entidad `Alert`, Id y descripción. El cambio y la auditoría se guardan con un único `SaveChangesAsync`. Las etiquetas antiguas de auditoría se conservan para consultar registros previos.

`Status` y `UpdatedAt` son tokens de concurrencia EF sobre las columnas existentes. Una escritura que quedó obsoleta por otra acción o lectura se rechaza con `409`, sin sobrescribir el responsable de la otra operación. El motor y las relaciones Alert → Rule, SupportingReading → Sensor, Community y Event se mantienen.

## Dashboard

Solo `Open` cuenta como activa en el endpoint del dashboard, la composición Angular, la distribución por nivel y la notificación del header. Las atendidas/cerradas pueden aparecer en el historial reciente, pero no en el resumen activo. El header consulta todas las páginas de alertas activas, evitando truncar su conteo a las primeras 50.

Los indicadores de mediciones y reglas siguen mostrando el riesgo climático; ese nivel de medición es independiente de si un operador ya atendió el incidente. El resumen de alertas activas se basa exclusivamente en su estado operativo.

## Verificación y límites

Las pruebas backend ejercitan controllers reales mediante HTTP y EF InMemory: transiciones, responsables JWT, timestamps, auditoría, filtros, detalle, dashboard, seguridad 401/403 y conflicto de concurrencia. Las pruebas de migración verifican operaciones aditivas y coherencia del snapshot; no sustituyen una validación contra SQL Server real.

Angular prueba etiquetas oficiales, filtros, detalle, responsables/fechas, acciones por estado y rol, conflictos y conteo de varias páginas. La advertencia de presupuesto CSS de sensores es anterior a este bloque.

La migración aún debe aplicarse en el entorno de ejecución. Los registros históricos sin responsable o rango completo continúan mostrando esa ausencia explícitamente. Revertir la migración mediante `Down` eliminaría únicamente los campos nuevos y su información; no se ejecutó esa reversión.
