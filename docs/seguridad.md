# Seguridad

## Inicio de sesión

`POST /api/auth/login` recibe las credenciales por HTTPS en un despliegue seguro. El backend busca al usuario y verifica su contraseña con `PasswordHasher<User>`; no compara ni almacena contraseñas en texto plano.

Si la autenticación es válida, `AuthService` emite un JWT firmado con issuer, audience, expiración e identidad del usuario, incluido su rol. Las claves y credenciales se obtienen de configuración local y no deben incluirse en Git o documentación.

## Sesión Angular

Angular conserva la sesión vigente en `sessionStorage`. Esto limita su persistencia a la pestaña o sesión del navegador. El interceptor obtiene el token válido y agrega:

```http
Authorization: Bearer <token>
```

El servicio elimina sesiones expiradas y permite cerrar sesión.

## Guards y autorización

El guard administrativo comprueba que la sesión esté vigente y que el rol sea `Administrator`. También se ocultan acciones administrativas para visitantes.

> El Guard de Angular mejora la experiencia del usuario, pero la seguridad real también es aplicada por el backend.

Un cliente puede omitir Angular y llamar directamente a la API. Por eso los Controllers protegen escrituras y bitácora mediante `[Authorize(Roles = "Administrator")]`.

## Permisos

| Capacidad | Visitante | Administrator |
|---|---:|---:|
| Consultar comunidades, sensores, historial, reglas y alertas | Sí | Sí |
| Crear, editar o eliminar comunidades | No | Sí |
| Administrar sensores y reglas | No | Sí |
| Reconocer o resolver alertas | No | Sí |
| Consultar bitácora administrativa | No | Sí |

## Respuestas de seguridad

- `401 Unauthorized`: falta un JWT válido, está vencido o no supera la validación.
- `403 Forbidden`: el token es válido, pero el usuario no posee el rol requerido.

El manejador global no devuelve trazas ni detalles internos ante un `500`. En producción deben usarse HTTPS, secretos robustos y almacenamiento seguro de configuración.
