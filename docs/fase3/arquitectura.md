# Arquitectura de Fase 3

## Primer incremento implementado

```text
Navegador Angular 22 (/api)
        |
        v
Nginx del frontend / proxy Angular en desarrollo
        |
        v
API Gateway — ASP.NET Core 10 + YARP
        |
        v
ClimateAlert.Api — monolito de Fase 2
        |
        v
SQL Server existente
```

El monolito conserva usuarios, JWT, autorización definitiva, comunidades, sensores,
lecturas, reglas, alertas, eventos, auditoría y dashboard. Este incremento no extrae
servicios ni modifica DTOs o esquemas de base de datos. La API conserva sus rutas.
El Gateway no accede a SQL Server ni comparte el DbContext.

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
- Autorización y consulta del usuario vigente permanecen en el monolito.
- Rutas anidadas de comunidad tienen prioridad explícita para futuras extracciones.
- `/api/reports` se reserva y hoy devuelve el 404 del monolito; no hay Report Service.
- `/health` y `/health/live` comprueban vida; `/health/ready` comprueba el upstream.
- Compose de Fase 3 separado, con backend y SQL Server sin puertos publicados.
- El Compose exige un volumen SQL ya inicializado y desactiva migraciones al arrancar.
- Sin OAuth/OIDC, broker ni migraciones de datos en este incremento.

## Preparación para Kubernetes/K3s

Gateway es stateless y recibe destinos, orígenes CORS y secretos por configuración
externa. No usa IPs fijas ni nombres de contenedores para descubrir servicios.
En Kubernetes, configurar liveness en `/health/live` y readiness en `/health/ready`.
Los manifiestos, gestión de secretos, escalado y outbox quedan para incrementos
posteriores. El simulador del monolito debe seguir siendo una única instancia.

Consulta [Operación del Gateway](gateway.md) para comandos, pruebas y limitaciones.
