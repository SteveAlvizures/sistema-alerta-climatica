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

Autenticación, SignalR y la lógica climática se incorporarán en las siguientes etapas.

## Persistencia

La capa Infrastructure utiliza `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.EntityFrameworkCore.Design`. El contexto y las migraciones se encuentran en `backend/src/ClimateAlert.Infrastructure/Persistence`.

La API requiere la variable de entorno `SQLSERVER_CONNECTION_STRING`. Su valor debe configurarse localmente y nunca almacenarse en Git.

Restaura la herramienta local de Entity Framework Core:

```powershell
dotnet tool restore
```

Crea una migración desde la raíz del repositorio:

```powershell
dotnet ef migrations add MigrationName --project backend/src/ClimateAlert.Infrastructure --startup-project backend/src/ClimateAlert.Api --output-dir Persistence/Migrations
```

Aplica las migraciones pendientes:

```powershell
dotnet ef database update --project backend/src/ClimateAlert.Infrastructure --startup-project backend/src/ClimateAlert.Api
```

Las credenciales y cadenas de conexión deben permanecer fuera del repositorio.
