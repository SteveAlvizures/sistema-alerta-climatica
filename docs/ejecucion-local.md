# Ejecución local con Docker

## Requisito

Antes de ejecutar los comandos, inicia Docker Desktop y espera a que Docker esté disponible.

## Preparar la configuración

Copia el archivo de ejemplo desde PowerShell:

```powershell
Copy-Item .env.example .env
```

Edita `.env` y reemplaza los valores demostrativos, especialmente las contraseñas y claves. El archivo `.env` es local y no debe agregarse a Git.

Valida la configuración antes de iniciar los servicios:

```powershell
docker compose config
```

## Administrar SQL Server

Inicia SQL Server en segundo plano:

```powershell
docker compose up -d sqlserver
```

Comprueba el estado y el healthcheck:

```powershell
docker compose ps
```

Consulta los registros:

```powershell
docker compose logs -f sqlserver
```

Detén y elimina el contenedor y la red creados por Compose:

```powershell
docker compose down
```

Este comando conserva el volumen y los datos locales de SQL Server. Para eliminar también el volumen, usa:

```powershell
docker compose down --volumes
```

La opción `--volumes` borra permanentemente los datos locales almacenados por SQL Server.
