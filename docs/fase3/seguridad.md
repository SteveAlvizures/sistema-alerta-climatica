# Seguridad de Fase 3

> Integración final: [informe del 23/10/2026](INFORME_FINAL_FASE3.md). El Compose base
> conserva el modo estable; `docker-compose.final.yml` agrega los cuatro propietarios
> nuevos y el corte es explícito después de importar/conciliar. No equivale a producción certificada.

## Fronteras y autenticación implementadas

Angular usa `/api`; el interceptor compara origen y prefijo completo antes de
adjuntar Authorization. La sesión se conserva en sessionStorage, no en un secreto
de servidor. Guards/visibilidad solo orientan navegación; los servicios autorizan.

JWT interoperable: HS256 con `SHA256(UTF8(JWT_KEY))`, issuer/audience compartidos,
expiración y tolerancia cero. No se usa el texto de la clave directamente como HMAC.
Gateway valida criptografía sin una política de autorización global: no convierte
consultas públicas en privadas. Auth y Report vuelven a validar; el monolito
verifica el token y consulta identidad vigente por HTTP en modo externo.

Auth consulta usuario activo en PostgreSQL para cada petición protegida; roles
antiguos del JWT no conservan permisos retirados. Report exige identidad coincidente,
rol Administrator/Operator y OperateSystem. Los documentos ajenos responden 404,
también para administradores. Consulta [API](api.md) para permisos por ruta.

401 significa sesión ausente/inválida/inactiva; 403, identidad válida sin permiso.
Hay una diferencia operativa actual: si falla la consulta de identidad, Report
responde 503, pero el monolito falla la autenticación y puede responder 401 en sus
rutas protegidas. No hay fallback a roles SQL obsoletos. Angular no borra la sesión
automáticamente ante cualquier error de red ni reintenta escrituras.

## Credenciales e integración

Auth conserva PasswordHasher Identity V2/V3, PBKDF2 y comparación constante, con
límites sobre formato/coste de hashes importados. Los nuevos hashes usan Identity
V3 SHA512/100000 iteraciones. Importación preserva IDs/hashes y es transaccional;
no hay reseteo masivo, bcrypt ni cambio de contraseña implementado en esta API.

GET interno de identidad exige **JWT y** `X-Internal-Service-Key`; la clave no
sustituye al usuario. La ingesta de auditoría usa esa clave y un ID durable para
reenvíos. SQL mantiene una proyección de identidad sin contraseña local utilizable,
solo para conservar relaciones. Los archivos exportados contienen hashes y deben
guardarse con permisos privados, fuera de Git. .env no se versiona.

Gateway elimina Forwarded/X-Forwarded y familias de cabeceras de identidad/rol/
servicio/permisos del cliente. No confía en ellas para autorizar ni para descubrir
IP real. Retira headers conocidos de infraestructura/CORS del upstream; Location
absoluto HTTP(S) se vuelve relativo. CORS admite orígenes explícitos, expone
X-Correlation-ID y Content-Disposition y no habilita cookies cross-origin.

## Persistencia, PDF y observabilidad

Report solo accede a Mongo propio; las fuentes se consultan por HTTP. El usuario
Mongo de aplicación tiene readWrite en su base; la credencial root inicializa el
volumen. GridFS y metadatos no contienen bearer. Jinja2 escapa contenido y el fetcher
de WeasyPrint impide recursos de red/archivos. Se aplican límites de filas, respuesta,
salida PDF, tiempo y recursos de contenedor; no equivalen a inmunidad ante saturación.

Correlación acepta UUID y se normaliza. Gateway/Auth/Report registran método, estado,
duración e ID, sin cuerpo/query/contraseñas/tokens/stack. Errores de transporte e
inesperados se sanitizan. Backend mantiene logging propio; no se afirma que ya
exista tracing distribuido completo. No activar logs detallados sin revisar datos.

## Evidencia y pendientes

En el modo final Community, Sensor, Alert y Audit comprueban firma HS256 con la
misma derivación SHA-256 de la clave UTF-8 y consultan la identidad vigente en Auth.
No se confía en roles obsoletos del token. Las rutas internas de importación,
evaluación, auditoría y composición requieren clave interna; no tienen rutas
públicas en Gateway. Las outboxes almacenan un payload permitido, nunca el bearer.
Cada servicio tiene credenciales de su persistencia y consulta otros dominios por
HTTP. Las redes y los volúmenes separados no sustituyen permisos mínimos ni backups.

El lock de Auth se actualizó y fija Multer 2.5.0 mediante override; la auditoría npm
de este cierre reportó cero vulnerabilidades. Sensor fija Pkcs 10.0.13 para corregir
la dependencia transitiva detectada. No se afirma una auditoría exhaustiva de Java,
Python o imágenes. Spring solo valida JWT; no se habilitaron flujos OAuth/OIDC.

**PROBADO LOCALMENTE:** firma/audiencia/emisor/expiración, permisos vigentes,
propiedad, headers, CORS, fallos y ausencia de secretos configurados en los archivos
y logs revisados. Alcance exacto en [pruebas](pruebas.md); no es una auditoría de
penetración completa.

**PENDIENTE:** OAuth/OIDC en Fase 4, refresh/revocación de JWT, rate limiting de login,
rotación operativa de claves, gestión de secretos K3s, revisión de accesibilidad
completa y validación TLS/producción. Logout limpia la sesión y registra el evento;
un access token emitido sigue válido hasta expirar si la cuenta conserva acceso.
El TLS externo se configura en el proxy del host; las APIs internas permanecen en
redes Docker no publicadas. No se ha implementado mTLS entre servicios.
