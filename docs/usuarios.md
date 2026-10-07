# Administración de usuarios — Fase 2, Bloque 4

Implementa RF-ADM-49 a RF-ADM-55. La ruta Angular `/users`, el menú **Usuarios** y todos los endpoints siguientes son exclusivos de `Administrator` mediante `administratorGuard` y la policy backend `AdministratorOnly`.

## Endpoints

| Método | Ruta | Función |
|---|---|---|
| GET | `/api/users` | Listado paginado y filtrado |
| GET | `/api/users/{id}` | Consulta individual |
| POST | `/api/users` | Crear una cuenta activa con nombre, login, contraseña y rol |
| PUT | `/api/users/{id}` | Editar nombre y login, conservando la contraseña |
| PATCH | `/api/users/{id}/status` | Activar/desactivar con `{ "isActive": true }` o `false` |
| PATCH | `/api/users/{id}/role` | Asignar un rol oficial con `{ "role": "Operator" }` |

`GET /api/users` acepta `page` (predeterminado 1), `pageSize` (predeterminado 20, máximo 100), `search` (nombre o login, sin distinguir mayúsculas), `role` e `isActive`. El filtro `ConsultationUser` incluye los registros antiguos `User`.

Respuesta paginada: `data`, `pageIndex`, `pageSize`, `totalPages`, `totalCount`, `hasPrevious`, `hasNext`. Cada usuario contiene `id`, `name`, `username`, `role`, `isActive`, `createdAt`, `lastAccessAt`. El último acceso es nulo si la cuenta aún no ha iniciado sesión.

El contrato de creación contiene `name`, `username`, `password`, `role`. El contrato de edición contiene solo `name` y `username`. No existe borrado físico ni cambio de contraseña en este bloque; una cuenta se desactiva conservando su historial.

La persistencia reutiliza `Users.Email` como login y su índice único. No se modifica el esquema y no se crea una migración EF Core.

## Auditoría

Se registran `UsuarioCreado`, `UsuarioEditado`, `UsuarioActivado`, `UsuarioDesactivado` y `RolUsuarioCambiado` con entidad `User` e Id de la cuenta afectada. La bitácora muestra etiquetas para estas acciones y permite filtrarlas. Repetir un estado o rol que ya está asignado no genera una auditoría ficticia.

## Demostración manual

1. Iniciar sesión con una cuenta `Administrator` existente.
2. Abrir **Usuarios** y crear manualmente las cuentas académicas deseadas: `steve` con `Administrator`, `walter` con `Operator` y `astrid` con `ConsultationUser`.
3. Elegir las contraseñas manualmente; el repositorio no configura ni crea estas cuentas.
4. Buscar las cuentas, filtrar por rol/estado, editar un nombre o login, activar/desactivar y asignar roles desde el listado.
5. Iniciar sesión con cada cuenta para comprobar que solo el administrador ve **Usuarios** y puede acceder a `/users`. El backend devuelve `403` a los otros roles aunque llamen directamente a la API con un JWT válido.

El nombre visible puede diferir del login. Cambiar un login conserva la contraseña y permite iniciar sesión con el login nuevo. La creación es la única operación de este módulo que solicita contraseña y el formulario la borra después de enviarla.

## Verificación y límites

Las pruebas backend ejercitan controllers reales a través de HTTP, autenticación JWT y persistencia EF InMemory. Cubren permisos, CRUD, filtros, paginación, validaciones, auditoría, hashes y efecto de desactivación/cambio de rol sobre JWT existentes. El índice único de SQL Server ya existe; no se modifican datos de una base real en las pruebas.

Angular prueba el menú, el acceso directo protegido por el guard, el formulario, listado, filtros, paginación, roles, estado y errores HTTP.

Los cambios simultáneos entre varios administradores no cuentan con control de versiones por fila en este bloque. La protección del último administrador comprueba el estado actual; no constituye una exclusión transaccional de operaciones concurrentes entre varias cuentas administrativas.
