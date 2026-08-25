# CRUD y códigos HTTP

## Comunidades como CRUD completo

| Operación | HTTP | Persistencia | Resultado exitoso |
|---|---|---|---|
| Create | `POST /api/communities` | `INSERT` | `201 Created` |
| Read | `GET /api/communities` o `GET /api/communities/{id}` | `SELECT` | `200 OK` |
| Update | `PUT /api/communities/{id}` | `UPDATE` | `200 OK` |
| Delete | `DELETE /api/communities/{id}` | `DELETE` | `204 No Content` |

POST y PUT reciben DTOs con nombre, ubicación y descripción. PUT modifica la entidad existente, por lo que conserva su identificador y sus relaciones.

DELETE es seguro: antes de eliminar, Application solicita a Infrastructure comprobar sensores, reglas, alertas o eventos asociados. Una comunidad con dependencias responde `409 Conflict`; una comunidad vacía puede responder `204 No Content`.

## Flujo

```text
Angular
  → Controller
  → Application Service / DTO
  → Infrastructure / Entity Framework Core
  → SQL Server
```

Angular nunca se conecta directamente a SQL Server. El Controller traduce HTTP, Application aplica validaciones y reglas, e Infrastructure ejecuta la persistencia.

## Códigos demostrables

| Código | Uso en Vigía Rural |
|---:|---|
| `200 OK` | Consulta o actualización exitosa. |
| `201 Created` | Comunidad, sensor, regla o lectura creada; puede incluir `Location`. |
| `204 No Content` | Comunidad vacía eliminada sin cuerpo de respuesta. |
| `400 Bad Request` | Payload o paginación inválida. |
| `401 Unauthorized` | Escritura sin JWT válido. |
| `403 Forbidden` | Usuario autenticado sin rol Administrator. |
| `404 Not Found` | ID inexistente. |
| `409 Conflict` | Duplicado, transición incompatible o comunidad con dependencias. |
| `500 Internal Server Error` | Fallo no controlado; se devuelve mensaje genérico y se registra internamente. |

No es necesario provocar un `500` durante una defensa. Basta explicar el manejador global y demostrar errores de cliente seguros.
