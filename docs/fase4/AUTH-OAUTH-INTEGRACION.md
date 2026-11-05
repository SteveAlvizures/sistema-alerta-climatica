# Integración Auth/OAuth fase 4

Base: `fase4-steve` (`7d04571`). Port selectivo de Google/OIDC, GitHub OAuth, callbacks y cuentas externas de Astrid (`c8e9cc7`, `4406560`), adaptado al esquema PostgreSQL, identidad interna y outbox existentes. No se integra su reemplazo de servicios fase 3 ni Kubernetes de Walter.

## JWT y roles

Una sola configuración de firma: `JWT_KEY`, mínimo 24 caracteres. Auth y runtimes existentes derivan los bytes de la clave mediante SHA-256 del UTF-8; firma HS256. Mantener iguales `JWT_ISSUER` y `JWT_AUDIENCE` en Gateway y servicios; defaults `climate-alert-api` y `climate-alert-client`. `JWT_EXPIRATION_MINUTES` controla la emisión (default 60). No introducir `JWT_SECRET` como segunda clave. Los validadores rechazan firma/issuer/audience/expiración inválidos.

Sesiones humanas y API de administración publican ADMIN, OPERATOR, VIEWER. JWT incluye `role` y `roles` oficiales, y el claim URI de rol compatible con .NET. Una única tabla de alias conserva equivalencias:

| Oficial | Alias de compatibilidad |
|---|---|
| ADMIN | Administrator |
| OPERATOR | Operator |
| VIEWER | ConsultationUser, User, Viewer |

Los roles persistidos y `/internal/identity/current` mantienen temporalmente los nombres existentes para Sensor, Community, reportes, alertas, auditoría y backend. Auth normaliza aliases antes de decidir permisos; Angular también acepta sesiones previas. No se convierten filas ni se reemplazan IDs. ADMIN administra usuarios; OPERATOR opera; VIEWER consulta. Identidad interna requiere JWT y credencial del servicio y vuelve a consultar cuenta/rol/estado actual en cada petición protegida. El backend acepta aliases oficiales sin ampliar permisos. Device Ingestion sigue usando su autenticación independiente por dispositivo.

## Usuarios, migración y auditoría

`001_auth.sql` no cambia: se conserva su checksum. `002_oauth.sql` agrega `auth_external_accounts`, intentos y tickets OAuth, y permite password_hash NULL para nuevas cuentas externas. No modifica hashes ni IDs de usuarios existentes, ni sensores o Device Keys. No se ejecutó migración sobre bases reales durante esta integración.

La relación `(provider,subject)` apunta a un UUID interno estable, con unicidad por usuario/proveedor. Solo un email verificado puede asociarse automáticamente al username/email interno exacto. No se vincula por nombre visible. Si no existe usuario se crea uno de consulta, con password NULL; no podrá entrar por contraseña hasta que exista un procedimiento explícito para asignarla. Nunca se actualiza el rol local desde el proveedor. Las cuentas inactivas se rechazan. La vinculación se serializa con administración para evitar carreras; los fallos revierten la transacción.

El login externo encola el evento Login con referencia al proveedor en el outbox durable existente, junto al acceso de usuario y canje del ticket; no depende de una llamada síncrona a Audit. No se guardan tokens Google/GitHub ni secretos de proveedor en cuentas, auditoría o respuestas.

## Flujo y protecciones

1. GET `/api/auth/oauth/google` o `/api/auth/oauth/github` crea state impredecible, PKCE S256 y cookie host-only `vigia_oauth`, HttpOnly, SameSite=Lax, Secure con HTTPS, Path=/api/auth, 10 minutos.
2. PostgreSQL conserva hash del state y hash del navegador, proveedor, verifier, nonce y expiración. El callback exige los mismos navegador/proveedor e intento vigente. DELETE atómico consume el intento antes de contactar al proveedor; no se puede reutilizar ni entre réplicas.
3. GET `/api/auth/callback/google` verifica el ID token mediante la biblioteca oficial Google, audiencia, firma/vigencia, email verificado y nonce del intento. GitHub usa siempre `/user/emails` y exige `verified=true`; nunca acepta como prueba el email público de `/user`. Todas las peticiones fetch de proveedor tienen timeout y rechazan redirects.
4. El callback entrega únicamente un ticket aleatorio de 60 segundos en `/auth/callback#code=...`. El JWT nunca viaja en URL. Angular borra el fragmento inmediatamente.
5. Angular hace POST `/api/auth/oauth/exchange` con el ticket. Auth exige Origin exacto y cookie del intento, consume ticket en PostgreSQL, consulta usuario activo y entrega el JWT interno. La cookie se elimina. Respuestas sensibles llevan no-store y los callbacks no-referrer. El ticket no sirve desde otro navegador.
6. Angular usa sessionStorage e interceptor existentes; logout conserva el comportamiento vigente: limpia sesión cliente y audita, sin revocar automáticamente otros JWT todavía vigentes. Estado/roles vigentes se consultan en Auth.

Gateway preserva redirects externos solo en inicio OAuth y solo a HTTPS, puerto estándar y rutas exactas de autorización Google/GitHub. Los demás redirects absolutos siguen convirtiéndose a relativos; los filtros de identidad, Device Keys y TrustedProxy no se debilitan.

## Configuración sin secretos en Git

Usar variables de entorno `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`. Los ejemplos quedan vacíos. No copiar valores a Angular, código, comandos visibles ni reportes. Sin credenciales, OAuth no se puede iniciar; login local sigue independiente.

Local: `OAUTH_ENV=local`, `OAUTH_PUBLIC_ORIGIN=http://localhost:4200`. Angular y /api deben compartir origen mediante su proxy de desarrollo hacia Gateway. Registrar callbacks:

- `http://localhost:4200/api/auth/callback/google`
- `http://localhost:4200/api/auth/callback/github`

El origen local también puede usar HTTPS con hostname/IP correcto; HTTP fuera de loopback se rechaza. No se infiere el origen desde Host o X-Forwarded enviados por clientes.

Producción: `OAUTH_ENV=production`, `OAUTH_PUBLIC_ORIGIN=https://vigiarural.site`. Se rechaza cualquier otro origen. Callbacks derivados:

- `https://vigiarural.site/api/auth/callback/google`
- `https://vigiarural.site/api/auth/callback/github`

No hay redirects dinámicos elegidos por clientes. Configuración Compose base incluye variables opcionales; overlay de producción fija dominio/entorno. No se desplegó ni se modificaron consolas OAuth. No se imprimieron secretos.

## Validación y límites

Las pruebas unitarias simulan respuestas de proveedores y verificación Google; las de integración ejercitan HTTP Nest/PostgreSQL, vinculación, IDs/hashes, state/cookie/proveedor, expiración y replay, ticket/Origin, identidad y 403. Se valida JWT real Node→Gateway y .NET→Auth con instancia y DB exclusivas de pruebas. Las suites actuales de dispositivos se conservan. Angular cubre callback, error, canje, sesión y roles.

Pendiente operativo: registrar/verificar aplicaciones reales Google/GitHub, configurar secretos fuera de Git y probar consentimiento/login/callback/logout reales en staging con TLS. Las pruebas simuladas no acreditan que las consolas productivas estén correctas. Antes de Walter: resolver el dashboard/device 502 público, adaptar manifiestos a los contratos preservados, imágenes identificables, TLS/Ingress y confianza explícita de proxy; verificar migración, respaldos y persistencia. No cambiar firmware o URL del ESP32 todavía.

La advertencia Pkcs 9.0.11 pertenece al árbol alternativo de Astrid: Steve ya referencia System.Security.Cryptography.Pkcs 10.0.13. Se conserva esa versión y se consulta NuGet, sin actualizar mayores a ciegas.

Validación ejecutada el 4 de noviembre de 2026:

| Comprobación | Resultado |
|---|---|
| Auth build + pruebas, DB exclusiva auth_integration_test | 59 correctas, cero omitidas |
| Gateway completo con interoperabilidad Auth real | 72 correctas, cero omitidas |
| Sensor completo | 43 correctas, cero omitidas |
| Angular ChromeHeadless | 301 correctas, incluyendo las 295 existentes |
| Angular build producción / TypeScript | Correcto; advertencia de presupuesto SCSS sensores 7.02 kB frente a 6 kB |
| Backend Domain / Infrastructure / Application | 48 / 18 / 200 correctas; 4 pruebas opcionales Application omitidas |
| npm audit Auth y Angular | Cero vulnerabilidades reportadas |
| NuGet backend y Gateway, incluyendo transitivas | Cero vulnerabilidades reportadas |
| Diff firmware, Sensor, dashboard, reportes, TrustedProxy y Kubernetes frente a Steve | Sin cambios |

No se actualizaron imágenes/contenedores del entorno físico ni se ejecutó migración sobre bases de producción. El contenedor PostgreSQL exclusivo de pruebas se utilizó para migraciones, reinicio/persistencia e interoperabilidad; los proveedores externos se simularon. El login OAuth real sigue pendiente de staging.

Comprobaciones operativas finales: Compose pasa `config --no-interpolate --quiet`; la interpolación con el `.env` actual no puede completarse porque falta `REPORT_MONGO_ROOT_PASSWORD`. No se agregó ni inventó ese secreto. Los cuatro contenedores físicos siguen activos y Gateway loopback devuelve 200. HTTPS en 172.20.10.4:18444 dio timeout porque Wi-Fi ahora tiene 192.168.1.64; el binding Nginx conserva 172.20.10.4. No se cambió esa configuración ni firmware: el reensayo físico requiere volver a la red/IP original. Esto limita la comprobación física actual y no se presenta como una validación E2E del hardware.

Referencias de implementación: [Google OIDC](https://developers.google.com/identity/openid-connect/openid-connect), [GitHub OAuth/PKCE](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps).
