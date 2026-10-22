# Validación ejecutada: Auth Service

Segundo incremento validado el 22 de octubre de 2026 (Guatemala), en `steve-fase3`.
El commit base sigue siendo `0b94e22`; no se realizó commit ni push.

## Resultados finales

| Validación | Resultado |
|---|---|
| Auth `npm install` y lockfile | Exit 0 |
| Auth `npm run build` | Exit 0; TypeScript estricto |
| Auth unitarias | 27 aprobadas, 0 fallidas |
| Auth integración PostgreSQL real | 16 aprobadas, 0 fallidas, 0 omitidas |
| Auth total, `node --test` sobre ambos archivos compilados | 43 aprobadas |
| Gateway `dotnet restore` | Exit 0 |
| Gateway build Release | 0 errores, 0 advertencias |
| Gateway tests con Nest real | 49 aprobadas, 0 fallidas, 0 omitidas |
| Backend Domain | 44 aprobadas |
| Backend Infrastructure | 18 aprobadas |
| Backend Application | 200 aprobadas, 3 omitidas |
| Backend total | 262 aprobadas, 0 fallidas, 3 omitidas |
| Herramienta AuthMigration build Release | 0 errores, 0 advertencias |
| Angular build producción | Exit 0, warning SCSS existente |
| Angular Chrome Headless | 254 aprobadas, 0 fallidas |
| Compose Fase 3 config y overlay producción config | Exit 0 |
| Docker build + up --wait | Seis servicios saludables |

Las 47 pruebas anteriores del Gateway se conservaron. Las otras dos comprueban JWT
Nest→Gateway .NET y .NET→Nest, con Nest y PostgreSQL reales. En una ejecución sin
`AUTH_INTEROP_URL` se omiten explícitamente esas dos, pero la ejecución final sí las
habilitó. Las tres omisiones del backend son teorías de atomicidad SQL que requieren
`CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING`; no se habilitó esa conexión ni sus
migraciones de preparación.

Las pruebas PostgreSQL comprobaron esquema versionado, checksum, unicidad,
importación repetida, rollback completo de importaciones conflictivas, usuarios,
roles/estado con JWT existentes, persistencia de hashes y outbox tras reiniciar el
contenedor. Solo limpiaron `auth_integration_test`, una base dedicada de pruebas.

El warning Angular existente corresponde a `sensors-page.scss`: 7.02 kB frente al
presupuesto de advertencia de 6 kB. No hay script lint en Auth; se ejecutó build con
comprobación TypeScript estricta.

## Comparación HTTP con el código legado

Se ejecutó temporalmente la imagen del monolito en modo legado, conectada a la copia
SQL y sin migraciones/seeding/simulación. Las respuestas se compararon contra Auth
a través de Nginx/Gateway. El contenedor temporal se eliminó al finalizar; no montó
el volumen SQL original.

| Caso | Legado | Auth |
|---|---:|---:|
| Credenciales existentes válidas | 200 | 200 |
| Identificador con mayúsculas y espacios exteriores | 200 | 200 |
| Contraseña incorrecta | 401 | 401 |
| Cuenta inexistente | 401 | 401 |
| Campos ausentes | 400 | 400 |
| Contraseña con espacios | 401 | 401 |
| Contraseña vacía | 401 | 401 |
| Identificador vacío | 401 | 401 |

En los casos válidos se compararon las claves de respuesta y valores de nombre,
email y rol. Los tokens emitidos por ese mecanismo .NET también autorizaron
`/api/users` en Auth a través del Gateway. Nombre visible único y ambigüedad de
nombres se comprobaron adicionalmente en integración Nest/PostgreSQL.

La comparación inicial permitió corregir 400 frente al 401 legado para credenciales
en blanco. Se detectó además una diferencia de unos seis segundos entre reloj
Windows y Docker: un token .NET del host quedaba temporalmente antes de `nbf` dentro
de Docker. La comparación final utilizó ambos servicios en Docker. No se aumentó
ClockSkew; producción requiere relojes sincronizados.

## HTTP del stack completo en localhost:8083

| Operación/condición | Código real |
|---|---:|
| Login con credencial importada existente | 200 |
| Login incorrecto o cuenta desactivada | 401 |
| Usuarios sin JWT | 401 |
| Usuarios como Administrator | 200 |
| Usuarios como Operator | 403 |
| Usuarios como ConsultationUser | 403 |
| Crear usuario | 201 |
| Cambiar rol/estado | 200 |
| Rol inválido | 400 |
| UUID inexistente | 404 |
| Login duplicado | 409 |
| Logout | 204 |
| `/internal/identity/current` desde Internet/Nginx | 404 |
| Encabezados de identidad/servicio falsificados sin JWT | 401 |
| Comunidades, sensores, alertas, dashboard | 200 |
| Eventos y auditoría como administrador | 200 |

Con el **mismo JWT**, una cuenta pasó Operator→Administrator→ConsultationUser:
`/api/users` respondió 403→200→403 y la auditoría del monolito 200→403 al retirar
Administrator. Tras desactivarla, tanto Auth `/api/users` como monolito `/api/events`
respondieron 401. Después de reactivarla, logout no revocó el token y eventos
continuó respondiendo 200, preservando la limitación anterior.

Se detuvo exclusivamente el backend del stack aislado: logout conservó 204 y Auth
usuarios conservó 200; comunidades devolvió 502, liveness Gateway 200 y readiness
503. Al reiniciarlo, se entregaron las dos acciones Logout esperadas a la bitácora
SQL. Dos reenvíos adicionales de un evento ya entregado devolvieron 204 y 204,
verificando el camino idempotente del receptor.

Se revisaron logs Auth/Gateway y no contenían los access tokens utilizados, la
contraseña de la cuenta de pruebas ni los secretos internos/PostgreSQL.

## Datos y contenedores

Se reutilizó la copia SQL del incremento anterior,
`climate-alert-phase3-validation-data`, y se exportaron cuatro usuarios con la
herramienta .NET de solo lectura. PostgreSQL recibió:

```text
Primera importación: { inserted: 4, unchanged: 0 }
Segunda importación: { inserted: 0, unchanged: 4 }
```

Los usuarios fuente no se borraron ni se modificó el esquema SQL. La primera
migración del esquema Auth se aplicó únicamente a PostgreSQL propio. La validación
del stack usó `climate-alert-auth-validation-data`; las pruebas de integración,
otro volumen y base separados. Las cuentas adicionales de smoke se crearon solo
en esa copia de Auth; sus proyecciones/auditorías se escribieron solo en la copia SQL.

Los seis servicios llegaron a healthy: frontend, gateway, backend, sqlserver,
auth-service y auth-postgres. PostgreSQL/Auth no publicaron puertos del stack. Para
la herramienta de exportación hubo un puerto SQL temporal de loopback 15433; el
PostgreSQL de pruebas independiente utilizó loopback 55432. Los overrides y secretos
de validación están en `tmp/`, ignorado por Git.

Tras verificar resultados se detuvieron el stack aislado y PostgreSQL de pruebas;
se conservaron contenedores, volúmenes y archivos privados. Los contenedores
originales de Fase 2 permanecieron activos. No se hizo un corte de producción.

## Límites reales

- No se probó TLS/proxy de host en un VPS ni Kubernetes.
- No se realizó navegación manual completa; Angular se validó con build y 254 tests.
- 422 no forma parte de estos endpoints; se preservó 400. Errores 500 sanitizados
  se probaron unitariamente, sin dañar PostgreSQL para provocarlos en producción.
- La auditoría transitoria es eventual. Se requiere monitorizar la outbox y coordinar
  importación/corte y posibles reversiones antes de producción.
- Logout no revoca JWT y no existe flujo refresh/OAuth/OIDC añadido.

Los comandos y decisiones están en [auth-service.md](auth-service.md).
