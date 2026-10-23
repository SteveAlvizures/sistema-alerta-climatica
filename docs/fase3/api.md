# API de Fase 3

El navegador usa `/api` en el origen web. Gateway conserva paths y cuerpos; no
existe un prefijo público por tecnología o nombre de contenedor. Los health se
consultan sin `/api` en el origen Nginx o directamente al proceso.

## Auth y usuarios

| Método / ruta | Acceso y contrato |
|---|---|
| POST `/api/auth/login` | Público; `{username,password}` → `{accessToken,expiresAt,name,email,role}`, 200 |
| POST `/api/auth/logout` | JWT e identidad vigente; 204, no revoca el JWT |
| GET `/api/users` | Administrator; `page,pageSize,search,role,isActive`; paginado |
| GET `/api/users/{id}` | Administrator; usuario |
| POST `/api/users` | Administrator; `{name,username,password,role}` → usuario, 201 |
| PUT `/api/users/{id}` | Administrator; `{name,username}` → usuario, 200 |
| PATCH `/api/users/{id}/role` | Administrator; `{role}` → usuario, 200 |
| PATCH `/api/users/{id}/status` | Administrator; `{isActive}` → usuario, 200 |

Usuario: `{id,name,username,role,isActive,createdAt,lastAccessAt}`; no contiene hash
ni contraseña. Roles: `Administrator`, `Operator`, `ConsultationUser`. El legado
`User` se normaliza a ConsultationUser, pero no se admite para asignaciones nuevas.
Detalles de validación y conflictos en [Auth Service](auth-service.md).

## Report

Todos requieren Administrator/Operator y permiso vigente OperateSystem. Cada
documento pertenece al solicitante; no hay excepción global para administradores.

| Método / ruta | Respuesta |
|---|---|
| GET `/api/reports/types` | Array `{id,name,states,supportsDates,supportsSensor,supportsVariable}` |
| POST `/api/reports` | `{type,filters}` → metadatos Completed, 201 |
| GET `/api/reports` | Paginado propio; `page` 1..10000, `pageSize` 1..50, `type`, `state` |
| GET `/api/reports/{id}` o `/{id}/status` | Metadatos propios |
| GET `/api/reports/{id}/download` | PDF, Content-Disposition, no-store, nosniff |

Metadatos: `id,type,filters,user:{id,name},requestedAt,state,filename,generatedAt,
downloadedAt,size,error,correlationId`. Fechas ISO UTC; campos de archivo/error
pueden ser null según estado. `fileId` de GridFS no sale en la API. Estados:
Pending, Processing, Completed, Failed. `size` está en bytes.

```json
{"type":"readings","filters":{"dateFrom":"2026-10-22T00:00:00Z","dateTo":"2026-10-22T23:59:59.999Z","variable":"Temperature"}}
```

Tipos, filtros compatibles, variables y semántica exacta:
[Report Service](report-service.md#reportes-implementados-y-semántica) y
[models.py](../../services/report-service/app/models.py). No enviar fechas sin
zona horaria ni campos desconocidos. UUID se representan como cadenas.

Paginado común: `{data,pageIndex,pageSize,totalPages,totalCount,hasPrevious,hasNext}`.
400 validación; 401 sesión inválida/inactiva; 403 permiso; 404 ausente/ajeno;
409 PDF no disponible; 422 exceso de filas/bytes; 429 generación ocupada;
502 fuente/contrato incompleto; 503 identidad o persistencia no disponible;
504 timeout; 500 inesperado sanitizado. Gateway también puede responder 502/503/504
por transporte. Los ProblemDetails contienen correlación; login inválido conserva
su respuesta histórica `{message}`.

## Monolito e integración interna

Comunidades, sensores, lecturas, alertas, reglas, eventos, auditoría y dashboard
siguen en el monolito. [Integración pendiente](integracion-pendiente.md) enumera
rutas, filtros y modelos exactos; [endpoints de Fase 2](../endpoints.md) es referencia
del dominio, con Auth/Report sustituidos por los contratos actuales de esta página.

Auth GET `/internal/identity/current` requiere clave de servicio y bearer; devuelve
identidad vigente con permissions. Monolito POST `/internal/auth-events` recibe la
outbox e identifica duplicados por id. Son contratos internos y no se publican
por Gateway. Esquemas y acciones: [integración pendiente](integracion-pendiente.md#audit).
No existe Swagger público de Report: docs/redoc/openapi están deshabilitados.
