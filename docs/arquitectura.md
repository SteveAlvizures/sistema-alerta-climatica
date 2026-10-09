# Arquitectura del sistema

## Propósito

Vigía Rural procesa sensores simulados, conserva sus lecturas, evalúa reglas y presenta alertas e información histórica por comunidad.

## Vista general

```text
Angular
  ↓ HTTP/JSON
ClimateAlert.Api / Controllers
  ↓
ClimateAlert.Application / Services / DTOs
  ↓
ClimateAlert.Infrastructure / EF Core / repositorios
  ↓
SQL Server 2022

ClimateAlert.Domain / entidades y reglas de negocio
```

| Proyecto | Responsabilidad |
|---|---|
| `ClimateAlert.Domain` | Entidades, enumeraciones, relaciones y comportamiento del negocio. |
| `ClimateAlert.Application` | Casos de uso, servicios, DTOs, validaciones y contratos de persistencia. |
| `ClimateAlert.Infrastructure` | `DbContext`, configuraciones EF Core, repositorios, SQL Server y simulación alojada. |
| `ClimateAlert.Api` | Controllers, JWT, autorización, errores, health y composición de dependencias. |
| Angular | Interfaz, navegación, consumo HTTP, sesión y experiencia según el rol. |

Las operaciones principales de comunidades, sensores, lecturas, reglas y alertas siguen el flujo Controller → Application → Infrastructure. El Controller debe permanecer delgado y no exponer entidades EF cuando existe un DTO.

## Persistencia

Entity Framework Core mapea el dominio a SQL Server. Las relaciones sensibles, como Comunidad–Sensor, utilizan restricciones en lugar de borrado en cascada indiscriminado. Intentar borrar una comunidad con dependencias produce `409 Conflict`.

El historial pagina desde la consulta SQL mediante `CountAsync`, `Skip`, `Take` y `ToListAsync`. Consulta [Paginación](paginacion.md).

## Lecturas y alertas

El servicio alojado obtiene sensores activos simulados, genera valores compatibles y registra cada lectura mediante el servicio de aplicación. El evaluador busca reglas activas y vigentes; una coincidencia puede abrir o actualizar una alerta y asociarla con un evento.

Aunque el modelo contempla el origen `Physical`, el dataset y la operación demostrada utilizan sensores simulados. Un dispositivo nunca debe acceder directamente a SQL Server.

## Seguridad transversal

La autenticación emite JWT. Angular aporta interceptor y guards, mientras el backend protege cada escritura administrativa por rol. El manejador global transforma errores conocidos en respuestas HTTP y evita exponer detalles sensibles ante errores internos.

## Estructura actual

```text
backend/
  src/
    ClimateAlert.Api/
    ClimateAlert.Application/
    ClimateAlert.Domain/
    ClimateAlert.Infrastructure/
  tests/
frontend/
  angular-app/
docs/
docker-compose.yml
.env.example
README.md
```

## Documentación relacionada

- [Modelo conceptual del dominio](modelo-dominio.md)
- [Reglas de alertas climáticas](reglas-alertas.md)
- [Endpoints](endpoints.md)
- [Seguridad](seguridad.md)
- [Ejecución local con Docker](ejecucion-local.md)
