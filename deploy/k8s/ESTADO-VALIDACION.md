# Estado real de la integración de infraestructura

4 de noviembre de 2026, rama fase4-integracion-final, base de aplicaciones a4cde77. No confundir preparación con despliegue.

| Punto solicitado | Evidencia / estado |
|---|---|
| 1. Archivos incorporados | namespace.yaml, apps-phase4.yaml, frontend-phase4.yaml, services.yaml, configmap.yaml, secret.example.yaml, storage.yaml, databases.yaml, mongo-init.yaml, nginx.yaml, ingress.yaml, networkpolicy.yaml, hpa.yaml, kustomization.yaml, validate.py, render.py, build-images.ps1, deploy.sh y documentación |
| 2. Walter descartado | apps.yaml anterior, backend-alias.yaml, parches imperativos hardening y configuración JWT_SECRET/puertos antiguos. No se importó código de aplicación de Walter |
| 3. Servicios nuevos desplegados | Ninguno en producción durante esta preparación; nueve aplicaciones y siete bases declaradas |
| 4. Pods | No inspeccionados todavía: kubeconfig remoto no legible por steve |
| 5. Services | Preparados y validados offline. Configuración del Nginx host apunta a ClusterIP 10.43.151.14:80; recurso actual pendiente de inventariar |
| 6. Ingress | Preparado TLS + rutas /api, /health, /. Sin aplicar ni sustituir reglas existentes |
| 7. PV/PVC | Siete PVC local-path preparados; sin acreditar Bound, restore o persistencia en cluster |
| 8. TLS | Dominio público validado con CA del sistema/Python, TLS 1.3 y Let's Encrypt YE2; vence 6 de enero de 2027. No se importó clave/certificado local al cluster |
| 9. Frontend público | HTTP 200. No se ha publicado ni acreditado el bundle actual a4cde77 |
| 10. Dashboard público | /api/dashboard sigue devolviendo 502 |
| 11. Device Ingestion público | POST /api/device-readings con {} y sin clave sigue devolviendo 502; objetivo pendiente: 401 |
| 12. Google | Inicio público 302 a accounts.google.com con callback del dominio; no login real. El inicio actual no incluye PKCE/nonce de la versión nueva |
| 13. GitHub | Inicio público 302 a github.com con callback del dominio; no login real. El inicio actual no incluye PKCE de la versión nueva |
| 14. Gateway | Docker antiguo en loopback 5160 devuelve health 200; no acredita Gateway K3s. Health público devuelve HTML idéntico al frontend |
| 15. Sensor | 43 pruebas locales correctas. Sensor K3s/health productivo no acreditado |
| 16. Reportes | Rutas/manifiestos preparados. Endpoint público sin JWT responde 401; generación/descarga PDF productiva no probada |
| 17. Auth | 59 pruebas correctas contra PostgreSQL exclusivo de pruebas, cero omitidas |
| 18. Gateway pruebas | 72 correctas, incluidas interoperabilidad real Auth y protección de redirects; cero omitidas |
| 19. Sensor pruebas | 43 correctas; Device Ingestion intacto |
| 20. Angular pruebas | 301 correctas |
| 21. TypeScript/build | Build Angular producción correcto; advertencia SCSS sensores 7.02 kB vs presupuesto 6 kB. Auth/Gateway compilan en sus suites |
| 22. Commit | La revisión de infraestructura se registra mediante commit local feat(k8s); consultar git log para el hash. No implica despliegue y no se hace push |
| 23. Git | Cambios limitados a deploy/k8s; aplicaciones y firmware iguales a a4cde77 |
| 24. Cambiar ESP32 | NO. Faltan ruta productiva 401/201, datos/credenciales migrados, salud real y E2E físico con TLS público |
| 25. Pendientes exactos | Habilitar kubeconfig; inventariar dependencias/capacidad; backup y reconciliación; importar imágenes y Secrets/TLS; dry-run server; rollout aislado; validar; cambiar proxy/Ingress con rollback; revalidar producción |

## Bloqueos observados, sin suposiciones

SSH a steve@169.58.83.7 funciona y `systemctl is-active k3s` responde active. `/etc/rancher/k3s/k3s.yaml` es root:root modo 600; sudo no interactivo pide contraseña. El usuario está preparando una copia privada para steve. No se intentó evadir esos permisos ni se mostraron credenciales.

El VPS tiene 11960 MiB de RAM total, unos 1408 MiB disponibles y sin swap en la inspección. Existen entornos Docker vigiarural-final y vigiarural-phase3 simultáneos, además de K3s. No hay memoria suficiente para agregar todas las bases/aplicaciones candidatas. No se detuvieron servicios ni se borraron volúmenes. Revisar consumidores desde K3s antes de liberar duplicados o aumentar capacidad. El script bloquea la fase storage con menos de 7 Gi disponibles.

## Diagnóstico de 502: hechos y límite de la conclusión

Nginx del host dirige vigiarural.site a `http://10.43.151.14:80`, no directamente a los puertos Docker. El Gateway Docker phase3 tiene dashboard/api-fallback→monolith, pero no override del destino monolith: queda `http://localhost:5152/` dentro del contenedor. Tampoco tiene ruta explícita device-readings, por lo que cae al mismo fallback. En ese Gateway, dashboard reproduce 502. El entorno final Docker sí configura backend:8080 y Sensor, pero no es el upstream directo del dominio.

Esta configuración demuestra el defecto en el Gateway phase3 inspeccionado. Sin acceso a los Pods/configuración del Service 10.43.151.14 no se certifica todavía que la causa exacta del Gateway K3s sea idéntica. Los nuevos manifiestos corrigen los dos contratos: monolith→backend:8080 y device-readings→sensor/primary→sensor-service:8080, con backend del dashboard configurado hacia los microservicios. **Los 502 públicos no se declaran resueltos.**

Los health públicos /health/live y /health/ready devuelven text/html y el mismo SHA-256 que /. No se toman como evidencia de salud. El Ingress candidato enruta /health al Gateway; los probes internos verifican endpoints propios.

## Comprobaciones de manifiestos y seguridad

Validación offline correcta de 65 objetos: nueve aplicaciones, siete bases/PVC, referencias ConfigMap/Secret, puertos, destinos YARP, rutas, health, Sensor sin surge y contrato TLS/TrustedProxy. Kustomize/render correcto. `kubectl apply --dry-run=client --validate=false` local no pudo completar discovery porque no existe contexto conectado; no se simula un dry-run de API exitoso. Falta dry-run server al tener acceso real.

Ambas configuraciones Nginx pasan nginx -t en contenedores temporales sin puertos publicados. El certificado local se montó solo como fixture de sintaxis en ese test y nunca se incluyó en manifiestos. No se generaron ni cambiaron certificados. No se modificaron Device Keys, UUIDs, firmware, setCACert, TrustedProxy de la aplicación, polling 1 segundo ni envío ESP32 2 segundos.

Trivy y OWASP ZAP no están instalados ni hay imágenes locales: no ejecutados. No se bloqueó la preparación por su ausencia. No se agregan secretos reales a Git; Secret ejemplo contiene placeholders y no entra en kustomization.

Durante la construcción estándar de imágenes, tras completar Frontend/Gateway/backend/Auth, Docker Desktop falló con EOF y error WSL/VHDX. Se recuperó reiniciando solo Docker Desktop, sin borrar datos ni reiniciar globalmente WSL. Los servicios principales volvieron healthy y se arrancaron Sensor/backend/Gateway de la demo. Nginx de demo no puede recuperar su binding 172.20.10.4:18444 porque Wi-Fi tiene 192.168.1.64; no se cambió IP, certificado, configuración o firmware para disimularlo. La segunda construcción con builder temporal de 1.5 GB completó las **nueve imágenes**; se verificaron sus labels revision=a4cde77 y arquitectura amd64. Se retiró únicamente ese builder y se conservaron las imágenes cargadas. Esto no modificó el VPS. Alert también completó su build Maven (11 pruebas correctas y una prueba opcional de persistencia omitida).
