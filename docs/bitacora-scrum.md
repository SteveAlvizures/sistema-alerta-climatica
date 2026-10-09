# Bitácora Scrum — Proyecto 1

## Equipo

| Integrante | Rol Scrum |
|---|---|
| Astrid | Product Owner |
| Steve | Scrum Master |
| Walter | Developer |

La primera reunión tiene fecha y hora confirmadas. Los demás registros resumen seguimientos y revisiones del proyecto; no se presentan como reuniones formales cuando no existe evidencia de ello.

## Reunión 1 — Organización inicial

Fecha: 17 de agosto de 2026  
Hora: 9:30 p. m.  
Tipo: Reunión virtual de inicio  
Objetivo: Revisar requerimientos, asignar roles y organizar el trabajo inicial.

### Astrid — Product Owner

Tarea asignada: Revisar requerimientos y priorizar las necesidades del producto.  
¿Qué hice ayer?: No aplica; fue la reunión inicial.  
¿Qué haré hoy?: Organizaré los módulos y criterios básicos de experiencia de usuario.  
¿Tengo algún impedimento?: Ninguno identificado.

### Steve — Scrum Master

Tarea asignada: Coordinar el repositorio, reuniones y seguimiento de tareas.  
¿Qué hice ayer?: No aplica; fue la reunión inicial.  
¿Qué haré hoy?: Prepararé el repositorio y el flujo de ramas individuales.  
¿Tengo algún impedimento?: Ninguno identificado.

### Walter — Developer

Tarea asignada: Apoyar la preparación técnica de backend, Docker y Linux.  
¿Qué hice ayer?: No aplica; fue la reunión inicial.  
¿Qué haré hoy?: Revisaré tecnologías y requisitos del entorno de desarrollo.  
¿Tengo algún impedimento?: Ninguno identificado.

### Acuerdos generales

Usar un repositorio compartido, ramas individuales y revisiones periódicas. Las primeras tareas serán preparar la arquitectura, el entorno y la estructura base.

### Próximos pasos

Definir Clean Architecture y levantar los proyectos iniciales de Angular y .NET.

## Reunión 2 — Arquitectura y entorno

Fecha: pendiente de completar  
Hora: pendiente de completar  
Tipo: Seguimiento Scrum  
Objetivo: Alinear arquitectura, tecnologías y ejecución local.

### Astrid — Product Owner

Tarea asignada: Validar que la estructura contemple los módulos requeridos.  
¿Qué hice ayer?: Organicé los módulos y prioridades iniciales.  
¿Qué haré hoy?: Revisaré la navegación prevista para el frontend Angular.  
¿Tengo algún impedimento?: Aún no había una versión completa para recorrer.

### Steve — Scrum Master

Tarea asignada: Coordinar la estructura backend/frontend y sus dependencias.  
¿Qué hice ayer?: Preparé el repositorio y el flujo de ramas.  
¿Qué haré hoy?: Integraré la solución .NET con Domain, Application, Infrastructure y API.  
¿Tengo algún impedimento?: Faltaba validar la configuración entre servicios.

### Walter — Developer

Tarea asignada: Revisar SQL Server, Docker y compatibilidad con Linux.  
¿Qué hice ayer?: Revisé tecnologías y requisitos técnicos.  
¿Qué haré hoy?: Apoyaré la configuración de contenedores y persistencia.  
¿Tengo algún impedimento?: Variables de entorno e imágenes aún estaban en preparación.

### Acuerdos generales

Angular consumirá la API HTTP; .NET aplicará la separación por capas; EF Core accederá a SQL Server; Docker Compose coordinará los servicios.

### Próximos pasos

Implementar comunidades y sensores sobre la estructura acordada.

## Reunión 3 — Comunidades, sensores e integración

Fecha: pendiente de completar  
Hora: pendiente de completar  
Tipo: Revisión técnica  
Objetivo: Revisar módulos iniciales, endpoints e integración del trabajo.

### Astrid — Product Owner

Tarea asignada: Revisar campos, formularios y consulta de comunidades y sensores.  
¿Qué hice ayer?: Evalué la navegación prevista para Angular.  
¿Qué haré hoy?: Probaré que las pantallas sean comprensibles para visitantes.  
¿Tengo algún impedimento?: Se necesitaban datos representativos para validar la interfaz.

### Steve — Scrum Master

Tarea asignada: Coordinar endpoints, DTOs, pruebas e integración de ramas.  
¿Qué hice ayer?: Organicé los proyectos y la separación por capas.  
¿Qué haré hoy?: Revisaré contratos entre Angular y la API y resolveré diferencias de integración.  
¿Tengo algún impedimento?: Algunos modelos y rutas requerían ajustes conjuntos.

### Walter — Developer

Tarea asignada: Validar persistencia, EF Core y ejecución con Docker.  
¿Qué hice ayer?: Apoyé la configuración de contenedores y SQL Server.  
¿Qué haré hoy?: Revisaré relaciones, configuraciones y pruebas de los endpoints.  
¿Tengo algún impedimento?: Fue necesario corregir mapeos y tiempos de inicio de Docker.

### Acuerdos generales

Mantener Controllers delgados, usar DTOs y preservar las relaciones entre comunidades y sensores durante las correcciones.

### Próximos pasos

Integrar Dashboard, autenticación y un dataset adecuado para la demostración.

## Reunión 4 — Dashboard, autenticación y datos de demostración

Fecha: pendiente de completar  
Hora: pendiente de completar  
Tipo: Sprint Review  
Objetivo: Revisar visualización, seguridad y datos simulados de Guatemala.

### Astrid — Product Owner

Tarea asignada: Evaluar Dashboard, login y diferencia visual entre visitante y administrador.  
¿Qué hice ayer?: Probé las pantallas iniciales de comunidades y sensores.  
¿Qué haré hoy?: Revisaré indicadores, mensajes y acceso a acciones administrativas.  
¿Tengo algún impedimento?: Era necesario identificar claramente que las lecturas son simuladas.

### Steve — Scrum Master

Tarea asignada: Coordinar integración del Dashboard, JWT y sesión Angular.  
¿Qué hice ayer?: Revisé contratos e integración de los módulos iniciales.  
¿Qué haré hoy?: Validaré interceptor, guards y autorización por rol en la API.  
¿Tengo algún impedimento?: La configuración JWT debía mantenerse fuera del repositorio.

### Walter — Developer

Tarea asignada: Revisar el dataset, sensores simulados y persistencia de lecturas.  
¿Qué hice ayer?: Validé relaciones y correcciones de EF Core y Docker.  
¿Qué haré hoy?: Comprobaré el seed idempotente y la ejecución en SQL Server.  
¿Tengo algún impedimento?: El simulador aumenta el conteo de lecturas con el tiempo.

### Acuerdos generales

Usar seis comunidades reales de Guatemala con sensores y lecturas simuladas. El guard mejora la experiencia, pero el backend aplica la autorización real.

### Próximos pasos

Completar alertas, historial, tendencias y herramientas administrativas.

## Reunión 5 — Alertas, historial y administración

Fecha: pendiente de completar  
Hora: pendiente de completar  
Tipo: Revisión funcional  
Objetivo: Evaluar monitoreo histórico, reglas y acciones administrativas.

### Astrid — Product Owner

Tarea asignada: Revisar claridad de alertas, tendencias, bitácora y notificaciones visuales.  
¿Qué hice ayer?: Evalué Dashboard, login y permisos visibles.  
¿Qué haré hoy?: Probaré filtros, estados de alerta y mensajes de confirmación.  
¿Tengo algún impedimento?: El historial debía presentar mucha información sin saturar la vista.

### Steve — Scrum Master

Tarea asignada: Coordinar integración de historial, reglas y administración de sensores.  
¿Qué hice ayer?: Validé JWT, interceptor, guards y autorización backend.  
¿Qué haré hoy?: Alinearé servicios Angular con los contratos de alertas, reglas y auditoría.  
¿Tengo algún impedimento?: Era necesario mantener consistencia entre varios estados y respuestas HTTP.

### Walter — Developer

Tarea asignada: Validar evaluación de reglas, ciclo de alertas y consultas persistidas.  
¿Qué hice ayer?: Comprobé el seed y las lecturas simuladas en SQL Server.  
¿Qué haré hoy?: Revisaré apertura, reconocimiento, resolución y registro de acciones.  
¿Tengo algún impedimento?: Algunos casos de transición y vigencia requerían pruebas adicionales.

### Acuerdos generales

Las reglas permanecen configurables; las alertas conservan evidencia; la bitácora es administrativa; y las notificaciones actuales son visuales.

### Próximos pasos

Cerrar CRUD de comunidades, paginación, pruebas y documentación para la defensa.

## Reunión 6 — Cierre y preparación de presentación

Fecha: pendiente de completar  
Hora: pendiente de completar  
Tipo: Revisión de cierre  
Objetivo: Validar requisitos técnicos y preparar la presentación final.

### Astrid — Product Owner

Tarea asignada: Revisar el recorrido de demostración y la edición administrativa de comunidades.  
¿Qué hice ayer?: Probé alertas, tendencias, filtros y mensajes.  
¿Qué haré hoy?: Ensayaré CRUD, paginación y explicación del valor del sistema.  
¿Tengo algún impedimento?: Ajustar la demostración al tiempo disponible.

### Steve — Scrum Master

Tarea asignada: Consolidar pruebas, códigos HTTP, documentación y orden de exposición.  
¿Qué hice ayer?: Coordiné contratos de historial, reglas y bitácora.  
¿Qué haré hoy?: Verificaré resultados finales, Docker y guía de defensa.  
¿Tengo algún impedimento?: Mantener sincronizados documentación, API y frontend.

### Walter — Developer

Tarea asignada: Validar backend, paginación SQL y operación desde Ubuntu WSL2.  
¿Qué hice ayer?: Revisé reglas, ciclo de alertas y auditoría.  
¿Qué haré hoy?: Comprobaré builds, pruebas, health y estados de Docker Compose.  
¿Tengo algún impedimento?: El motor Docker debe estar iniciado antes de la presentación.

### Acuerdos generales

Demostrar CRUD completo de comunidades, Offset-Based Pagination, códigos `200`, `201`, `204`, `400`, `401`, `403`, `404` y `409`, y los servicios saludables. No exponer credenciales ni modificar datos oficiales.

### Próximos pasos

Realizar la defensa y registrar las mejoras futuras como backlog.
