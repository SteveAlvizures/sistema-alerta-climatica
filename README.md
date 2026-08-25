# Vigía Rural

Sistema Web de Monitoreo y Alerta Temprana para Riesgos Climáticos en comunidades rurales. Integra lecturas climáticas simuladas, reglas configurables, alertas, tendencias y herramientas administrativas en una aplicación web desplegable con Docker.

## Objetivo general

Proporcionar una plataforma académica para registrar, consultar y analizar condiciones climáticas de comunidades rurales, detectar situaciones de riesgo mediante reglas y presentar información útil para el seguimiento y la alerta temprana.

## Arquitectura

```text
Angular
  ↓ HTTP/JSON
ClimateAlert.Api — Controllers y autenticación
  ↓
ClimateAlert.Application — servicios, casos de uso y DTOs
  ↓
ClimateAlert.Infrastructure — Entity Framework Core y repositorios
  ↓
SQL Server 2022

ClimateAlert.Domain — entidades, relaciones y reglas de negocio
```

Los Controllers exponen la API; los servicios de aplicación coordinan las operaciones; Infrastructure implementa la persistencia; y Domain mantiene el modelo de negocio sin depender de Angular o SQL Server. Consulta [Arquitectura](docs/arquitectura.md).

## Tecnologías

| Área | Tecnología |
|---|---|
| Frontend | Angular, TypeScript y SCSS |
| Backend | ASP.NET Core sobre .NET 10 |
| Persistencia | Entity Framework Core y SQL Server 2022 |
| Seguridad | JWT, `PasswordHasher<User>`, interceptor y guards |
| Contenedores | Docker, Docker Compose y Nginx |
| Pruebas | xUnit, Jasmine y Karma con Chrome Headless |
| Control de versiones | Git y GitHub |
| Validación Linux | Ubuntu sobre WSL2 |

## Módulos implementados

- Dashboard comunitario con indicadores y resumen de riesgo.
- Comunidades con consulta pública y CRUD administrativo.
- Sensores con consulta, registro, edición y cambio de estado.
- Historial de lecturas con paginación real en la base de datos.
- Alertas con consulta, reconocimiento y resolución.
- Reglas de alerta configurables y activación administrativa.
- Bitácora de acciones administrativas.
- Login y autenticación JWT.
- Simulación periódica de lecturas climáticas.
- Gráficas de tendencias y notificaciones visuales.

## Seguridad

El sistema distingue tres niveles: visitante sin sesión, usuario autenticado con rol `User` y administrador con rol `Administrator`. Los tres pueden consultar la información pública; solo `Administrator` puede ejecutar operaciones administrativas y consultar la bitácora. Angular conserva la sesión en `sessionStorage`, adjunta el JWT con un interceptor y oculta o protege controles según el rol. La autorización efectiva se aplica en el backend mediante `[Authorize(Roles = "Administrator")]`.

Para la demostración académica local, `.env` configura `user` / `user` y `admin` / `admin`. Estas credenciales son únicamente de demostración y deben sustituirse por secretos robustos fuera del entorno académico. Cerrar sesión elimina el token y vuelve a `/`.

No se almacenan secretos de entornos reales en la documentación ni en archivos versionados. Consulta [Seguridad](docs/seguridad.md).

## Base de datos y datos demostrativos

SQL Server almacena comunidades, sensores, lecturas, reglas, alertas, eventos, usuarios, sesiones y bitácora. El dataset académico incluye seis comunidades reales de Guatemala, cinco sensores simulados por comunidad y lecturas históricas. Las localidades son reales, pero las mediciones no representan datos meteorológicos reales.

El volumen `climate-alert-sqlserver-data` conserva los datos entre recreaciones de contenedores. Consulta [Datos de demostración](docs/datos-demostracion.md).

## Paginación

El historial utiliza Offset-Based Pagination:

```http
GET /api/sensors/{id}/readings?page=1&pageSize=20
```

EF Core ejecuta `CountAsync`, `Skip` y `Take` sobre `IQueryable` antes de `ToListAsync`, por lo que SQL Server pagina los resultados sin cargar el historial completo en memoria. El tamaño máximo es 100. Consulta [Paginación](docs/paginacion.md).

## Pruebas verificadas

| Suite | Resultado |
|---|---:|
| Domain | 26/26 |
| Application | 56/56 |
| Infrastructure | 14/14 |
| Backend total | 96/96 |
| Angular | 57/57 |

El build Release del backend finaliza con 0 errores y 0 advertencias; el build de producción de Angular finaliza correctamente.

## Ejecución rápida

1. Crear la configuración local sin versionar:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Completar los valores locales de `.env`.
3. Construir e iniciar los servicios:

   ```powershell
   docker compose up -d --build
   docker compose ps
   ```

4. Abrir el frontend en `http://localhost` y el health del backend en `http://localhost:8080/health`.

La guía completa está en [Ejecución local](docs/ejecucion-local.md).

## Equipo Scrum

| Integrante | Rol |
|---|---|
| Astrid | Product Owner |
| Steve | Scrum Master |
| Walter | Developer |

La distribución describe roles de coordinación y no atribuye cambios específicos sin evidencia del historial Git.

## Documentación

- [Arquitectura](docs/arquitectura.md)
- [Ejecución local](docs/ejecucion-local.md)
- [Endpoints](docs/endpoints.md)
- [Paginación](docs/paginacion.md)
- [Seguridad](docs/seguridad.md)
- [CRUD y códigos HTTP](docs/crud-y-http.md)
- [Validación Linux y Docker](docs/linux-docker.md)
- [Datos de demostración](docs/datos-demostracion.md)
- [Integración continua y validación](docs/integracion-continua.md)
- [Modelo de dominio](docs/modelo-dominio.md)
- [Reglas de alertas](docs/reglas-alertas.md)
- [Bitácora Scrum](docs/bitacora-scrum.md)
- [Guía de defensa técnica](docs/defensa-tecnica.md)

## Evolución futura

Entre las posibles extensiones se encuentran integrar dispositivos físicos mediante adaptadores controlados y desplegar los contenedores en un VPS. Estas posibilidades no forman parte del despliegue actual.
