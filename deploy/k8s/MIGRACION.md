# Migración no destructiva y evidencia requerida

No ejecutar restauraciones sobre las bases de producción. Este documento no acredita que una restauración haya sucedido.

1. Inventariar fuentes y propietarios: Docker `vigiarural-final-*`, variante antigua `vigiarural-phase3-*`, Kubernetes existente y banco físico de laptop. Anotar versión SQL Server/PostgreSQL/Mongo, tamaño, bases/volúmenes y consumidores. No escoger un volumen solo porque su nombre se parece al esperado.
2. Programar pausa de escritores y conservar lecturas pendientes del ESP32. Mantener credenciales y UUIDs. Guardar backups privados fuera de Git, permisos solo del operador, transporte SSH y una copia fuera del VPS. Calcular SHA-256 del archivo de backup y registrar hora/fuente sin incluir claves.
3. PostgreSQL: pg_dump en formato custom para Auth, Community y Alert, incluyendo esquema, datos y extensiones; documentar usuarios/permisos aparte. Restaurar solo en las bases candidatas y validar UUIDs, hashes, roles, outbox y relaciones. No ejecutar TRUNCATE sobre producción. Resolver explícitamente diferencias auth_users vs users/roles antes de importar.
4. SQL Server: BACKUP DATABASE COPY_ONLY y CHECKSUM de Sensor y backend; verificar con RESTORE VERIFYONLY. Restaurar con MOVE a rutas del volumen candidato, nunca con REPLACE sobre una base productiva. Reconciliar CommunityId, SensorId, lecturas, DeviceCredential y DeviceReadingReceipt, además de estado de workers/outbox. Backup del Sensor físico debe incluir todos los cuatro UUIDs y hashes de credenciales; no basta exportar sensores por HTTP.
5. Mongo: backup consistente de datos, archivos GridFS/reportes, índices, usuarios y roles de Report/Audit. Restaurar a las bases candidatas con autenticación correcta; confirmar que Mongo init no reescribe usuarios existentes. Mantener auditoría y referencias de usuarios. No activar importadores legacy automáticos para encubrir pérdidas.
6. Las credenciales de conexión se provisionan por Secret y deben coincidir con las bases restauradas. Si se rota una contraseña debe hacerse como operación coordinada independiente, sin modificar Device Keys. Mantener JWT_KEY y AUTH_INTERNAL_KEY coherentes durante la transición.
7. Ensayar las migraciones versionadas sobre una copia: Auth 001 checksum intacto + 002 aditiva; Community/Sensor/backend solo migraciones revisadas; Alert Flyway. No usar EnsureDeleted/EnsureCreated ni recrear esquemas como atajo. Guardar el ensayo y el plan de reversión.
8. Reconciliar conteos antes/después, UUIDs y referencias. Comparar hashes de credenciales/contraseñas en memoria y registrar solo igualdad, nunca valores. Requeridos sensores físicos:
   - temperatura 4d2b0578-3af7-4cd8-a709-5ec5f2eb604f
   - humedad 7cf92f36-bca1-4559-b50b-2834a87baeab
   - lluvia 478f70dd-cb4d-4b77-ad1a-72e71be02884
   - nivel 8418a2d8-c32b-4d87-a390-68ce0df2a500
9. Verificar usuario interno estable por cuenta externa; ningún token Google/GitHub se usa como identidad primaria. Probar login local, usuario desactivado, roles y permisos. Importar cuentas nuevas sin sustituir una cuenta que tenga el mismo login y distinto UUID sin una decisión documentada.
10. Reiniciar **solo Pods candidatos**, no borrar PVC. Confirmar los mismos datos e IDs antes/después. Probar restauración desde backup fuera del nodo; local-path no es HA.
11. Crear migration-verification únicamente cuando exista evidencia de los pasos anteriores. Guardar detalles privados fuera del ConfigMap público y referenciarlos por identificador/checksum. El despliegue se bloquea si la verificación no está hecha.

La presentación final no depende de la laptop: todas las bases, credenciales y servicios necesarios deben estar en el VPS/cluster y el hotspot solo da Internet. Mientras no se migren y validen los datos físicos, no cambiar la URL del ESP32.
