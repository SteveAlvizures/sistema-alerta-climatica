# Antecedente: revisión técnica de Steve del 22/10/2026

> Integración final: [informe del 23/10/2026](INFORME_FINAL_FASE3.md). El Compose base
> conserva el modo estable; `docker-compose.final.yml` agrega los cuatro propietarios
> nuevos y el corte es explícito después de importar/conciliar. No equivale a producción certificada.

Fecha local de revisión: 22/10/2026 (America/Guatemala). Rama `steve-fase3`.
Base: `764ac02 feat: implementar report service y reportería frontend`.
Los PDFs registran su fecha de generación en UTC, por lo que muestran 23/10/2026.
Este antecedente histórico mantuvo Community, Sensor, Alert y Audit en el monolito, sin OAuth,
migraciones destructivas, commit ni push. Los resultados vigentes se centralizan en [pruebas](pruebas.md).
Este documento distingue la regresión funcional previa de la revisión documental posterior.

## Hallazgos y correcciones

| Problema comprobado en código | Corrección y evidencia |
|---|---|
| Solicitudes simultáneas de historial podían sobrescribir la selección nueva con una respuesta antigua | Se cancela la solicitud anterior; prueba HTTP Angular comprueba cancelación y estado loading |
| Un historial indisponible podía presentarse como vacío y mezclaba errores de generación | Error propio del historial, mensaje distinto del estado vacío y reintento con limpieza del error; prueba Angular |
| Fechas inválidas podían lanzar RangeError y dejar generación ocupada | Validación antes de modificar loading o convertir a ISO; prueba de fecha inexistente |
| Usuario autenticado sin permiso de Reportes era enviado a login | Se devuelve al dashboard; visitantes siguen en login, con pruebas de los cuatro roles |
| Auth podía devolver JSON estructuralmente inválido y provocar un error interno | Se valida objeto/nombre/lista de permisos; cuatro casos devuelven 503 controlado |
| Fallo de lectura de metadatos después de guardar PDF cambiaba Completed a Failed | La transición de error solo afecta Pending/Processing; prueba con Mongo real, fallo inyectado de lectura y descarga posterior |
| Límite PDF se comprobaba después de almacenar toda la salida del hijo en memoria del padre | Lectura acotada por chunks y terminación/espera del hijo al superar tamaño, timeout o cancelación; prueba del límite con PDF real |
| Respuestas paginadas aceptaban filas no objeto, total booleano/negativo o hasNext textual | Validación del contrato y 502 controlado; cuatro pruebas |
| Texto de Reportes mencionaba APIs sin aportar información al usuario | Texto de alcance orientado a información de monitoreo |
| Faltaba una guía concreta de corte hacia futuros servicios | Contratos baseline, rutas anidadas, destinos, variables reales, health, persistencia y riesgos en [integracion-pendiente.md](integracion-pendiente.md) |

No se fragmentó ReportsPage por tamaño: conserva una sola responsabilidad de página
y aproximadamente 140 líneas, con HTTP separado en servicios API y modelos tipados.
No se encontraron `any` TypeScript en el frontend productivo. Sidebar y dashboard
mantienen sus patrones existentes; no se rediseñaron ni se modificaron por estética.

## Revisión funcional y seguridad

Se revisaron rutas lazy/guards, sidebar y acceso a Reportes desde dashboard,
interceptores, servicios API y base `/api`, formularios, filtros, historial, carga,
vacíos y descarga Blob. Las etiquetas nativas, avisos role=status/alert, navegación
aria-label/aria-current, foco y estilos responsive existentes se conservaron.
Reportes reduce a una columna en 680px y las tablas permiten scroll horizontal;
el layout/sidebar adapta navegación a móvil en 768px. Esto es revisión de código
y pruebas, no certificación visual interactiva ni auditoría WCAG completa.

Gateway conserva rutas explícitas Auth/Report, fallback de monolito y prioridad de
sensores/alertas anidados bajo comunidades. Se verificaron JWT HS256 con SHA-256
de la clave UTF-8 compatible con Auth/.NET, issuer/audience/expiración y sin margen
de reloj; CORS explícito; eliminación de cabeceras reservadas; correlación UUID;
errores upstream controlados; Location sin autoridad interna y Content-Disposition
expuesto para PDF. Las pruebas existentes cubren estos contratos; no fue necesario
cambiar código Gateway. El corte futuro queda parametrizado y explicado con IDs
reales de rutas y clusters, sin activar servicios inexistentes.

Auth mantiene PostgreSQL propio, hashes existentes, JWT, verificación de identidad
vigente y outbox. Se repitieron pruebas con PostgreSQL real y JWT interoperable.
Report conserva autorización por identidad vigente y propiedad: incluso un admin
recibe 404 para un documento ajeno. Solo usa PyMongo/GridFS para persistencia y
HTTPX para fuentes/Auth; no tiene conexiones o clientes SQL/PG hacia bases ajenas.
Se revisaron índices, historial paginado, recuperación de operaciones abandonadas,
GridFS, límites de filas/bytes, timeout, reintentos GET, escape HTML y bloqueo de
recursos externos de WeasyPrint.

El escaneo de 409 archivos versionados no encontró valores secretos del entorno ni
JWT completos (excluye archivos `.env.example`, que son ejemplos). Se revisaron
también archivos modificados/nuevos y logs de Auth/Gateway/Report sin imprimir
secretos. No se agregaron credenciales ni trazas de excepciones. Los localhost
de configuración son defaults para desarrollo y probes al proceso local;
Compose los sustituye por DNS internos. El overlay de producción exige origen
CORS y mantiene el frontend detrás del proxy TLS del host. Se validó configuración,
no se desplegó a producción ni se probó TLS externo.

## Evidencia de regresión y PDF

Los conteos, comandos, PDFs y límites de evidencia están en
[pruebas](pruebas.md), como única referencia vigente. La revisión documental
no repite esas pruebas ni amplía su alcance.

## Límites y pendientes

La inspección visual interactiva de Angular sigue pendiente por falta de navegador
integrado en las herramientas de revisión. Chrome Headless sí estuvo disponible
para Karma. No se instaló un navegador, no se agregaron dependencias para sustituir
esa revisión y no se considera bloqueo del resto del cierre.

Report sigue síncrono, con una generación por proceso, 2,000 filas/fuente por defecto,
22 segundos de presupuesto de consulta/render y 10 MB/PDF. Esto no equivale a una
cola global ni a pruebas de carga sostenida/Kubernetes. Un crash entre escritura
GridFS y metadatos puede dejar un archivo huérfano; reconciliación/retención y leases
distribuidos siguen como trabajo futuro, sin borrar datos aquí. Se conservan los
límites y se rechazan conjuntos demasiado grandes o incompletos.

Los otros integrantes deben entregar los cuatro servicios con URLs internas,
contratos equivalentes, persistencia y migraciones propias, autorización, health y
pruebas. Community depende de datos Sensor; Alert de comunidades/sensores/lecturas;
Audit debe recibir eventos de Auth y resolver la auditoría administrativa hoy
atómica en SQL. Dashboard permanece en el monolito. No se puede afirmar que esos
cortes estén probados mientras sus servicios no existan.

Steve puede mantener y demostrar Angular/Gateway/Auth/Report, ejecutar estas
regresiones y preparar destinos/redes/probes y pruebas de contrato. El corte de
datos y la integración final requieren coordinación con sus propietarios. La operación se describe en el [manual técnico](manual-tecnico.md).

## Revisión documental posterior

Se separó el material personal del entregable, se consolidaron resultados,
se actualizaron manuales/rutas y se agregaron comentarios de diseño. No se
modificó comportamiento durante esta revisión.

Hallazgo funcional reportado sin corregir: sidebar contiene dos entradas
Reportes (líneas 28 y 61), ambas con el mismo label usado por el track del
template. Las pruebas previas no detectaron esa duplicación. Requiere un cambio
funcional separado antes de certificar la navegación final.

### Validación de documentación

Se verificaron los enlaces Markdown locales y sus anchors, las rutas de archivos,
servicios/puertos/variables frente a Compose y el código, y la separación del índice
oficial. Las referencias externas de YARP, NuGet, PasswordHasher y WeasyPrint
respondieron mediante consulta web; el enlace YARP se normalizó con locale explícito.
WeasyPrint stable describe una versión posterior: la versión instalada sigue fijada
en requirements.lock, sin cambios de dependencias.

La comparación de 230 archivos fuente frente al inicio de esta revisión confirma
que los 29 archivos de código tocados aquí solo cambian comentarios o docstrings
(AST Python normalizado y contenido C#/TypeScript sin comentarios). Esto distingue
los cambios documentales de las correcciones funcionales que ya estaban pendientes
en el working tree. `git diff --check` valida espacios; sus avisos CRLF→LF no son
errores de contenido. No se ejecutaron suites ni se alteraron datos/contenedores
durante esta revisión documental. No se hizo commit ni push.
