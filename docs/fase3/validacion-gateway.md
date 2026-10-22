# Resultados del primer incremento

Validación realizada en `steve-fase3`, el 21 de octubre de 2026 (Guatemala).
No se hicieron commits ni push. No se aplicaron migraciones ni se eliminó la
persistencia de Fase 2.

## Compilación y pruebas finales

| Comprobación | Resultado |
|---|---|
| `dotnet restore gateway/ClimateAlert.Gateway.sln` | Exit 0 |
| `dotnet build gateway/ClimateAlert.Gateway.sln -c Release --no-restore -m:1` | Exit 0; 0 errores, 0 advertencias |
| `dotnet test gateway/ClimateAlert.Gateway.sln -c Release --no-build --no-restore -m:1` | 47 aprobadas, 0 fallidas, 0 omitidas |
| `dotnet test backend/ClimateAlert.sln -c Release --no-restore -m:1` | 262 aprobadas, 0 fallidas, 3 omitidas |
| Backend Domain | 44 aprobadas |
| Backend Infrastructure | 18 aprobadas |
| Backend Application | 200 aprobadas, 3 omitidas |
| `npm run build` | Exit 0; warning de presupuesto SCSS de sensores |
| `npm test -- --watch=false --browsers=ChromeHeadless` | Exit 0; 254 aprobadas |
| Compose Fase 3 `config --quiet` | Exit 0 |
| Compose Fase 3 + overlay producción `config --quiet` | Exit 0, con `FRONTEND_ORIGIN` de ejemplo configurado |
| Docker build y `up --wait` | Frontend, Gateway, backend y SQL Server saludables |
| `git diff --check` | Sin errores de whitespace |

Las tres omisiones son las teorías de atomicidad SQL que requieren
`CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING`. No se configuró esta variable ni se
ejecutaron sus migraciones de preparación. La suite Gateway usa HTTP real y
controllers reales con EF InMemory. El smoke Docker sí utilizó SQL Server real.

El warning Angular corresponde al archivo existente
`sensors-page.scss`: 7.02 kB frente al presupuesto de advertencia de 6 kB. La suite
Angular también informa una spec existente de cancelación de comunidades sin
expectativas explícitas; no falló ninguna prueba.

Se corrigieron durante la implementación un fallo de filtrado de forwarding de
YARP y el aislamiento de CORS respecto del upstream. La restauración Docker
detectó `System.Security.Cryptography.Pkcs` transitivo vulnerable; Infrastructure
ahora fija `10.0.13`. La compilación y las suites finales se repitieron después.
Referencias de la corrección: [aviso de seguridad](https://github.com/advisories/GHSA-846f-f764-76mx)
y [segundo aviso](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).

## HTTP real a través de Nginx y Gateway

Destino utilizado: `http://localhost:8083`, sobre una copia restaurada de SQL.

| Petición | Código obtenido |
|---|---:|
| `GET /` | 200 |
| `GET /health` | 200 |
| `GET /health/ready` | 200 |
| `GET /api/communities` | 200 |
| `POST /api/auth/login` | 200 |
| `GET /api/users` sin JWT | 401 |
| `GET /api/users` con JWT administrador | 200 |
| `GET /api/users` con JWT de consulta | 403 |
| `GET /api/does-not-exist` | 404 |
| `OPTIONS /api/users`, preflight de origen permitido | 204 |
| `GET /api/communities`, backend aislado detenido | 502 |
| `GET /health`, backend aislado detenido | 200 |
| `GET /health/ready`, backend aislado detenido | 503 |
| `GET /health/ready`, tras reiniciar backend | 200 |

Se comprobó correlation ID en las respuestas API/health. Se revisaron los logs
del Gateway para confirmar que no contenían el access token emitido, la contraseña
usada para login ni la clave de firma configurada. No se volcaron sus valores.

400 y 409, timeout 504, filtrado de headers y precedencia se probaron en la suite
Gateway sobre Kestrel real; no se provocaron escrituras administrativas para
repetir esos casos sobre la base restaurada.

## Aislamiento de datos y estado del entorno

Docker Desktop estaba detenido y se inició para validar. Los contenedores existentes
de Fase 2 permanecieron activos durante las pruebas. Se creó un respaldo SQL
`COPY_ONLY` y se restauró en el volumen nuevo
`climate-alert-phase3-validation-data`. No se montó el volumen original en otro SQL
Server. La copia utilizó los datos y usuarios existentes, sin ejecutar migraciones
ni seeding de demo, y con simulación automática deshabilitada.

Se usó el proyecto Compose `climate-alert-phase3` y únicamente se publicó
`127.0.0.1:8083`. Tras verificar recuperación, se detuvo el stack de validación;
se conservaron sus contenedores, volumen y respaldo. El script temporal, variables
de validación, backup local y resultados HTTP están en `tmp/`, ignorado por Git.
El respaldo fuente quedó en `/var/opt/mssql/phase3-gateway-validation-20261021.bak`
del SQL original; la copia local está en `tmp/`.

## Alcance que no se afirma probado

- TLS y proxy del host en un servidor de producción: solo se validó el Compose de producción.
- Navegación manual de todas las pantallas: se ejecutaron build, pruebas Angular
  con Chrome Headless y acceso HTTP al frontend construido.
- Migración de servicios o datos, OAuth/OIDC y Kubernetes: quedan fuera de este incremento.

Los contratos y restricciones operativas están en [gateway.md](gateway.md).
