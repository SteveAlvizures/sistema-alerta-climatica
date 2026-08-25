# Validación en Ubuntu WSL2 y Docker

Vigía Rural fue validado desde una terminal Ubuntu en WSL2 para comprobar portabilidad y operación sin depender de una interfaz gráfica de administración.

## Comandos utilizados

```bash
docker --version
docker compose version
docker compose up -d
docker compose ps
curl http://localhost
curl http://localhost:8080/health
```

## Estado validado

```text
SQL Server → healthy
Backend    → Up
Frontend   → Up
```

Respuesta del health:

```json
{"status":"Healthy","service":"ClimateAlert.Api"}
```

El health del backend consulta los health checks configurados, incluido SQL Server. El frontend es servido por Nginx, la API escucha en el puerto 8080 y SQL Server conserva sus archivos en un volumen con nombre.

## Alcance

WSL2 se utilizó como entorno Ubuntu local para validar comandos, red de Compose y acceso HTTP mediante terminal. No existe un VPS configurado como parte del estado actual del proyecto.

Un despliegue en VPS Linux es una posible etapa futura. Requeriría, entre otros aspectos, HTTPS, gestión de secretos, firewall, copias de seguridad, observabilidad y una estrategia de actualización.
