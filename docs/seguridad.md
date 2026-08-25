# Seguridad

## Inicio de sesión

`POST /api/auth/login` recibe las credenciales por HTTPS en un despliegue seguro. El backend busca al usuario y verifica su contraseña con `PasswordHasher<User>`; no compara ni almacena contraseñas en texto plano.

Si la autenticación es válida, `AuthService` emite un JWT firmado con issuer, audience, expiración e identidad del usuario, incluido su rol `User` o `Administrator`. El visitante no tiene usuario ni JWT. Las claves y credenciales se obtienen de configuración local; `.env` no se versiona.

La demostración académica local usa `user` / `user` para el usuario de consulta y `admin` / `admin` para el administrador. No son credenciales apropiadas para producción y deben reemplazarse en cualquier otro entorno.

## Sesión Angular

Angular conserva la sesión vigente en `sessionStorage`. Esto limita su persistencia a la pestaña o sesión del navegador. El interceptor obtiene el token válido y agrega:

```http
Authorization: Bearer <token>
```

El servicio elimina sesiones expiradas. Al cerrar sesión borra token, usuario y rol de `sessionStorage`, y la aplicación vuelve a `/`.

## Guards y autorización

El guard administrativo comprueba que la sesión esté vigente y que el rol sea `Administrator`. También se ocultan acciones administrativas tanto para visitantes como para usuarios con rol `User`.

> El Guard de Angular mejora la experiencia del usuario, pero la seguridad real también es aplicada por el backend.

Un cliente puede omitir Angular y llamar directamente a la API. Por eso los Controllers protegen escrituras y bitácora mediante `[Authorize(Roles = "Administrator")]`.

## Permisos

| Capacidad | Visitante | User | Administrator |
|---|---:|---:|---:|
| Consultar comunidades, sensores, historial, reglas y alertas | Sí | Sí | Sí |
| Mantener una sesión identificada | No | Sí | Sí |
| Crear, editar o eliminar comunidades | No | No | Sí |
| Administrar sensores y reglas | No | No | Sí |
| Reconocer o resolver alertas | No | No | Sí |
| Consultar bitácora administrativa | No | No | Sí |

## Respuestas de seguridad

- `401 Unauthorized`: falta un JWT válido, está vencido o no supera la validación.
- `403 Forbidden`: el token es válido, pero el usuario no posee el rol requerido.

El manejador global no devuelve trazas ni detalles internos ante un `500`. En producción deben usarse HTTPS, secretos robustos y almacenamiento seguro de configuración.
