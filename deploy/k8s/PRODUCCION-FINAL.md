# Despliegue final en vigiarural.site

Aplicaciones desplegadas en el namespace `vigiarural`, una réplica por servicio,
con estrategia Recreate. Imágenes `vigiarural/*:a4cde77`, salvo Auth
`vigiarural/auth-service:phase4-final`, importadas en containerd del nodo.

Las bases de datos de este despliegue son los contenedores Docker existentes
`vigiarural-final-*`. Los Services privados `final-data-*` y sus EndpointSlices
apuntan a sus direcciones Docker. Si se recrean esos contenedores y cambia su IP,
hay que actualizar dichos EndpointSlices antes de reiniciar aplicaciones.
No aplicar `databases.yaml` para reemplazar estas bases.

Gateway usa Nginx TLS como sidecar, certificado público existente y confianza
exclusiva en loopback. Traefik verifica el upstream mediante el ServersTransport
`final-gateway-tls`. Las aplicaciones usan los Services `final-*`. Las credenciales
están en `final-runtime`; no se guardan aquí. No aplicar los manifiestos genéricos
sobre esta instalación sin revisar estas diferencias.

## Datos y compatibilidad

Auth conserva las identidades existentes e incorpora las anteriores sin cambiar
UUID ni hashes. Admite bcrypt heredado y el formato Identity existente. La
migración 003 conserva también el UUID original de cada identidad externa.
Los cuatro sensores físicos, hashes de Device Keys, lecturas y recibos de
idempotencia se incorporaron sin sobrescribir datos existentes.

## Recuperación

En el VPS, `/home/steve/backups/fase4-final/` contiene los backups nativos y
`rollback-k8s-before.json`; `/home/steve/fase4-final-release/` contiene las imágenes
importadas. Son archivos privados; no copiarlos a Git. El snapshot K8s excluye
Secrets. Los nueve contenedores Docker de aplicaciones finales se detuvieron,
pero se conservaron para recuperación; las bases de datos continúan activas.
Un rollback requiere revisar conjuntamente Services, Ingress, Deployments y
ConfigMaps del snapshot; `rollout undo` por sí solo no revierte el enrutamiento.
No restaurar bases sobre datos actuales sin una decisión explícita.

## Validación

TLS público válido; frontend, dashboard y readiness 200. Login 200; generación
de reporte 201 y descarga PDF 200. Device Ingestion sin clave 401, con clave 201,
repetición del mismo messageId 200. JWT inválido 401 y rol de consulta en una
operación de escritura 403. Google/GitHub inician con 302 y PKCE; el consentimiento
real del proveedor requiere interacción del usuario.

La configuración privada preparada del ESP32 permanece fuera de Git. No se
flasheó el dispositivo ni se modificaron sus claves o SensorIds.
