# Guía de defensa técnica

Respuestas breves basadas en la implementación actual de Vigía Rural.

## Sistema y arquitectura

### ¿Qué es Vigía Rural?

Un sistema web académico de monitoreo y alerta temprana para riesgos climáticos en comunidades rurales. Trabaja actualmente con lecturas simuladas persistidas en SQL Server.

### ¿Cómo funciona el sistema?

Angular consulta la API; la API coordina servicios de aplicación; Infrastructure persiste con EF Core; las reglas evalúan lecturas y pueden producir alertas que luego se muestran en Dashboard, Alertas e Historial.

### ¿Qué es Clean Architecture?

Una forma de separar responsabilidades y orientar dependencias hacia el negocio. Facilita probar casos de uso y reemplazar detalles de infraestructura.

### ¿Qué función tiene Domain?

Contiene entidades como Comunidad, Sensor, Lectura, Regla y Alerta, además de enumeraciones y comportamiento del negocio.

### ¿Qué hace Application?

Implementa casos de uso, validaciones, DTOs y contratos de persistencia. Por ejemplo, `CommunityService` decide si una comunidad puede actualizarse o eliminarse.

### ¿Qué hace Infrastructure?

Implementa repositorios, `ClimateAlertDbContext`, configuraciones EF Core, SQL Server y servicios técnicos como la simulación alojada.

### ¿Qué hace un Controller?

Recibe HTTP, delega el caso de uso y devuelve el código y DTO apropiados. Debe evitar lógica de negocio importante.

### ¿Qué es un DTO?

Un objeto para transportar datos entre capas o por la API sin exponer directamente la entidad persistida. Ejemplo: `UpdateCommunityRequest`.

### ¿Qué es Entity Framework Core?

El framework de acceso a datos utilizado por .NET para consultar y modificar SQL Server a partir de clases y expresiones LINQ.

### ¿Qué es un ORM?

Una herramienta que relaciona objetos del programa con tablas y filas. EF Core es el ORM de Vigía Rural.

### ¿Cómo se comunica Angular con SQL Server?

No se comunica directamente. Angular → HTTP → API → Application → Infrastructure/EF Core → SQL Server.

### ¿Qué es un endpoint?

Una combinación de verbo HTTP y ruta que ofrece una operación, por ejemplo `GET /api/communities`.

## HTTP y CRUD

### ¿Qué hace GET?

Consulta recursos sin crear ni modificar. Ejemplo: listar comunidades.

### ¿Qué hace POST?

Crea un recurso o inicia una operación. `POST /api/communities` devuelve `201 Created`.

### ¿Qué hace PUT?

Actualiza la representación soportada de un recurso existente. Comunidades permite nombre, ubicación y descripción.

### ¿Qué hace PATCH?

Aplica un cambio parcial o una acción concreta. Se usa para estado de sensores y reglas, y ciclo de alertas.

### ¿Qué hace DELETE?

Solicita eliminar un recurso. Una comunidad vacía devuelve `204`; si tiene dependencias devuelve `409`.

### ¿Qué es CRUD?

Create, Read, Update y Delete. En comunidades se demuestra con POST, GET, PUT y DELETE.

### ¿Qué es QueryString?

Son parámetros después de `?`, por ejemplo `?page=2&pageSize=20`.

### ¿Qué significa 200 OK?

La consulta o actualización terminó correctamente y normalmente incluye respuesta.

### ¿Qué significa 201 Created?

Se creó un recurso. La API lo usa al crear comunidades, sensores, reglas y lecturas.

### ¿Qué significa 204 No Content?

La operación fue exitosa sin cuerpo. Se usa al borrar una comunidad vacía.

### ¿Qué significa 400 Bad Request?

El cliente envió datos inválidos; por ejemplo nombre vacío o `pageSize=101`.

### ¿Qué significa 401 Unauthorized?

Falta autenticación válida, normalmente un JWT aceptado.

### ¿Qué significa 403 Forbidden?

El usuario está autenticado, pero no tiene el rol requerido.

### ¿Qué significa 404 Not Found?

El identificador solicitado no corresponde a un recurso existente.

### ¿Qué significa 409 Conflict?

La solicitud entra en conflicto con el estado actual, como borrar una comunidad con sensores.

### ¿Qué significa 500 Internal Server Error?

Ocurrió un fallo no controlado. Vigía Rural registra el error y devuelve un mensaje genérico sin traza sensible.

## Paginación

### ¿Qué es Offset-Based Pagination?

Divide resultados por desplazamiento y tamaño. Vigía Rural calcula el desplazamiento con `(page - 1) * pageSize`.

### ¿Qué es Cursor-Based Pagination?

Continúa desde un identificador o valor del último registro recibido. Puede ser más estable ante inserciones frecuentes, pero no es la estrategia usada en Historial.

### ¿Qué hacen Skip y Take?

`Skip` omite el desplazamiento y `Take` limita el número de filas. Se ejecutan antes de materializar la consulta.

### ¿Qué es OFFSET/FETCH?

Es el patrón SQL Server equivalente para saltar filas ordenadas y traer solo la página solicitada. EF Core genera SQL equivalente a partir de `Skip/Take`.

## Seguridad

### ¿Qué es JWT?

Un token firmado que contiene identidad, rol y expiración. La API valida firma, issuer, audience y vigencia.

### ¿Qué es un Guard?

Una función de Angular que decide si una ruta puede activarse según la sesión y el rol.

### ¿Cuál es la diferencia entre Guard y autorización backend?

El Guard mejora UX y navegación. El backend es la barrera de seguridad porque también rechaza llamadas directas no autorizadas.

### ¿Cómo se protege la contraseña?

Con `PasswordHasher<User>`. Se persiste un hash verificable, no la contraseña en texto plano.

### ¿Cómo viaja el token?

Angular lo conserva en `sessionStorage` y el interceptor agrega `Authorization: Bearer <token>`.

### ¿Cuáles son los niveles de acceso?

El visitante consulta sin cuenta ni JWT. El usuario autenticado recibe el rol `User` y mantiene acceso de consulta. El administrador recibe `Administrator` y además puede operar CRUD, administrar sensores y reglas, atender alertas y consultar la bitácora.

### ¿Qué ocurre al cerrar sesión?

Angular elimina la sesión de `sessionStorage` y navega a `/`, por lo que la interfaz vuelve inmediatamente al estado de visitante.

## Docker y Linux

### ¿Qué es Docker?

Una plataforma para empaquetar aplicaciones y sus dependencias en contenedores reproducibles.

### ¿Qué es Docker Compose?

La herramienta que define y coordina frontend, backend, SQL Server, red, puertos, dependencias y volumen.

### ¿Qué función cumple Nginx?

Sirve los archivos compilados de Angular y redirige las solicitudes `/api` al backend según la configuración del contenedor.

### ¿Qué es Ubuntu WSL2 en este proyecto?

El entorno Linux local usado para validar Docker, Compose, health y acceso con `curl`. No es un VPS.

### ¿Qué conserva el volumen SQL Server?

Los archivos de la base entre recreaciones de contenedores. `docker compose down` lo conserva; `--volumes` lo eliminaría.

## Flujos del dominio

### ¿Cuál es el flujo completo de una lectura climática?

El simulador o POST crea un request → Application valida sensor y lectura → EF Core agrega la lectura → el evaluador revisa reglas → se guardan cambios → Angular consulta el resultado.

### ¿Cómo una regla genera una alerta?

El evaluador busca reglas activas, vigentes y compatibles con comunidad, sensor y variable. Si el valor cumple límites, abre o actualiza una alerta con la lectura como evidencia y puede asociarla a un evento.

### ¿Cómo una acción genera una entrada en Bitácora?

Las acciones administrativas integradas llaman al servicio de auditoría con usuario, acción, entidad, ID y descripción. La entrada se persiste y se consulta en `/api/audit-actions` solo como Administrator.

### ¿Por qué DELETE de comunidad puede devolver 409?

Porque sensores, reglas, alertas o eventos dependen de ella. El backend comprueba esas relaciones y evita un borrado inseguro.

### ¿Por qué la paginación mejora el rendimiento?

Porque SQL Server devuelve solo la página. La memoria y la transferencia no crecen con todo el historial.

## Demostración sugerida

1. Abrir Dashboard y explicar Angular → API → SQL Server.
2. Mostrar GET público de comunidades como visitante.
3. Iniciar con `user` / `user`, confirmar la etiqueta Usuario y comprobar que no aparecen controles administrativos.
4. Cerrar sesión, iniciar con `admin` / `admin` y editar solo una comunidad temporal mediante PUT.
5. Mostrar Historial y cambiar de página para evidenciar la query string.
6. Explicar `401`, `403`, `404` y el `409` de una comunidad con sensores sin eliminarla.
7. Mostrar `docker compose ps`, `/health` y resultados de pruebas.

Las credenciales anteriores son solo para la demostración académica local; en cualquier despliegue deben cambiarse por secretos robustos.
