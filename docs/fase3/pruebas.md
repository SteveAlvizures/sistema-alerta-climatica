# Pruebas y evidencia vigente

Registro único de conteos de la última regresión funcional, ejecutada el 22/10/2026
(America/Guatemala), sobre `764ac02` y correcciones locales posteriores. **No se
repitió esa regresión en la revisión exclusivamente documental.** Las suites
aprobadas no certifican ausencia de todos los defectos ni producción.

| Bloque | Última evidencia local |
|---|---|
| Gateway restore/build Release | Correctos, 0 errores y 0 advertencias |
| Gateway tests | 58 aprobadas, 0 fallidas/omitidas; incluye interoperabilidad Auth real |
| Auth npm ci/build | Correctos; auditoría npm de esa instalación: 0 vulnerabilidades |
| Auth tests | 43 aprobadas: 27 unitarias/seguridad y 16 integración PostgreSQL real |
| Report pytest | 53 aprobadas, 0 fallidas, MongoDB real y WeasyPrint; Ruff sin errores |
| Backend | 285 aprobadas: Domain 44, Infrastructure 18, Application 223; SQL real habilitado, sin omisiones |
| Angular build producción | Correcto; aviso previo sensors-page.scss 7.02 kB frente a 6 kB |
| Angular ChromeHeadless | 282 aprobadas, 0 fallidas |
| Docker | Builds frontend/Report correctos; stack aislado de ocho servicios healthy |
| Compose config | Base, production y report-development correctos; solo frontend publica puerto |
| E2E HTTP | 33 comprobaciones: login, dashboard, shell /reports, ocho generaciones/descargas, historial y validación |
| Seguridad/persistencia/regresión | 39 comprobaciones; ocho PDFs conservados tras reiniciar Mongo/Report |
| Caídas, health y logs | 10 comprobaciones adicionales aprobadas |

Avisos de herramientas: TestClient de FastAPI/Starlette depreca su integración con
HTTPX; Karma forzó cierre de Chrome después del SUCCESS. No se cambiaron dependencias
ni presupuestos para ocultar estos avisos. Los builds Docker no implican una nueva
auditoría de vulnerabilidades de todas las imágenes.

## Reproducir las suites

Desde la raíz:

```powershell
dotnet restore gateway/ClimateAlert.Gateway.sln
dotnet build gateway/ClimateAlert.Gateway.sln -c Release --no-restore -m:1
dotnet test gateway/ClimateAlert.Gateway.sln -c Release --no-build --no-restore -m:1
dotnet test backend/ClimateAlert.sln -c Release -m:1
dotnet build tools/ClimateAlert.AuthMigration/ClimateAlert.AuthMigration.csproj -c Release -m:1
dotnet tools/ClimateAlert.AuthMigration/bin/Release/net10.0/ClimateAlert.AuthMigration.dll fixtures services/auth-service/test/fixtures/generated.json
```

Para incluir interoperabilidad Gateway, configurar `AUTH_INTEROP_URL` hacia una
instancia Nest de pruebas y `AUTH_INTEROP_FIXTURE` al fixture generado. Sin esas
variables se omiten dos pruebas; no describir esa ejecución como la regresión
completa. Las pruebas Gateway usan Kestrel y EF InMemory para negocio; no sustituyen
SQL Server. Backend requiere `CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING` para sus
casos SQL; usar una instancia dedicada, nunca la base real de negocio.

En `services/auth-service`: `npm ci`, `npm run build`, `npm run test:unit`.
Integración requiere PostgreSQL de
[docker-compose.auth-test.yml](../../deploy/compose/docker-compose.auth-test.yml),
`AUTH_TEST_PASSWORD` privado para Compose, `AUTH_INTEGRATION_TESTS=true`,
`AUTH_DATABASE_HOST=127.0.0.1`, `AUTH_DATABASE_PORT=55432`,
`AUTH_DATABASE_NAME=auth_integration_test`, `AUTH_DATABASE_USER=auth_test`,
`AUTH_DATABASE_PASSWORD` igual a la clave privada usada por Compose y
`AUTH_TEST_CONTAINER=climate-alert-auth-tests-postgres-1`. Entonces ejecutar
`npm run test:integration`. La suite limpia solo su base `_test` y reinicia su
contenedor dedicado. Variables completas en
[integration.test.ts](../../services/auth-service/test/integration.test.ts).

```powershell
docker compose -f deploy/compose/docker-compose.report-test.yml run --build --rm tests
docker compose -f deploy/compose/docker-compose.report-test.yml run --rm tests ruff check app tests
```

Report test usa su Mongo independiente y solo limpia `reports_test`; genera PDFs
reales con fixtures HTTP para fuentes/Auth. En `frontend/angular-app`, ejecutar
`npm run build` y `npm test -- --watch=false --browsers=ChromeHeadless --reporters=dots`.

## E2E y revisión PDF

Configurar `REPORT_E2E_URL`, `REPORT_E2E_USERNAME`, `REPORT_E2E_PASSWORD` en un entorno
privado de validación y ejecutar `python tools/validate-reports.py`. Crea metadatos/
PDFs y un evento de login; no es una consulta totalmente libre de escrituras.
No usar credenciales en el texto del comando. `REPORT_E2E_FROM` permite fijar rango;
por defecto lecturas/estadísticas/ejecutivo usan cinco minutos antes de la última
lectura disponible. El script no imprime JWT ni credenciales.

Evidencia local no versionada: `tmp/report-evidence/http-results.json`,
`edge-results.json`, `failure-results.json`, `docker-health.json`,
`review-pdf-results.json`, PDFs y renderizados. Los scripts auxiliares locales que
probaron caídas cargaron secretos privadamente y solo detuvieron servicios del
stack aislado. No se distribuyen como herramientas de producción.

| PDF real de la última ejecución | Páginas | Bytes |
|---|---:|---:|
| Comunidades | 2 | 14386 |
| Sensores | 5 | 23331 |
| Lecturas | 21 | 69503 |
| Alertas | 3 | 16730 |
| Historial de eventos | 3 | 15955 |
| Estado de sensores | 5 | 22222 |
| Estadísticas climáticas | 2 | 14483 |
| Resumen ejecutivo | 2 | 15458 |

Se comprobaron título, usuario, generación UTC, filtros, pie/numeración en cada
página y firma/MIME/tamaño por HTTP. Se inspeccionaron primeras páginas renderizadas
de los ocho tipos y la página 2 de 27 de la prueba con 130 nombres largos y HTML
escapado. También se probaron los ocho tipos vacíos, N/D, unidades y límites.
Esto no equivale a inspeccionar visualmente cada página de todo PDF posible.

## Pendiente y hallazgos posteriores

- Inspección interactiva de Angular/capturas del [manual de usuario](manual-usuario.md).
- Carga sostenida, instalación nativa completa por plataforma, K3s, TLS externo y
  restauración integral de producción.
- Contratos de los futuros servicios reales: no existen todavía para probar su corte.
- La revisión documental detectó dos entradas Reportes en
  [sidebar.ts](../../frontend/angular-app/src/app/layout/sidebar/sidebar.ts), líneas
  28 y 61. No se corrigió funcionalidad en esta tarea; ambas usan el mismo label
  que el template utiliza como track. Revisar duplicación antes del siguiente cierre funcional.

Los conteos antiguos de incrementos intermedios se retiraron de las páginas
oficiales vigentes para evitar confusiones. La revisión documental valida enlaces,
rutas locales, comentarios y ausencia de cambios ejecutables frente a su estado
inicial; no añade resultados funcionales a la tabla anterior.
