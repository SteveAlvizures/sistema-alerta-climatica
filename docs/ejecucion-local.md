# Ejecución local con Docker

## Requisitos

- Docker Desktop con Docker Compose.
- Puertos 80, 1433 y 8080 disponibles, o configuración equivalente.
- Para ejecutar sin contenedores: .NET 10 SDK y Node.js compatible con Angular.

## Configuración

```powershell
Copy-Item .env.example .env
docker compose config
```

Completa los valores requeridos de `.env`. Este archivo es local, no debe agregarse a Git ni mostrarse en capturas.

## Iniciar la solución

```powershell
docker compose up -d --build
docker compose ps
```

| Servicio | Puerto | Estado esperado |
|---|---:|---|
| Frontend Nginx | 80 | `Up` |
| Backend ASP.NET Core | 8080 | `Up` |
| SQL Server 2022 | 1433 | `healthy` |

```powershell
Invoke-WebRequest http://localhost
Invoke-RestMethod http://localhost:8080/health
```

Respuesta saludable:

```json
{"status":"Healthy","service":"ClimateAlert.Api"}
```

## Operación

```powershell
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f sqlserver
docker compose restart backend
```

Para reconstruir un servicio sin iniciar sus dependencias nuevamente:

```powershell
docker compose build frontend
docker compose up -d --no-deps frontend
```

## Detener sin borrar datos

```powershell
docker compose down
```

El volumen `climate-alert-sqlserver-data` conserva la base. No uses `docker compose down --volumes` salvo autorización expresa para perder esos datos.

## Pruebas

Backend:

```powershell
dotnet restore backend/ClimateAlert.sln
dotnet build backend/ClimateAlert.sln -c Release
dotnet test backend/tests/ClimateAlert.Domain.Tests/ClimateAlert.Domain.Tests.csproj -c Release
dotnet test backend/tests/ClimateAlert.Application.Tests/ClimateAlert.Application.Tests.csproj -c Release
dotnet test backend/tests/ClimateAlert.Infrastructure.Tests/ClimateAlert.Infrastructure.Tests.csproj -c Release
```

Frontend:

```powershell
Set-Location frontend/angular-app
npm ci
npm test -- --watch=false --browsers=ChromeHeadless
npm run build -- --configuration production
```

Consulta [Validación Linux y Docker](linux-docker.md).
