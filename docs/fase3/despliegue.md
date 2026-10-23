# Despliegue y recuperación

## Alcance implementado

El archivo [docker-compose.phase3.yml](../../deploy/compose/docker-compose.phase3.yml)
define ocho servicios: frontend, gateway, auth-service, auth-postgres,
report-service, report-mongodb, backend y sqlserver. Solo frontend publica puerto,
por defecto `127.0.0.1:8082`. No ejecutar simultáneamente SQL Server de Fase 2 y
Fase 3 contra el mismo volumen. Los Compose originales de Fase 2 se conservan.

## Preparación de datos y primer corte

1. Preparar respaldo y una copia restaurada para validar el cambio. Configurar
   `.env` según [manual técnico](manual-tecnico.md). `PHASE3_SQL_VOLUME` debe existir
   e incluir el esquema Fase 2; este Compose no inicializa una base SQL vacía.
2. Para el corte real, detener escrituras antiguas de identidad y exportar usuarios
   mediante `tools/ClimateAlert.AuthMigration` en modo solo lectura. Procedimiento
   exacto y permisos del archivo en [Auth Service](auth-service.md#importación-segura-de-usuarios).
3. Iniciar auth-postgres/auth-service e importar el JSON por la herramienta de Auth.
   Auth crea esquema propio aditivo y comprueba checksum; no modifica SQL Server.
4. Conciliar usuarios/IDs y credenciales antes de habilitar tráfico. Reimportar el
   mismo snapshot no sobrescribe cuentas; conflictos abortan toda la transacción.
5. Iniciar el stack completo y comprobar health y flujo de usuario con datos de prueba.

El init Mongo crea usuario/base únicamente en un volumen nuevo. Cambiar un password
en `.env` no actualiza una credencial ya persistida. Auth/PostgreSQL y Report/Mongo
tienen volúmenes propios; `AUTH_POSTGRES_VOLUME` y `REPORT_MONGO_VOLUME` permiten
aislar validaciones. Report no necesita migraciones en bases ajenas.

## Comandos desde la raíz

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml up -d --build --wait
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml ps
```

No ejecutar el segundo comando antes de preparar SQL e importar identidad. Las
migraciones SQL y el demo seeding están deshabilitados en esta configuración.
El puerto 8083 de evidencia pertenece al override local de validación, no al YAML
entregado por defecto. Para comprobar vida/readiness con el puerto predeterminado:

```powershell
Invoke-RestMethod http://localhost:8082/health/live
Invoke-RestMethod http://localhost:8082/health/ready
```

## Producción

**IMPLEMENTADO:** overlay
[docker-compose.phase3.production.yml](../../deploy/compose/docker-compose.phase3.production.yml),
Nginx hacia Gateway, origen obligatorio `FRONTEND_ORIGIN` y edición SQL Express por
defecto en el overlay. Confirmar licencia/capacidad antes de elegir otra edición.
Mantener binding loopback y configurar TLS en el proxy del host; no publicar el
puerto HTTP directamente como entrada de Internet.

```powershell
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.phase3.production.yml config --quiet
docker compose --env-file .env -f deploy/compose/docker-compose.phase3.yml -f deploy/compose/docker-compose.phase3.production.yml up -d --build --wait
```

**PROBADO LOCALMENTE:** config base/overlay de producción y build/arranque del stack
aislado. **PENDIENTE:** ejecución en el host de producción, certificado/TLS externo,
DNS público, backups/restauración integral y capacidad. No hay manifiestos K3s
entregados; su preparación arquitectónica no equivale a despliegue Kubernetes.

El overlay [report-development](../../deploy/compose/docker-compose.report-development.yml)
monta código Python y activa recarga para desarrollo. No combinarlo con producción.

## Recuperación y rollback

Comprobar `ps`, health, logs con correlación y conectividad a la base propia antes
de reiniciar el servicio afectado. Las pruebas locales verificaron reinicio Mongo/
Report conservando PDFs y reinicio PostgreSQL conservando datos de Auth. No se
probó restauración de todos los volúmenes desde backups de producción.

No usar `down -v`, borrar volúmenes ni reaplicar migraciones destructivas para
resolver errores de arranque. Guardar configuración/destinos anteriores permite
revertir proxy, pero después de escribir usuarios en PostgreSQL no basta volver
a SQL: hay que conciliar cuentas. Igualmente, futuros cortes de dominio requieren
reconciliación de datos antes de rollback. Una copia restaurada debe usar un volumen
distinto al original; nunca montar un mismo volumen en dos motores activos.

Retención, reconciliación de huérfanos GridFS, monitoreo de outbox y objetivos
RPO/RTO requieren procedimientos operativos acordados. No hay borrado automático
de PDFs ni cola global de generación. Síntomas: [troubleshooting](troubleshooting.md).
