# Endpoints de la API

Base local: `http://localhost:8080`. Las respuestas usan JSON. **Público** permite visitantes sin JWT y usuarios autenticados; las escrituras operativas requieren `Administrator` u `Operator`. La bitácora y todos los endpoints de usuarios requieren `Administrator`. Un endpoint protegido responde `401` si falta una identidad válida y activa y `403` si su rol actual es insuficiente.

## Autenticación

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `POST /api/auth/login` | Validar credenciales y emitir una sesión JWT. | Público | `200`, `400`, `401` |

El login mantiene las cuentas existentes y emite los roles oficiales; el rol antiguo `User` se interpreta como `ConsultationUser`. Las cuentas iniciales solo se crean en una instalación vacía usando credenciales configuradas externamente.

## Comunidades

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/communities` | Listar comunidades. | Público | `200` |
| `GET /api/communities/{id}` | Consultar una comunidad. | Público | `200`, `404` |
| `POST /api/communities` | Crear una comunidad. | Administrator / Operator | `201`, `400`, `401`, `403`, `409` |
| `PUT /api/communities/{id}` | Actualizar los datos administrativos completos. | Administrator / Operator | `200`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/communities/{id}/status` | Activar/desactivar conservando el historial. | Administrator / Operator | `200`, `401`, `403`, `404` |
| `DELETE /api/communities/{id}` | Eliminar una comunidad sin dependencias. | Administrator / Operator | `204`, `401`, `403`, `404`, `409` |

## Sensores

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/sensors` | Listar sensores. | Público | `200` |
| `GET /api/sensors/{id}` | Consultar un sensor. | Público | `200`, `404` |
| `GET /api/communities/{id}/sensors` | Listar sensores de una comunidad. | Público | `200`, `404` |
| `POST /api/sensors` | Registrar un sensor. | Administrator / Operator | `201`, `400`, `401`, `403`, `404`, `409` |
| `PUT /api/sensors/{id}` | Editar los campos permitidos. | Administrator / Operator | `200`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/sensors/{id}/status` | Activar o desactivar un sensor. | Administrator / Operator | `200`, `401`, `403`, `404` |

Comunidades y sensores admiten filtros y paginación en sus GET de listado. Sin parámetros conservan la respuesta histórica como arreglo; con parámetros devuelven `PagedResponse`. Ambos listados y detalles incluyen los datos administrativos. Consulta [Gestión de comunidades y sensores](comunidades-sensores.md) para contratos, filtros, tipos, compatibilidad y migración.

Sensores mantienen el POST mínimo anterior y el PUT de ubicación como alternativas de compatibilidad. Los contratos completos añaden nombre, código, tipo, unidad, instalación y descripción. Nombre y código se generan si se omiten. Las nuevas comunidades requieren datos administrativos y coordenadas válidas.

## Lecturas

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/sensors/{id}/readings?page=1&pageSize=20` | Obtener historial paginado. | Público | `200`, `400`, `404` |
| `GET /api/sensors/{id}/readings/latest` | Obtener la lectura más reciente. | Público | `200`, `404` |
| `POST /api/sensor-readings` | Registrar y evaluar una lectura manual con `sensorId` y `value`. | Administrator / Operator | `201`, `400`, `401`, `403`, `404`, `409` |

La API obtiene del sensor su variable, unidad y origen. Un `409` indica, entre otros conflictos de estado, que el sensor está inactivo. La medición se agrega al historial y se audita; no reemplaza lecturas anteriores.

`pageSize` admite de 1 a 100. El parámetro heredado `limit` se acepta como alternativa de compatibilidad cuando no se proporciona `pageSize`.

## Reglas de alerta

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/alert-rules` | Listar reglas. | Público | `200` |
| `GET /api/alert-rules/{id}` | Consultar una regla. | Público | `200`, `404` |
| `POST /api/alert-rules` | Crear una regla. | Administrator / Operator | `201`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/alert-rules/{id}/status` | Cambiar el estado activo. | Administrator / Operator | `200`, `401`, `403`, `404` |

## Alertas

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/alerts` | Listar y filtrar alertas con paginación. | Público | `200`, `400` |
| `GET /api/alerts/{id}` | Detalle, responsables y trazabilidad de una alerta. | Público | `200`, `404` |
| `GET /api/communities/{id}/alerts` | Listar alertas de una comunidad. | Público | `200`, `404` |
| `PATCH /api/alerts/{id}/acknowledge` | Atender una alerta Activa. | Administrator / Operator | `200`, `401`, `403`, `404`, `409` |
| `PATCH /api/alerts/{id}/resolve` | Cerrar una alerta Atendida. | Administrator / Operator | `200`, `401`, `403`, `404`, `409` |

Filtros: `dateFrom`, `dateTo` sobre fecha de detección, `communityId`, `sensorId`, `phenomenon`, `level`, `status` y `variable`. Los valores de estado en API se conservan como `Open`, `Acknowledged`, `Closed`; se presentan como Activa, Atendida, Cerrada. Las operaciones toman al responsable del JWT, conservan el historial y auditan el cambio. Consulta [Ciclo de vida de alertas](ciclo-vida-alertas.md).

## Eventos de riesgo

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/events` | Historial filtrado y paginado de incidentes persistidos. | Administrator / Operator / ConsultationUser | `200`, `400`, `401`, `403` |
| `GET /api/events/{id}` | Detalle con sensores, alertas y trazabilidad. | Administrator / Operator / ConsultationUser | `200`, `401`, `403`, `404` |
| `GET /api/events/statistics` | Totales, activos/cerrados y distribuciones del filtro completo. | Administrator / Operator / ConsultationUser | `200`, `400`, `401`, `403` |

Filtros compartidos: `from`, `to`, `communityId`, `phenomenon`, `level`, `status`. Listado: `page`, `pageSize`. Estados `Open`/`Closed` se presentan como Activo/Cerrado. Los tres endpoints usan la policy `ConsultEvents`; no hay escritura manual de eventos. Consulta [Eventos](eventos.md) para el modelo, evidencia representativa, responsabilidad e integración.

## Dashboard, bitácora y health

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/dashboard` | Obtener el resumen climático persistido. | Público | `200` |
| `GET /api/audit-actions` | Consultar acciones administrativas recientes. | Administrator | `200`, `401`, `403` |
| `GET /health` | Consultar salud de API y SQL Server. | Público | `200`, `503` |

## Administración de usuarios

Todos estos endpoints requieren la policy `AdministratorOnly`, incluidos los GET.

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/users` | Listar, buscar y filtrar usuarios por nombre/login, rol y estado. | Administrator | `200`, `400`, `401`, `403` |
| `GET /api/users/{id}` | Consultar datos de una cuenta. | Administrator | `200`, `401`, `403`, `404` |
| `POST /api/users` | Crear usuario activo con contraseña hasheada. | Administrator | `201`, `400`, `401`, `403`, `409` |
| `PUT /api/users/{id}` | Editar nombre y login. | Administrator | `200`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/users/{id}/status` | Activar o desactivar. | Administrator | `200`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/users/{id}/role` | Asignar uno de los tres roles oficiales. | Administrator | `200`, `400`, `401`, `403`, `404`, `409` |

Los DTOs excluyen hashes y tokens. Consulta [Administración de usuarios](usuarios.md) para contratos, filtros, auditoría y demostración manual.

## Errores

Los errores conocidos se devuelven como `ProblemDetails`. El manejador global reserva `500 Internal Server Error` para fallos no controlados y responde con un detalle genérico para no divulgar información sensible.

Consulta [CRUD y códigos HTTP](crud-y-http.md) y [Seguridad](seguridad.md).

## Bloque 8: dashboard y auditoria

- GET /api/dashboard?communityId={guid}, publico: agrega totalCommunities (global), activeSensors,
  inactiveSensors, activeAlerts (solo Open), alertDistributionByLevel, readingEvolution y recentEvents.
  Conserva campos anteriores. Ver [Dashboard](dashboard.md).
- POST /api/auth/logout, AuthenticatedUser: 204 y auditoria del actor JWT; 401 sin autenticacion.
- GET /api/audit-actions, AdministratorOnly: user/action/entity/from/to/page/pageSize; filtros antes
  de paginacion y aliases anteriores compatibles. Ver [Auditoria](auditoria.md).
- DELETE /api/communities/{id} agrega ComunidadEliminada tras una eliminacion permitida.
- PUT /api/alert-rules/{id} agrega auditoria de estado cuando realmente cambia durante la edicion.
- POST /api/sensor-readings escribe LecturaManualCreada; conserva ValorSimuladoModificado.
