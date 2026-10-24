# Arquitectura de Fase 3

> Integración final: [informe del 23/10/2026](INFORME_FINAL_FASE3.md). El Compose base
> conserva el modo estable; `docker-compose.final.yml` agrega los cuatro propietarios
> nuevos y el corte es explícito después de importar/conciliar. No equivale a producción certificada.

## Modo base conservado: Auth y Report extraídos

```text
Navegador Angular 22 (/api)
        |
        v
Nginx del frontend / proxy Angular en desarrollo
        |
        v
API Gateway — ASP.NET Core 10 + YARP
        +--> /api/auth/*, /api/users/* --> Auth NestJS --> PostgreSQL propio
        |
        +--> /api/reports/* --> Report FastAPI --> MongoDB propio / GridFS
        |                        |
        |                        +--> HTTP APIs de datos (monolito temporal)
        |                        +--> HTTP identidad vigente (Auth)
        |
        +--> resto de /api/* --> ClimateAlert.Api --> SQL Server existente
```

Auth es propietario de usuarios, credenciales, roles, estado y emisión JWT. El
monolito conserva comunidades, sensores, lecturas, reglas, alertas, eventos, auditoría
y dashboard; consulta por HTTP la identidad vigente a Auth antes de autorizar.
Una outbox PostgreSQL entrega auditoría al monolito por HTTP. SQL conserva los usuarios
originales y una proyección de identidad necesaria para sus claves foráneas, sin ser
fuente de autenticación en el modo externo. No se modificó el esquema SQL Server.
Los contratos públicos permanecen estables. Gateway no accede a bases de datos.

## Arquitectura de servicios del overlay final

```text
Angular 22+
    |
    v
API Gateway — ASP.NET Core 10 + YARP
    +--> Auth       — NestJS / TypeScript          --> PostgreSQL propio
    +--> Community  — C# / .NET 10                 --> PostgreSQL propio
    +--> Sensor     — C# / .NET 10                 --> SQL Server propio
    +--> Alert      — Java / Spring Boot           --> PostgreSQL propio
    +--> Report     — Python / FastAPI             --> MongoDB propio
    |                Jinja2 / WeasyPrint
    +--> Audit      — Python / FastAPI             --> MongoDB propio
```

Reglas y eventos climáticos pertenecen a Alert en el corte final. Las bases de datos
son propiedad de cada servicio; los demás las consultarán mediante contratos HTTP
o proyecciones alimentadas por eventos, nunca mediante conexiones directas.

## Decisiones de este incremento

- Gateway como punto único de acceso a la API; Angular mantiene `/api`.
- Validación criptográfica JWT preliminar, sin imponer autorización global.
- Report y monolito aplican sus permisos con identidad vigente consultada a Auth.
- Rutas anidadas de comunidad tienen prioridad explícita para futuras extracciones.
- `/api/reports` se enruta a Report Service; ocho tipos PDF, historial y descarga propia.
- Report valida JWT/identidad vigente y consulta únicamente APIs de datos por HTTP.
- MongoDB y GridFS son persistencia exclusiva de Report, sin bases compartidas.
- Últimas lecturas por lote: endpoint aditivo del monolito, sin migraciones SQL.
- `/health` y `/health/live` comprueban vida; readiness de Gateway verifica monolito, Auth y Report.
- Report readiness verifica MongoDB e identidad; liveness no depende de servicios externos.
- Compose de Fase 3 separado, con backend y SQL Server sin puertos publicados.
- El Compose exige un volumen SQL ya inicializado y desactiva migraciones al arrancar.
- Sin OAuth/OIDC ni broker. Se agrega esquema PostgreSQL propio y un importador de
  usuarios no destructivo; SQL Server y sus usuarios originales se conservan.

## Preparación para Kubernetes/K3s

Gateway es stateless y recibe destinos, orígenes CORS y secretos por configuración
externa. No usa IPs fijas ni nombres de contenedores para descubrir servicios.
En Kubernetes, configurar liveness en `/health/live` y readiness en `/health/ready`.
Los manifiestos, gestión de secretos y escalado quedan para incrementos posteriores.
Auth ya dispone de outbox para auditoría. Report usa una generación síncrona por
proceso, límites de tiempo/filas y persistencia en Mongo; no incorpora broker.
El simulador del monolito debe seguir siendo una única instancia.

Consulta [Operación del Gateway](gateway.md) para comandos, pruebas y limitaciones.
La extracción y el corte de identidad se describen en [Auth Service](auth-service.md).

El tercer incremento se describe en [Report Service](report-service.md) y su
[validación](validacion-reportes.md). La configuración base conserva
las rutas legacy en el modo base; el overlay final activa propietarios separados.

## Flujos y fronteras de confianza

El flujo de usuario es navegador → Angular → interceptor HTTP → Nginx/proxy de
desarrollo → Gateway → propietario → persistencia propia → respuesta. Angular
conserva `/api`; no conoce URLs internas ni credenciales de bases. Los guards
restringen navegación, pero los servicios aplican autorización nuevamente.

Gateway valida JWT de forma preliminar y elimina cabeceras de identidad no fiables.
Auth resuelve usuario/rol/estado vigente desde PostgreSQL. Report valida JWT y
consulta esa identidad antes de generar, listar o descargar documentos propios.
El monolito también consulta identidad Auth por HTTP en el modo externo y conserva
una proyección SQL para relaciones existentes, sin usarla como fuente de permisos.

Report obtiene datos por APIs HTTP de sus propietarios y compone Jinja2/WeasyPrint;
guarda metadatos y bytes GridFS en Mongo. Un PDF Completed no vuelve a consultar
las fuentes al descargarse. La generación no ofrece una instantánea transaccional
entre APIs ni una cola distribuida. Detalles y límites en [Report](report-service.md).

## Disponibilidad y despliegue actual

Liveness de Gateway/Auth/Report indica proceso vivo. Readiness Gateway agrega
monolito, Auth y Report; Auth depende de PostgreSQL y Report de Mongo/Auth. Una
fuente caída impide generar el documento correspondiente, pero no descargar uno
existente si Auth/Mongo responden. Errores y diferencias 401/403/503 están en
[seguridad](seguridad.md) y [troubleshooting](troubleshooting.md).

Compose entrega frontend, gateway, auth-service, auth-postgres, report-service,
report-mongodb, backend y sqlserver. Solo frontend publica puerto; cada persistencia
usa su red/volumen. Producción requiere proxy TLS externo y configuración de origen.
El stack aislado fue probado localmente; producción y K3s siguen pendientes.
Ver [despliegue](despliegue.md) y [pruebas](pruebas.md).

Los cortes futuros requieren contratos compatibles, migración/reconciliación de
datos y actualización de destinos YARP/clientes internos, sin cambiar Angular.
[Integración pendiente](integracion-pendiente.md) especifica rutas y dependencias.
El índice del entregable está en [README](README.md).

## Estado de la integración final

El cierre agrega Community (.NET/PostgreSQL), Sensor (.NET/SQL Server), Alert
(Java/Spring/PostgreSQL) y Audit (FastAPI/Mongo), además de Auth/Report existentes.
`docker-compose.final.yml` define 16 contenedores con backend/SQL transitorios.
El corte cambia ClusterId sin modificar Angular y conserva las rutas anidadas.
Report consulta los nuevos propietarios; dashboard se compone por HTTP desde el
backend; Audit recibe outbox Auth y un feed HTTP de auditoría legacy. Sensor entrega
lecturas a Alert por outbox y conserva UUID deduplicados en el receptor.

No se eliminaron las bases originales. La importación HTTP insert-only preservó
38 sensores, 69,120 lecturas, 13 reglas, 14 alertas y 14 eventos del snapshot.
Paridad avanzada Sensor/Alert, auditoría de nuevas mutaciones y retirada definitiva
del backend siguen pendientes; no lo está la existencia del código de esos servicios.
Diagrama y detalle: [informe final](INFORME_FINAL_FASE3.md#5-arquitectura-de-microservicios).
