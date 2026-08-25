# Endpoints de la API

Base local: `http://localhost:8080`. Las respuestas usan JSON. **Público** permite visitantes sin JWT y usuarios autenticados con rol `User` o `Administrator`; **Administrator** requiere `Authorization: Bearer <token>` con ese rol. Un endpoint protegido responde `401` si falta un token válido y `403` si el JWT es válido pero tiene rol `User`.

## Autenticación

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `POST /api/auth/login` | Validar credenciales y emitir una sesión JWT. | Público | `200`, `400`, `401` |

En la demostración académica local, `user` / `user` emite rol `User` y `admin` / `admin` emite rol `Administrator`. Estas credenciales deben sustituirse fuera del entorno académico.

## Comunidades

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/communities` | Listar comunidades. | Público | `200` |
| `GET /api/communities/{id}` | Consultar una comunidad. | Público | `200`, `404` |
| `POST /api/communities` | Crear una comunidad. | Administrator | `201`, `400`, `401`, `403`, `409` |
| `PUT /api/communities/{id}` | Actualizar nombre, ubicación y descripción. | Administrator | `200`, `400`, `401`, `403`, `404`, `409` |
| `DELETE /api/communities/{id}` | Eliminar una comunidad sin dependencias. | Administrator | `204`, `401`, `403`, `404`, `409` |

## Sensores

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/sensors` | Listar sensores. | Público | `200` |
| `GET /api/sensors/{id}` | Consultar un sensor. | Público | `200`, `404` |
| `GET /api/communities/{id}/sensors` | Listar sensores de una comunidad. | Público | `200`, `404` |
| `POST /api/sensors` | Registrar un sensor. | Administrator | `201`, `400`, `401`, `403`, `404`, `409` |
| `PUT /api/sensors/{id}` | Editar los campos permitidos. | Administrator | `200`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/sensors/{id}/status` | Activar o desactivar un sensor. | Administrator | `200`, `401`, `403`, `404` |

## Lecturas

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/sensors/{id}/readings?page=1&pageSize=20` | Obtener historial paginado. | Público | `200`, `400`, `404` |
| `GET /api/sensors/{id}/readings/latest` | Obtener la lectura más reciente. | Público | `200`, `404` |
| `POST /api/sensor-readings` | Registrar y evaluar una lectura. | Administrator | `201`, `400`, `401`, `403`, `404`, `409` |

`pageSize` admite de 1 a 100. El parámetro heredado `limit` se acepta como alternativa de compatibilidad cuando no se proporciona `pageSize`.

## Reglas de alerta

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/alert-rules` | Listar reglas. | Público | `200` |
| `GET /api/alert-rules/{id}` | Consultar una regla. | Público | `200`, `404` |
| `POST /api/alert-rules` | Crear una regla. | Administrator | `201`, `400`, `401`, `403`, `404`, `409` |
| `PATCH /api/alert-rules/{id}/status` | Cambiar el estado activo. | Administrator | `200`, `401`, `403`, `404` |

## Alertas

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/alerts` | Listar alertas. | Público | `200` |
| `GET /api/alerts/{id}` | Consultar una alerta. | Público | `200`, `404` |
| `GET /api/communities/{id}/alerts` | Listar alertas de una comunidad. | Público | `200`, `404` |
| `PATCH /api/alerts/{id}/acknowledge` | Marcar una alerta como reconocida. | Administrator | `200`, `401`, `403`, `404`, `409` |
| `PATCH /api/alerts/{id}/resolve` | Resolver y cerrar una alerta. | Administrator | `200`, `401`, `403`, `404`, `409` |

## Dashboard, bitácora y health

| Verbo y ruta | Propósito | Acceso | Códigos relevantes |
|---|---|---|---|
| `GET /api/dashboard` | Obtener el resumen climático persistido. | Público | `200` |
| `GET /api/audit-actions` | Consultar acciones administrativas recientes. | Administrator | `200`, `401`, `403` |
| `GET /health` | Consultar salud de API y SQL Server. | Público | `200`, `503` |

## Errores

Los errores conocidos se devuelven como `ProblemDetails`. El manejador global reserva `500 Internal Server Error` para fallos no controlados y responde con un detalle genérico para no divulgar información sensible.

Consulta [CRUD y códigos HTTP](crud-y-http.md) y [Seguridad](seguridad.md).
