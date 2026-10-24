# Troubleshooting

En modo final, 502 al consultar comunidades/sensores/alertas puede indicar que el
propietario HTTP no está disponible; no cambiar credenciales de otra base para
resolverlo. Consultar su health y logs con correlación. Readiness 503 con liveness
200 durante una caída es esperado. Los PDFs existentes pueden descargarse aunque
una fuente de datos esté caída. Un 422 al generar un reporte con demasiadas filas
requiere acotar filtros, no aumentar límites sin medir memoria y tiempo.

Si después del arranque se ven datos legacy, comprobar las cuatro variables
`*_ROUTE_CLUSTER`: por seguridad el overlay no corta tráfico automáticamente.
Antes de habilitarlas, importar/conciliar con escrituras detenidas. Si falta una
acción reciente de auditoría, revisar outbox y disponibilidad de Audit: la entrega
es eventual e idempotente. No borrar pendientes ni reiniciar volúmenes.

> Integración final: [informe del 23/10/2026](INFORME_FINAL_FASE3.md). El Compose base
> conserva el modo estable; `docker-compose.final.yml` agrega los cuatro propietarios
> nuevos y el corte es explícito después de importar/conciliar. No equivale a producción certificada.

Usar primero health, estado de contenedores y el ID de correlación, sin copiar
credenciales, JWT, exportaciones de usuarios o logs privados a incidencias públicas.
Los comandos básicos están en [manual técnico](manual-tecnico.md#health-logs-y-recuperación).

| Síntoma | Causa a comprobar | Acción |
|---|---|---|
| Compose no resuelve configuración | Variable obligatoria ausente | Completar .env según ejemplo y ejecutar config --quiet; no imprimir config con secretos |
| SQL no inicia o falta volumen externo | PHASE3_SQL_VOLUME inexistente/no inicializado o ya montado por otro SQL | Preparar copia restaurada y verificar que solo un motor use ese volumen; no borrarlo |
| Login no reconoce cuentas de Fase 2 | Auth apunta a PostgreSQL sin importar usuarios | Conciliar/importar snapshot mediante herramienta; no crear otra fuente de identidad SQL |
| Importación aborta | ID con fuente distinta o login duplicado | Revisar archivo y datos privadamente; transacción completa se revierte, no forzar overwrite |
| Auth no arranca por checksum | Se editó una migración ya aplicada | Recuperar versión aplicada y preparar migración nueva coordinada; no alterar checksum en la base |
| Gateway vivo pero ready 503 | Backend, Auth o Report no está ready | Revisar dependencia concreta y su base; no reiniciar Gateway por una caída ajena |
| Report vivo pero ready 503 | Mongo, índices o Auth indisponibles | Verificar credencial/base propia y Auth; una fuente de datos caída no es parte de Report readiness |
| API 401 | JWT inválido/vencido, cuenta inactiva; en monolito también lookup Auth fallido | Reingresar o diagnosticar Auth; comprobar claves/issuer/audience compartidos sin imprimirlos |
| API 403 | Identidad válida sin política/rol | Administrador revisa permisos; no cambiar cabeceras del navegador |
| Report 404 por ID conocido | Documento ajeno o inexistente | Usar la misma cuenta solicitante; admin tampoco descarga documentos ajenos |
| PDF 409/404 | Estado no Completed o archivo/referencia inconsistente/ausente | Consultar historial y solicitar documento nuevo; no inventar archivo a partir de metadatos |
| Report 422 o 504 | Exceso de filas/bytes o presupuesto agotado | Acotar fechas/filtros; no truncar datos ni desactivar límites |
| Report 429 | Una generación ocupa ese proceso | Esperar y reintentar manualmente; no repetir POST de forma automática |
| Gateway 502 o 504 | Conexión upstream o timeout | Verificar destino/red/health; no publicar puertos internos como solución |
| Historial no disponible | Fallo de red, Auth o Mongo | Actualizar historial tras recuperar dependencia; el mensaje se distingue de una selección vacía |
| Cambiar .env no cambia password Mongo | El volumen ya fue inicializado | Rotar credencial en la base y configuración de forma coordinada; no borrar volumen |
| Auditoría Auth retrasada | Receptor /internal/auth-events caído o rechaza eventos | Verificar backend y clave interna; la outbox conserva y reintenta sin duplicar por reenvío secuencial |
| Dos entradas Reportes | Duplicación encontrada en sidebar | Hallazgo funcional pendiente; no se modificó durante la revisión documental |

Cuando se reinicia Report, solicitudes Pending/Processing abandonadas por más de
dos minutos pasan a Failed al consultar historial/metadatos. No se regeneran sin
credenciales del solicitante. PDFs Completed ya guardados siguen descargables
si Auth y Mongo responden, aunque estén caídas las fuentes originales.

Las pruebas locales comprobaron caídas y reinicio; no prueban recuperación de
disco perdido. GridFS y metadatos deben respaldarse juntos. No hay reconciliador
automático de huérfanos ni borrado/retención configurados. Escalar por separado
requiere acordar capacidad, leases/cola y consistencia, no quitar el límite local.

La advertencia SCSS de sensores, deprecación TestClient y cierre forzado del Chrome
de pruebas se registran en [pruebas](pruebas.md); no se deben confundir con tests
fallidos. Si falla una ejecución nueva, conservar su resultado propio sin reutilizar
el conteo de la regresión anterior como si hubiera pasado nuevamente.
