# Backend de Vigía Rural

Base del backend del Sistema Web de Monitoreo y Alerta Temprana para Riesgos Climáticos. La solución utiliza ASP.NET Core y separa sus responsabilidades mediante Clean Architecture.

## Proyectos

- `ClimateAlert.Domain`: conceptos y reglas centrales del dominio, sin dependencias hacia otras capas.
- `ClimateAlert.Application`: casos de uso y coordinación de las operaciones del sistema.
- `ClimateAlert.Infrastructure`: implementaciones técnicas requeridas por la aplicación.
- `ClimateAlert.Api`: entrada HTTP, configuración y exposición de controladores.
- `ClimateAlert.Domain.Tests`: pruebas del dominio y comprobaciones de su arquitectura.

Las dependencias avanzan hacia el dominio: Application depende de Domain; Infrastructure depende de Application y Domain; Api depende de Application e Infrastructure. El proyecto de pruebas depende únicamente de Domain.

## Requisito

- .NET SDK 10

## Comandos

Ejecuta los siguientes comandos desde la raíz del repositorio.

Restaurar dependencias:

```powershell
dotnet restore backend/ClimateAlert.sln
```

Compilar la solución:

```powershell
dotnet build backend/ClimateAlert.sln --no-restore
```

Ejecutar las pruebas:

```powershell
dotnet test backend/ClimateAlert.sln --no-build
```

Iniciar la API:

```powershell
dotnet run --project backend/src/ClimateAlert.Api
```

La comprobación de salud estará disponible en la URL configurada para la API, por ejemplo `http://localhost:5000/health`.

Entity Framework Core, SQL Server, autenticación, SignalR y la lógica climática se incorporarán en las siguientes etapas.
