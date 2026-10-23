# Manual de usuario

Vigía Rural permite consultar el monitoreo de comunidades y preparar documentos
PDF. Usa la dirección que proporcione la persona administradora. Las direcciones
locales de desarrollo no son direcciones públicas de producción.

## Iniciar y cerrar sesión

1. Abre la pantalla **Iniciar sesión** (`/login`).
2. Introduce el usuario y contraseña que te hayan asignado y pulsa el botón de ingreso.
3. Si los datos no son aceptados, revisa lo escrito y consulta al administrador.
4. Para terminar, usa **Cerrar sesión** en la cabecera. En un equipo compartido,
   cierra también la ventana del navegador.

[CAPTURA PENDIENTE: Pantalla de inicio de sesión]

- Abrir: `/login`, antes de ingresar.
- Debe verse: campos de usuario/contraseña y botón de ingreso, sin credenciales reales.
- Demuestra: acceso al sistema.

## Navegar y consultar el resumen

La navegación lateral permite abrir Resumen, Sensores, Alertas, Historial, Reglas
y Comunidades. Las opciones disponibles cambian según la cuenta: Eventos exige
sesión; Reportes requiere operador o administrador; Usuarios y Bitácora son de
administración. En pantallas pequeñas, desplaza horizontalmente la navegación
si una opción no está visible. Historial contiene mediciones; Mis reportes contiene
los documentos solicitados por tu cuenta.

En **Resumen** selecciona una comunidad para consultar sus indicadores, sensores,
alertas y actividad. “Sin comunidades” o “Comunidad sin sensores” significa que no
hay información para esa selección. Si aparece un error de conexión, usa Reintentar.
Los valores simulados son datos de demostración; no equivalen a mediciones físicas.

[CAPTURA PENDIENTE: Resumen de una comunidad]

- Abrir: Resumen (`/`), con una comunidad que tenga datos.
- Debe verse: selector, nombre de comunidad, indicadores y nivel/estado disponible.
- Demuestra: consulta del monitoreo. Evitar nombres o datos sensibles.

## Crear un reporte

1. Entra a **Reportes** desde la navegación o el acceso del resumen.
2. Elige el tipo: Comunidades, Sensores, Lecturas, Alertas, Historial de eventos,
   Estado de sensores, Estadísticas climáticas o Resumen ejecutivo.
3. Selecciona comunidad, sensor, variable o estado cuando estén disponibles.
   Al cambiar el tipo se limpian los filtros anteriores.
4. Para tipos con fechas, “Desde” y “Hasta” representan días completos en horario
   UTC. La fecha inicial no puede ser posterior a la final.
5. Pulsa **Generar reporte PDF** y espera el mensaje de resultado. El botón se
   bloquea durante la preparación para evitar solicitudes repetidas.
6. Cuando esté disponible, pulsa **Descargar PDF**.

Los inventarios de comunidades, sensores y estado de sensores muestran su situación
actual y no ofrecen fechas. Los eventos no ofrecen filtro por variable. Un sensor
seleccionado se limpia al cambiar de comunidad. Los selectores muestran hasta
2,000 opciones; si falta alguna, solicita ayuda al administrador.

[CAPTURA PENDIENTE: Formulario de reportes con filtros]

- Abrir: Reportes (`/reports`) con cuenta operadora; elegir Lecturas.
- Debe verse: tipo, comunidad/sensor, fechas, variable, estado y botón de generación.
- Demuestra: filtros disponibles según el contenido y selección de un rango válido.

[CAPTURA PENDIENTE: Reporte disponible]

- Abrir: Reportes después de generar un documento correctamente.
- Debe verse: mensaje de confirmación y botón Descargar PDF.
- Demuestra: resultado de la generación, sin mostrar información privada.

## Consultar el historial y descargar otra vez

En **Mis reportes** filtra por tipo y estado y pulsa **Actualizar historial**.
Puedes elegir 10, 20 o 50 registros por página y navegar con Anterior/Siguiente.
Solo ves documentos solicitados por tu propia cuenta, incluso si eres administrador.

Un documento Disponible puede descargarse varias veces. Pendiente/En proceso
todavía no permite descargar; actualiza el historial. Un documento Fallido muestra
el motivo disponible y requiere una nueva solicitud. No hay una opción de borrar
reportes en esta versión. El navegador puede preguntar dónde guardar el PDF.

[CAPTURA PENDIENTE: Historial de reportes]

- Abrir: Reportes, sección Mis reportes, con documentos propios existentes.
- Debe verse: filtros, fecha, usuario, estado, tamaño, descarga y paginación.
- Demuestra: búsqueda y recuperación de documentos ya preparados.

[CAPTURA PENDIENTE: Documento PDF abierto]

- Abrir: un PDF descargado con datos autorizados para la captura.
- Debe verse: título, solicitante, fecha UTC, filtros, tabla y pie con número de página.
- Demuestra: contenido descargado. Usar datos de prueba, no información sensible.

## Interpretar resultados y mensajes

**N/D** significa que la fuente no dispone de ese dato; no significa cero. Las
estadísticas separan variables y unidades. La lluvia acumulada se muestra N/D:
el nivel registrado no permite calcularla de forma confiable. El resumen ejecutivo
combina inventario actual con actividad del período elegido.

| Mensaje o situación | Qué hacer |
|---|---|
| Sesión no válida o vencida | Inicia sesión nuevamente |
| Cuenta sin permiso | Consulta al administrador; repetir el ingreso no cambia tus permisos |
| Sin reportes en la selección | Genera uno o cambia los filtros del historial |
| Historial no disponible | Usa Actualizar historial para reintentar |
| Servicio no disponible | Espera unos momentos y vuelve a intentar |
| Demasiados datos o tiempo agotado | Reduce fechas o aplica filtros |
| Hay un reporte en generación | Espera antes de solicitar otro |
| PDF no disponible | Genera un nuevo documento; comunica el problema si persiste |

[CAPTURA PENDIENTE: Mensaje de validación de fechas]

- Abrir: Reportes/Lecturas y seleccionar una fecha inicial posterior a la final.
- Debe verse: mensaje de fechas y generación deshabilitada, sin alterar datos.
- Demuestra: prevención de solicitudes incorrectas.

Las capturas están **PENDIENTES** y deben tomarse en el sistema local con datos de
prueba. La inspección visual interactiva no quedó validada en la revisión anterior.
Existe un hallazgo de dos accesos iguales a Reportes en la navegación; ambos apuntan
a la misma pantalla. Su corrección funcional queda fuera de esta revisión documental.
