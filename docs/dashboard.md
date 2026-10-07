# Dashboard final: Bloque 8, RF-ADM-60 a RF-ADM-68

GET /api/dashboard?communityId={guid} conserva su acceso publico y sus campos anteriores.
Sin communityId selecciona la primera comunidad por nombre. Una comunidad inexistente devuelve 404;
Angular muestra un estado vacio cuando no hay comunidades y no inventa datos ante errores de API.

| Requisito / campo | Fuente persistida | Alcance |
| --- | --- | --- |
| RF-ADM-60 / totalCommunities | COUNT Communities | Todas las comunidades registradas, activas e inactivas; global |
| RF-ADM-61 / activeSensors | Sensors con Status Active | Comunidad seleccionada |
| RF-ADM-62 / inactiveSensors | Sensors con Status Inactive | Comunidad seleccionada |
| RF-ADM-63 / activeAlerts | Alerts con Status Open | Comunidad seleccionada |
| RF-ADM-64 / alertDistributionByLevel | Alerts Open por Green, Yellow, Orange, Red | Comunidad seleccionada; incluye categorias con cero |
| RF-ADM-65 / readingEvolution | SensorReadings asociadas a Sensors | Ultimas 30 lecturas por sensor, orden determinista fecha/ID |
| RF-ADM-66 / communityId | Filtro backend y seleccion Angular | Sensores, alertas, indicadores, lecturas y eventos |
| RF-ADM-67 / recentEvents | EventService e IEventHistoryRepository del Bloque 7 | Ocho Event mas recientes por StartedAt/ID, sin duplicados |
| RF-ADM-68 / actualizacion | Nueva consulta de datos persistidos | Polling cada 15 segundos |

La UI muestra cuatro KPIs, distribucion Normal/Precaucion/Alerta/Emergencia, indicadores,
evolucion y eventos. Acknowledged y Closed no son alertas activas y no forman parte de la distribucion.
Las tarjetas de sensor conservan las ultimas 15 lecturas y la grafica general las ultimas 30 por variable.
El historial sigue incluyendo sensores inactivos: su estado no elimina las lecturas anteriores.

Eventos muestra ID real, fecha/hora, comunidad, fenomeno, nivel, estado y descripcion; su enlace abre
/events/:id. La consulta de detalle exige sesion. El resumen publico excluye responsables y metadatos
administrativos del DTO completo de eventos. No crea eventos desde alertas ni lecturas.
El modo de simulacion explicito anterior sigue siendo una demostracion separada; nunca sustituye una
API fallida silenciosamente. Las lecturas simuladas persistidas si son datos reales del sistema.

La carga inicial pertenece a DashboardDataService. DashboardPage inicia un solo timer a los 15 segundos,
lo cancela al destruirse y no inicia otro refresh mientras la consulta anterior siga pendiente. Cambiar
comunidad cancela la consulta anterior para evitar respuestas desactualizadas. El backend filtra antes
de materializar: COUNT global, sensores/reglas/alertas de la comunidad, lectura mas reciente por sensor,
consulta correlacionada con limite 30 por sensor y pagina de ocho eventos reutilizando el repositorio.
No hay WebSockets, cambios de reglas de negocio, esquema ni migraciones.

Validacion: DashboardAuditHttpTests cubre conteos, estados, distribucion, aislamiento entre comunidades,
identidad real de eventos y ventana de lecturas; pruebas Angular cubren KPIs renderizados, enlaces,
filtrado, historial persistido y polling sin duplicar carga inicial, cancelacion y destruccion.
Las pruebas HTTP utilizan EF InMemory, no una instancia real de SQL Server.
