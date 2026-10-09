# Seguridad

## Inicio de sesión y JWT

`POST /api/auth/login` verifica la contraseña con `PasswordHasher<User>` y emite un JWT firmado con issuer, audience, expiración, identidad y rol. Las claves y credenciales iniciales se obtienen de configuración; `.env` no se versiona.

El login se almacena en la columna existente `Users.Email`. El backend prioriza ese identificador único; mantiene el acceso antiguo por nombre visible solo cuando identifica exactamente una cuenta. Se permiten nombres visibles repetidos sin romper el login.

Los roles oficiales son `Administrator`, `Operator` y `ConsultationUser`. Una cuenta persistida con el rol antiguo `User` se interpreta como `ConsultationUser`. El formato y firma de los JWT existentes se mantienen.

En cada solicitud con un JWT válido, el backend comprueba que la cuenta exista y esté activa y utiliza su rol actual en la base de datos. Una desactivación invalida el acceso autenticado inmediatamente; un cambio de rol afecta los permisos sin esperar al vencimiento del JWT. Esto requiere una consulta adicional a la base de datos por solicitud autenticada.

## Sesión Angular

Angular conserva la sesión en `sessionStorage`. El interceptor agrega `Authorization: Bearer <token>` cuando el token está vigente. Al cerrar sesión elimina la información local y vuelve a `/`.

El guard administrativo verifica una sesión vigente con rol `Administrator`. Las rutas `/users` y `/audit-log` usan ese guard; sus enlaces se ocultan para otros roles. Los menús reflejan el rol obtenido en el último login; si otro administrador cambia el rol, el backend aplica el cambio inmediatamente y un nuevo login actualiza la sesión visual.

## Autorización backend

| Capacidad | Visitante | ConsultationUser / User antiguo | Operator | Administrator |
|---|---:|---:|---:|---:|
| Consultas públicas actuales | Sí | Sí | Sí | Sí |
| Escrituras de comunidades, sensores, lecturas, reglas y alertas | No | No | Sí | Sí |
| Consultar bitácora | No | No | No | Sí |
| Listar, consultar o administrar usuarios | No | No | No | Sí |

La policy `OperateSystem` exige `Administrator` u `Operator`. `AdministratorOnly` exige `Administrator` y protege todas las acciones de `UsersController`, incluidos los GET, y la bitácora. El guard mejora la interfaz; la seguridad real se aplica en backend.

## Administración de usuarios

- Se devuelve un DTO explícito: Id, nombre, login, rol, estado, fecha de creación y último acceso. No se serializan entidades, hashes ni refresh tokens.
- El login se normaliza a minúsculas y se comprueba su unicidad, también para cuentas inactivas. Se conserva el índice único existente. Los conflictos de unicidad de SQL Server devuelven `409`.
- La contraseña de creación debe tener entre 8 y 128 caracteres y no estar formada solo por espacios. Se almacena únicamente su hash con `PasswordHasher<User>`.
- `PUT` modifica solo nombre y login. No se implementa cambio de contraseña; campos extra como `passwordHash`, rol o estado no se aplican mediante ese endpoint.
- La asignación de rol admite solo los tres roles oficiales. El alias `User` es de lectura y compatibilidad.
- No se permite desactivar ni quitar el rol de administrador de la propia cuenta. También se comprueba que permanezca un administrador activo al desactivar o degradar una cuenta administrativa.
- Cada cambio y su auditoría se guardan juntos mediante un único `SaveChangesAsync`; la auditoría identifica al administrador y al usuario afectado y nunca contiene contraseñas.
- El seeder configura las cuentas iniciales solo cuando `Users` está vacío. No sobrescribe cuentas existentes ni recrea el login antiguo después de una edición.

Consulta [Administración de usuarios](usuarios.md) para los endpoints y la demostración manual.

## Respuestas HTTP

- `401`: falta un JWT válido, está vencido, la cuenta está inactiva o ya no existe.
- `403`: la identidad es válida, pero el rol actual no permite la acción.
- `400`: datos inválidos, rol desconocido o estado omitido.
- `404`: usuario no encontrado.
- `409`: login duplicado o cambio administrativo que bloquearía el acceso propio.

El manejador global no expone trazas ni detalles internos ante un `500`. En producción se usan HTTPS y secretos configurados fuera del código.

## Bloque 8: sesiones y bitacora

POST /api/auth/logout usa AuthenticatedUser y toma el actor del JWT. Es stateless: registra Logout,
elimina la sesion del cliente y no revoca tokens ni agrega blacklist. GET /api/audit-actions sigue
requiriendo AdministratorOnly (401 visitante, 403 Operator/ConsultationUser). Los filtros y nombres de
usuario enviados solo afectan consultas, nunca determinan el autor auditado.
GET /api/dashboard conserva acceso publico; sus eventos resumidos excluyen responsables de auditoria.
El detalle /api/events/{id} y su ruta Angular conservan autenticacion. Ver [Auditoria](auditoria.md).
