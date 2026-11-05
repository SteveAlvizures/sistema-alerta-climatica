# Fase 4: K3s adaptado a Steve + Auth/OAuth integrado

Estado: infraestructura preparada; consultar `ESTADO-VALIDACION.md` para las comprobaciones reales del entorno. No interpretar manifiestos existentes como un despliegue exitoso.

Se porta selectivamente el diseño de Walter `22e7fe2` (namespace, Deployments, Services, PVC, probes, Ingress, ConfigMap, HPA y recursos), adaptándolo a las aplicaciones actuales. No se hace merge ni se importa su Auth, Gateway, frontend o modelos antiguos. `apps.yaml`, `backend-alias.yaml` y `apply-hardening.sh` antiguos se descartan: se sustituyen por aplicaciones actuales, backend real y recursos declarativos. El ejemplo antiguo de Secret/JWT_SECRET también se reemplaza.

## Aislamiento y recursos

Namespace candidato **vigiarural-phase4**, distinto de cualquier instalación previa. No sobrescribir servicios/datos existentes. Hay nueve aplicaciones: frontend, gateway, auth-service, community-service, sensor-service, alert-service, report-service, audit-service y backend. Este último sigue siendo necesario como agregador `/api/dashboard` y para relaciones/proyecciones y rutas heredadas.

Siete bases persistentes: auth-postgres, community-postgres, alert-postgres, sensor-sqlserver, report-mongodb, audit-mongodb y sqlserver del backend. PVC local-path explícitos, sin borrado automático ni hostPath arbitrario. Deployments de bases con una réplica y estrategia Recreate evitan dos escritores durante rollout. La persistencia del volumen sobrevive al reemplazo del Pod; la pérdida de nodo no queda resuelta por local-path. Requiere backup fuera del nodo. No afirmar PV/PVC Bound o persistencia operativa sin probarlos en el cluster.

Frontend/Gateway parten de dos réplicas. Sensor queda en una: rate limiting 60/min por dispositivo es por proceso; múltiples réplicas multiplicarían el presupuesto efectivo y un rolling surge también podría hacerlo. Debe mantenerse sin surge (ver manifiesto) hasta implementar un contador compartido. Auth y los otros servicios parten de una réplica. HPA es opt-in: Gateway/Frontend 2–4 y Auth 1–3, requiere Metrics Server; no hay HPA de Sensor ni bases.

## Tráfico y confianza TLS

`https://vigiarural.site` → Traefik Ingress websecure con certificado público → `/api` y `/health` a Service gateway:443 → Nginx TLS sidecar:8443 → Gateway en **127.0.0.1:8080**. `/` sirve frontend:80.

Traefik verifica el certificado del sidecar mediante ServersTransport `serverName: vigiarural.site`, sin insecureSkipVerify. Nginx usa el Secret `vigiarural-public-tls`, determina `$scheme` en su propia conexión TLS, borra forwarding del cliente y fija X-Forwarded-Proto. Gateway confía exclusivamente en 127.0.0.1; no se confía en una red de Pods ni se habilita ASPNETCORE_FORWARDEDHEADERS_ENABLED global.

NetworkPolicy limita entrada al Gateway TLS desde Traefik/Frontend y a Sensor desde los servicios autorizados. Gateway y Sensor no tienen NodePort/LoadBalancer. El hop privado Gateway→Sensor conserva `DeviceIngestion__RequireHttps=false`, igual que el Compose final existente: el límite HTTPS público se aplica en Gateway con peer explícito. No se relaja HTTPS externo, Device Key, idempotencia ni credenciales por sensor. Verificar que el CNI de K3s aplica NetworkPolicy y que las etiquetas Traefik coincidan con las declaradas; no ampliar una allowlist a todo el namespace como solución rápida.

El frontend incluye proxy HTTPS **verificado** hacia gateway para servir el mismo bundle detrás del Nginx host existente si se elige esa topología. Sus probes `/health/live` y `/health/ready` son endpoints explícitos; no son fallback HTML. El Ingress `/health` apunta al Gateway real. Los demás servicios usan sus `/health/live` y `/health/ready`; backend usa `/health` real para readiness y TCP para liveness, porque su API actual no tiene las otras rutas. Gateway se comprueba por exec sobre loopback, sin publicar su puerto HTTP.

La API mantiene `/api/auth`, `/api/users`, `/api/sensors`, `/api/sensor-readings`, `/api/device-readings`, `/api/device-credentials`, comunidades, alertas, auditoría, reportes y dashboard. Los destinos YARP usan exactamente `reports/report-service`, `sensor/primary`, `auth/auth-service`, `monolith/backend`; no se agrega un destino paralelo con nombres antiguos.

## Secretos y OAuth

`secret.example.yaml` solo enumera claves con REPLACE_PRIVATELY. **No aplicarlo** y no rellenarlo en Git. Kustomization lo excluye. Provisionar `vigiarural-secrets` con un archivo privado fuera del repositorio o un gestor de secretos; no imprimir ni hacer `kubectl get secret -o yaml`.

ConfigMap contiene issuer/audience, expiración, DNS/nombres de bases y el origen OAuth. Secret contiene JWT_KEY, AUTH_INTERNAL_KEY, credenciales OAuth, contraseñas de bases y connection strings. `SENSOR_SQL_CONNECTION` debe apuntar a sensor-sqlserver y su base restaurada; `SQLSERVER_CONNECTION_STRING` a sqlserver y base backend restaurada; `COMMUNITY_CONNECTION_STRING` a community-postgres. Mantener claves/usuarios/passwords consistentes con el backup restaurado. No inventar credenciales nuevas al migrar una base existente.

Auth actual usa JWT_KEY SHA-256/HS256, no JWT_SECRET. `OAUTH_ENV=production`, `OAUTH_PUBLIC_ORIGIN=https://vigiarural.site`. Callbacks se derivan de Auth y se registran en las consolas:

- https://vigiarural.site/api/auth/callback/google
- https://vigiarural.site/api/auth/callback/github

Google/GitHub CLIENT_ID y CLIENT_SECRET se suministran al Secret sin mostrarlos. La mera existencia de esas claves no acredita login real: realizar consentimiento/callback con cuentas de prueba cuando esté configurado staging. No usar tokens de proveedor como JWT interno.

## Certificado existente

Reutilizar la cadena pública vigente de vigiarural.site. **Nunca** el certificado/CA local 172.20.10.4. Si el Secret ya existe con otro nombre, adaptar referencias antes de aplicar. Si solo existe en `/etc/letsencrypt/live/vigiarural.site`, un administrador puede importar directamente en el VPS, sin copiar claves a este repositorio:

```bash
# En el VPS, con privilegios suficientes y kubeconfig correcto; no mostrar la salida intermedia.
kubectl create namespace vigiarural-phase4 --dry-run=client -o yaml | kubectl apply -f -
sudo k3s kubectl -n vigiarural-phase4 create secret tls vigiarural-public-tls \
 --cert=/etc/letsencrypt/live/vigiarural.site/fullchain.pem \
 --key=/etc/letsencrypt/live/vigiarural.site/privkey.pem --dry-run=client -o yaml \
 | kubectl -n vigiarural-phase4 apply -f -
```

Repetir importación y rollout de Gateway tras renovación; Nginx no recarga automáticamente el Secret actualizado. No se instala cert-manager ni se solicita otro certificado automáticamente. Si ya existe cert-manager, adaptar el Certificate al Secret anterior y verificar renovación, sin competir con el mecanismo vigente. Traefik CRDs deben incluir ServersTransport.

## Backup, restauración y conservación de IDs

Antes de arrancar aplicaciones sobre datos migrados, seguir `MIGRACION.md`. No borrar producción ni volúmenes, no inicializar semillas/demo, no cambiar UUIDs ni Device Keys. Auth conserva su migración adicional y outbox; Community/Sensor/backend tienen migraciones startup deshabilitadas. Alert ejecuta sus migraciones Flyway versionadas al iniciar: ensayar sobre la copia restaurada y verificar checksum/historial antes del rollout.

No unir automáticamente el esquema Auth antiguo `users/roles` de Astrid con `auth_users` de Steve. Inventariar ambos y definir explícitamente la correspondencia conservando UUIDs y contraseñas. La base aislada física de laptop contiene Device Credentials que deben migrarse junto a sensores, lecturas y recibos, sin imprimir hashes ni claves. Las identidades de las tres fuentes deben reconciliarse antes de publicar rutas nuevas.

## Preparar imágenes y validar

```powershell
# Desde raíz del repo; usar un tag de commit concreto de las aplicaciones, nunca latest.
./deploy/k8s/build-images.ps1 -Tag a4cde77
python deploy/k8s/render.py --tag a4cde77 --output tmp/phase4-release.yaml
python -m pip install -r deploy/k8s/requirements.txt
python deploy/k8s/validate.py
kubectl kustomize deploy/k8s
```

En Docker Desktop con memoria compartida con otros servicios, crear un builder acotado sin cambiar el predeterminado: `docker buildx create --name vigiarural-phase4-limited --driver docker-container --driver-opt memory=1536m --driver-opt memory-swap=1536m`. Ejecutar `build-images.ps1 -Tag a4cde77 -Builder vigiarural-phase4-limited`; un fallo por límite de memoria detiene la construcción y no se debe ignorar. Al terminar, `docker buildx rm vigiarural-phase4-limited` elimina únicamente ese builder temporal y su caché, no las imágenes cargadas con --load ni volúmenes de aplicación.

No se hace push. Importar las nueve imágenes y todas sus dependencias en **cada nodo elegible** de K3s (`sudo k3s ctr images import archivo.tar`), o usar un registro privado aprobado con digest. Confirmar que el tag apunta al source esperado y registrar imageID/digest por Pod. `build-images.ps1` usa el frontend/Gateway/Auth actuales y target runtime de Report; ninguna aplicación se construye desde Walter. Nginx y bases ya tienen digests fijados en estos manifiestos; revisar soporte de arquitectura (SQL Server requiere amd64) y no sustituirlos por latest.

`kubectl apply --dry-run=client` todavía requiere discovery del servidor incluso con validate=false; un fallo por falta de contexto no es un fallo del YAML. Ejecutar dry-run server cuando exista acceso real al cluster. `validate.py` comprueba offline 65 objetos, referencias, probes, puertos, rutas, persistencia y TLS/TrustedProxy; no reemplaza la validación del API server.

## Despliegue incremental

1. SSH al VPS. Con kubeconfig autorizado: `export KUBECONFIG=/home/steve/.kube/config`; verificar contexto, nodo, namespace y recursos existentes. No usar otro contexto por defecto ni exportar el kubeconfig a logs.
2. Comprobar recursos disponibles: dos SQL Server requieren al menos 4 Gi solo en requests, más aplicaciones, PostgreSQL/Mongo y cluster. No desplegar otra infraestructura si el nodo no tiene capacidad. La fase storage rechaza un VPS con menos de 7 Gi disponibles. En un cluster multinodo, preparar un plan por nodo con requests/allocatable y placement antes de adaptar esta barrera, sin eliminarla a ciegas.
3. Provisionar los Secrets privados. `deploy.sh CONTEXTO storage tmp/phase4-release.yaml` crea solo namespace/config/storage/bases candidatas. No cambia el tráfico existente.
4. Restaurar backups y reconciliar datos. Solo después crear ConfigMap `migration-verification` con `status=verified`, referencia privada al backup y al informe de reconciliación. No crearlo para saltar las verificaciones.
5. Cargar imágenes en todos los nodos, TLS vigente y CRDs necesarias. `deploy.sh CONTEXTO apps tmp/phase4-release.yaml`; comprobar rollouts, endpoints y pruebas desde dentro del namespace. Gateway y frontend deben llegar a ready.
6. Antes de Ingress, inventariar host rules existentes. No mantener dos Ingress contradictorios para el mismo dominio. `deploy.sh CONTEXTO ingress tmp/phase4-release.yaml` solo después de planificar la sustitución de la regla anterior. Si Nginx host sigue proxyando un ClusterIP antiguo, adaptar ese upstream con backup de la configuración y `nginx -t`; crear Ingress no cambia ese proxy por sí solo. `host-nginx-upstream.example.conf` prepara Host Nginx→Traefik Service HTTPS verificado (obtener IP con `kubectl -n kube-system get service traefik -o jsonpath='{.spec.clusterIP}'`). No necesita competir por los puertos host 80/443. No instalar el ejemplo sin sustituir el placeholder y validar el cert/Ingress real.
7. Verificar TLS públicamente y `/`, health JSON, dashboard válido y POST device sin key=401. Comprobar reportes con sesión y login local/OAuth real. Solo entonces habilitar HPA opcional y observar.

Rollback: mantener el proxy previo, imágenes y backups. Antes de cutover guardar sus valores en un informe privado. Revertir únicamente el upstream/regla a la versión anterior y, si hace falta, `kubectl rollout undo deployment/NOMBRE -n vigiarural-phase4`. No borrar PVC ni hacer downgrade de esquema sin un plan probado. Evitar nuevos writes durante el rollback hasta reconciliar datos.

ESP32 conserva URL y CA locales. No cargar firmware hasta validar dominio, TLS, Sensor Service, UUIDs/credenciales migrados, 201/200 idempotente, 401 y 429, y cortes/reconexión. Después preparar el cambio de URL y CA pública correcta; nunca setInsecure().
