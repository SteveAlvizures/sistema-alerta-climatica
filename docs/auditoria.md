# Auditoria completa: Bloque 8, RF-ADM-56 a RF-ADM-59

AuditAction conserva usuario, accion, OccurredAt (fecha/hora UTC), entidad, identificador y descripcion.
El actor autenticado procede del JWT, nunca de username/userId del cuerpo. Login usa el usuario cuya
contrasena y estado fueron validados. No se cambian registros anteriores ni nombres persistidos.

## Auditoria de operaciones y catalogo

| Operacion | Acciones finales | Estado al auditar |
| --- | --- | --- |
| Sesion | Login, Logout | Login ya existia; Logout agregado |
| Comunidades | ComunidadCreada, ComunidadEditada, ComunidadActivada, ComunidadDesactivada, ComunidadEliminada | Crear/editar/estado ya existian; eliminar agregado |
| Sensores | SensorCreado, SensorEditado, SensorActivado, SensorDesactivado, MonitoreoReiniciado | Ya existian; reinicio de sensor ya activo conserva su accion especifica |
| Lecturas | LecturaManualCreada, ValorSimuladoModificado | Nueva lectura usa nombre oficial; valor simulado ya existia |
| Reglas | ReglaCreada, ReglaEditada, ReglaActivada, ReglaDesactivada | Ya existian; PUT registra tambien el estado solo si cambia |
| Usuarios | UsuarioCreado, UsuarioEditado, UsuarioActivado, UsuarioDesactivado, RolUsuarioCambiado | Ya existian |
| Alertas | AlertaAtendida, AlertaCerrada | Ya existian |

Lectura manual y cambio de valor simulado son dos hechos distintos, registrados una vez cada uno.
El filtro LecturaManualCreada incluye los antiguos CreateManualReading sin reescribirlos; la UI permite
consultar tambien su nombre historico. AlertaReconocida y AlertaResuelta permanecen como opciones de
consulta historicas. No se agrega auditoria a lecturas automaticas ni consultas GET.
La eliminacion de comunidades sigue bloqueada si existen dependencias. No se introducen endpoints
DELETE para usuarios, sensores o reglas ni se elimina historial.

## Logout stateless

POST /api/auth/logout, policy AuthenticatedUser: cualquier sesion JWT valida, sin aceptar identidad
externa. Devuelve 204, o 401 sin sesion. Registra Logout con usuario e ID del JWT.
Angular envia el JWT vigente, elimina inmediatamente token/sesion de sessionStorage y navega a /.
La limpieza local ocurre tambien si falla la peticion de auditoria. Una sesion vencida se limpia sin
llamar a logout ni recursiones del interceptor. No se revoca JWT ni se crea blacklist: un token copiado
sigue siendo valido hasta su expiracion, sujeto a las validaciones existentes de usuario/rol/estado.
Si el backend no esta disponible, el logout local funciona pero no puede garantizarse el registro.

## Consulta y seguridad

GET /api/audit-actions, policy AdministratorOnly; 401 sin JWT, 403 Operator/ConsultationUser,
200 Administrator. Sidebar y guard Angular conservan el mismo permiso.

Parametros: user, action, entity, from, to, page (1), pageSize (20; maximo 100).
user busca nombre o login/email; action exacta (salvo compatibilidad de lectura manual); entity contiene
texto; fechas UTC inclusivas. Desde/hasta invalidos o paginacion invalida devuelven 400.
Se conservan username/dateFrom/dateTo como aliases; parametros oficiales tienen precedencia.
Todos los filtros se aplican en IQueryable antes de COUNT, ORDER BY OccurredAt DESC, Id DESC y paginacion.

La pantalla conserva filtros de usuario/accion/entidad/fechas, limpiar, paginacion y tamanos de pagina.
Muestra fecha/hora local, usuario, accion, entidad, identificador y descripcion. Los dias seleccionados
se interpretan en la zona local del navegador y se convierten a UTC para enviar los limites completos.

Validacion: pruebas HTTP de roles reales, logout, identidad JWT, filtros combinados y aislados,
eliminacion y lectura; pruebas existentes de CRUD/estado/rol y lifecycle; pruebas de servicio con
paginacion y legado; pruebas Angular de filtros, etiquetas, permisos, sidebar, logout y redireccion.
No hay migraciones ni cambios de JWT, usuarios o persistencia de dominio.

Limitacion existente: varios controladores guardan la operacion y despues la auditoria en SaveChanges
separados. Un fallo de almacenamiento intermedio puede dejar una operacion sin registro; hacer todas
esas escrituras atomicas requiere un cambio transaccional adicional fuera de este refactor acotado.
