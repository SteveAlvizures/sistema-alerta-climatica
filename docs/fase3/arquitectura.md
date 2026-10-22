# Arquitectura de Fase 3

## Segundo incremento: Auth extraído

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
        +--> resto de /api/* --> ClimateAlert.Api --> SQL Server existente
```

Auth es propietario de usuarios, credenciales, roles, estado y emisión JWT. El
monolito conserva comunidades, sensores, lecturas, reglas, alertas, eventos, auditoría
y dashboard; consulta por HTTP la identidad vigente a Auth antes de autorizar.
Una outbox PostgreSQL entrega auditoría al monolito por HTTP. SQL conserva los usuarios
originales y una proyección de identidad necesaria para sus claves foráneas, sin ser
fuente de autenticación en el modo externo. No se modificó el esquema SQL Server.
Los contratos públicos permanecen estables. Gateway no accede a bases de datos.

## Arquitectura objetivo, todavía no implementada

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

Reglas y eventos climáticos pertenecen al futuro Alert Service. Las bases de datos
son propiedad de cada servicio; los demás las consultarán mediante contratos HTTP
o proyecciones alimentadas por eventos, nunca mediante conexiones directas.

## Decisiones de este incremento

- Gateway como punto único de acceso a la API; Angular mantiene `/api`.
- Validación criptográfica JWT preliminar, sin imponer autorización global.
- Auth y monolito aplican sus permisos con identidad vigente consultada a Auth.
- Rutas anidadas de comunidad tienen prioridad explícita para futuras extracciones.
- `/api/reports` se reserva y hoy devuelve el 404 del monolito; no hay Report Service.
- `/health` y `/health/live` comprueban vida; `/health/ready` comprueba el upstream.
- Compose de Fase 3 separado, con backend y SQL Server sin puertos publicados.
- El Compose exige un volumen SQL ya inicializado y desactiva migraciones al arrancar.
- Sin OAuth/OIDC ni broker. Se agrega esquema PostgreSQL propio y un importador de
  usuarios no destructivo; SQL Server y sus usuarios originales se conservan.

## Preparación para Kubernetes/K3s

Gateway es stateless y recibe destinos, orígenes CORS y secretos por configuración
externa. No usa IPs fijas ni nombres de contenedores para descubrir servicios.
En Kubernetes, configurar liveness en `/health/live` y readiness en `/health/ready`.
Los manifiestos, gestión de secretos, escalado y outbox quedan para incrementos
posteriores. El simulador del monolito debe seguir siendo una única instancia.

Consulta [Operación del Gateway](gateway.md) para comandos, pruebas y limitaciones.
La extracción y el corte de identidad se describen en [Auth Service](auth-service.md).
